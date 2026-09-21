using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[Serializable]
public sealed class AutoplayOptions
{
    public int Seed = 42;
    public bool DebugPlaythrough, Godmode = true, InfiniteResources = true;
    public bool ExploreAll;
    public float Speed = 4, TimeLimitMinutes = 60, StallSeconds = 120;
}

[Serializable]
public sealed class AutoplayReport
{
    public string Outcome = "Running", Reason, StartedUtc, EndedUtc, GameVersion, BuildId, UnityVersion;
    public string Policy = "astar-objectives-v1", Observation = "Full campaign graph and full dungeon map/stairs (omniscient navigation)";
    public AutoplayOptions Options;
    public bool EligibleForBalance;
    public bool InfiniteStrength;
    public bool ValidationOnly;
    public double Seconds;
    public int Actions, DungeonTurns, DamageTaken, HealingReceived, ItemsUsed;
    public string Location, Scene;
    public int Floor, Gold;
    public CampaignSnapshot Campaign;
    public string[] Visited, Inventory;
    public List<AutoplayActor> Party = new();
    public List<AutoplayActor> Enemies = new();
    public Vector3Int[] WalkableTiles, DiscoveredTiles, Stairs;
}

[Serializable]
public sealed class AutoplayActor
{
    public string Name, Id;
    public int HP, MaxHP, SP, Hunger, Level, Strength, Defense, X, Y;
    public string[] Equipment, Skills;
}

/// <summary>Opt-in isolated session shared by the app's Watch Demo and the manual editor launcher.</summary>
public sealed class AutoplayRunner : MonoBehaviour
{
    public const string PendingKey = "EternalEnigma.Autoplay.Pending";
    public static AutoplayRunner Active { get; private set; }
    public AutoplayOptions Options { get; private set; }
    public AutoplayReport Report { get; private set; }
    public string DirectoryPath { get; private set; }
    public bool Paused { get; private set; }
    public bool Running => Report?.Outcome == "Running";
    public string Status { get; private set; } = "Starting";
    private IDisposable saveScope;
    private AutoplayStore store;
    private StreamWriter actions;
    private double started, lastProgress, pausedAt, pausedSeconds;
    private float nextTick;
    private string progressSignature, pendingError, floorIdentity;
    private readonly HashSet<string> visited = new(), triedParties = new();
    private readonly HashSet<Vector3Int> discovered = new();
    private readonly Dictionary<string, int> repetitions = new();
    private readonly HashSet<TownBuilding> shopped = new();
    private readonly Queue<GridPoint> worldSteps = new();
    private OverworldScene pathWorld;
    private readonly AutoplayRoute townRoute = new(), dungeonRoute = new(), approachRoute = new();
    private Vector3Int? combatObjective;
    private Town preparedTown;
    private GameSaveData originalSave;
    private CampaignContext originalContext;
    private TownConfiguration originalTownConfiguration;
    private UnityEngine.Random.State originalRandom;
    private float originalTimeScale;
    private bool appSession;
    public bool ReturnPromptOpen { get; private set; }
    private bool wasPausedBeforePrompt, exiting;
    private float acceptInputAfter;
    private bool startedCampaign, disposed;
    private static readonly Vector3Int[] Steps = {
        Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left,
        new(1,1), new(1,-1), new(-1,-1), new(-1,1) };

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var json = UnityEditor.SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return;
        UnityEditor.SessionState.EraseString(PendingKey);
        var runner = new GameObject("Manual playthrough").AddComponent<AutoplayRunner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.Initialize(JsonUtility.FromJson<AutoplayOptions>(json));
    }
#endif

    public static void WatchDemo(AutoplayOptions options)
    {
        if (Active != null || Common.Instance.Travel.IsTransitioning) return;
        var runner = new GameObject("Watch demo").AddComponent<AutoplayRunner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.appSession = true;
        runner.originalSave = Common.Instance.GameSaveData;
        runner.originalContext = Common.Instance.CampaignContext;
        runner.originalTownConfiguration = Common.Instance.CurrentTownConfiguration;
        runner.Initialize(options);
    }

    private void Initialize(AutoplayOptions options)
    {
        Active = this; Options = options;
        acceptInputAfter = Time.unscaledTime + .75f;
        originalTimeScale = Time.timeScale; originalRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(options.Seed);
        string root = Application.isEditor ? "Logs" : Application.persistentDataPath;
        DirectoryPath = Path.GetFullPath(Path.Combine(root, "Playthroughs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        Directory.CreateDirectory(DirectoryPath);
        store = new AutoplayStore(DirectoryPath);
        saveScope = SaveSystem.UseStore(store);
        actions = new StreamWriter(Path.Combine(DirectoryPath, "actions.jsonl")) { AutoFlush = true };
        started = lastProgress = Time.realtimeSinceStartupAsDouble;
        Report = new AutoplayReport { Options = options, EligibleForBalance = !options.DebugPlaythrough,
            InfiniteStrength = options.DebugPlaythrough && options.Godmode,
            StartedUtc = DateTime.UtcNow.ToString("O"), GameVersion = Application.version,
            BuildId = typeof(Game).Assembly.ManifestModule.ModuleVersionId.ToString(), UnityVersion = Application.unityVersion };
        Application.logMessageReceived += OnLog;
        Time.timeScale = Options.Speed;
        WriteReport();
    }

    private IEnumerator Start()
    {
        if (Options == null) yield break;
        while (FindFirstObjectByType<Common>() == null) yield return null;
        if (!appSession) yield return SceneManager.LoadSceneAsync("MainMenu");
    }

    public static bool Protects(Vitals vitals) => Active != null && Active.Running && Active.Options.DebugPlaythrough &&
        Active.Options.Godmode && IsPartyVitals(vitals);
    public static bool Replenishes(Vitals vitals) => Active != null && Active.Running && Active.Options.DebugPlaythrough &&
        Active.Options.InfiniteResources && IsPartyVitals(vitals);
    private static bool IsPartyVitals(Vitals vitals) => Game.Instance != null && Game.Instance.Allies != null &&
        Game.Instance.Allies.Any(a => a != null && (ReferenceEquals(a.Vitals, vitals) || ReferenceEquals(a.DisplayedVitals, vitals)));
    public static bool InfiniteResourcesFor(Character actor) => Active != null && Active.Running &&
        Active.Options.DebugPlaythrough && Active.Options.InfiniteResources && actor is Ally;
    public static bool GodmodeFor(Character actor) => Active != null && Active.Running &&
        Active.Options.DebugPlaythrough && Active.Options.Godmode && actor is Ally;
    public static void RecordHPChange(Vitals vitals, int before, int after)
    {
        if (Active == null || !Active.Running || Game.Instance == null || !Game.Instance.IsReady || Game.Instance.Allies == null ||
            !Game.Instance.Allies.Any(a => a != null && ReferenceEquals(a.Vitals,vitals))) return;
        Active.Report.DamageTaken += Math.Max(0,before-after);
        Active.Report.HealingReceived += Math.Max(0,after-before);
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (Running && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            pendingError ??= message + "\n" + stack;
    }

    private void Update()
    {
        if (appSession && !exiting)
        {
            if (ReturnPromptOpen)
            {
                if (Time.unscaledTime > acceptInputAfter)
                {
                    if (Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true) ConfirmReturn(true);
                    else if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true) ConfirmReturn(false);
                }
                return;
            }
            if (Time.unscaledTime > acceptInputAfter && UserInput()) { RequestReturn(); return; }
        }
        if (!Running || Paused) return;
        if (pendingError != null) { Finish("GameError", pendingError); return; }
        var now = Time.realtimeSinceStartupAsDouble;
        if (now - started - pausedSeconds > Options.TimeLimitMinutes * 60) { Finish("TimeLimit", "Manual run time limit reached."); return; }
        if (now - lastProgress > Options.StallSeconds) { Finish("Stalled", "No observable progress: " + Status); return; }
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + .03f / Options.Speed;
        try { Tick(); }
        catch (Exception error) { Finish("AutomationError", error.ToString()); }
    }

    private void Tick()
    {
        var common = FindFirstObjectByType<Common>();
        if (common == null || common.Travel.IsTransitioning) return;
        if (!startedCampaign)
        {
            var menu = FindFirstObjectByType<MainMenu>();
            if (menu == null) return;
            common.GameSaveData = menu.CreateNewSave(Options.Seed);
            startedCampaign = true;
            common.Travel.NewCampaign(Options.Seed);
            Log("New campaign"); return;
        }
        var context = common.CampaignContext;
        if (context == null) { Finish("Unsupported", "Campaign context disappeared."); return; }
        TrackProgress(context);
        if (context.State.Finished && (!Options.ExploreAll || context.Campaign.Locations.All(l =>
            IsDungeon(l) ? context.Completed.Contains(l.Id) : visited.Contains(l.Id))))
        { Finish("Victory", Options.ExploreAll ? "Campaign complete; every generated destination visited and dungeon completed once." : "Campaign Finished state reached."); return; }
        var game = FindFirstObjectByType<Game>();
        if (game != null) { DungeonTick(game); return; }
        var town = FindFirstObjectByType<Town>();
        if (town != null) { if (town.IsReady) TownTick(town, context); return; }
        var world = FindFirstObjectByType<OverworldScene>();
        if (world != null && world.IsReady && !world.IsMoving) WorldTick(world, context);
    }

    private void TrackProgress(CampaignContext context)
    {
        string signature = context.State.Scene + "/" + context.State.PendingDungeon + "/" + context.State.LocationId + "/" +
            context.Completed.Count + "/" + context.Claimed.Count + "/" + context.Opened.Count;
        if (signature == progressSignature) return;
        progressSignature = signature; repetitions.Clear(); lastProgress = Time.realtimeSinceStartupAsDouble;
        Capture(); WriteReport();
    }

    private static bool IsDungeon(CampaignLocation location) => location.Kind == LocationKind.StoryDungeon ||
        location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon;

    private void TownTick(Town town, CampaignContext context)
    {
        var player = town.TownPlayer;
        if (player.IsBusy || player.ControllingTownAlly == null) return;
        visited.Add(town.Configuration.Id);
        if (Options.DebugPlaythrough && Options.InfiniteResources) player.Gold = 1000000;
        if (preparedTown != town)
        {
            preparedTown = town; shopped.Clear(); townRoute.Clear();
            ChooseParty(town, context);
            foreach (var source in context.Campaign.Sources.Where(s => s.LocationId == town.Configuration.Id)) context.Claim(source.Id);
            EquipParty(town);
            Log("Prepare town party");
        }
        var interior = context.Campaign.Locations.FirstOrDefault(l => l.ParentTownId == town.Configuration.Id && !context.Completed.Contains(l.Id));
        Vector3Int target;
        var shop = town.TownBuildings.FirstOrDefault(b => !shopped.Contains(b) && b.Definition.ShopCatalog.Any(o => WantsOffer(town,o)));
        if (shop != null)
        {
            target = shop.TilemapPosition;
            if (player.ControllingTownAlly.TilemapPosition == target)
            {
                foreach (var offer in shop.Definition.ShopCatalog)
                    for (int i=0;i<4 && WantsOffer(town,offer);i++)
                    {
                        if (!town.Services.Buy(shop.Definition,offer.Item.ItemName,out _)) break;
                        EquipParty(town); Log("Buy " + offer.Item.ItemName);
                    }
                shopped.Add(shop); return;
            }
        }
        else if (interior != null)
        {
            var menu = FindFirstObjectByType<TownMenu>();
            var entrance = town.TownBuildings.FirstOrDefault(b => b.Definition.DialogPrefab is EntranceDialog ||
                menu.BuildingDialogs.Any(d => d.Id == b.Definition.DialogId && d.Dialog is EntranceDialog));
            if (entrance == null) { Finish("Unsupported", "Town dungeon has no discoverable entrance building."); return; }
            target = entrance.TilemapPosition;
            if (player.ControllingTownAlly.TilemapPosition == target)
            {
                if (Common.Instance.Travel.EnterTownDungeon(town, interior.Id)) Log("Enter " + interior.Id);
                return;
            }
        }
        else target = town.GetComponent<CampaignTownControls>().Exit;
        var manager = FindFirstObjectByType<TownMenuManager>();
        if (manager.DialogStack.Count > 0) { manager.DialogStack.Peek().CloseDialog(); return; }
        var from = player.ControllingTownAlly.TilemapPosition;
        if (from == target) { Common.Instance.Travel.ExitTown(town); return; }
        bool Blocked(Vector3Int cell) => cell != target && (town.TownBuildings.Any(b => b.TilemapPosition == cell) ||
            town.TownAllies.Any(a => a.TilemapPosition == cell));
        AStar.Node[,] TownGrid()
        {
            var grid = player.WalkableMap.GetAStarGrid();
            foreach (var node in grid) if (Blocked(new Vector3Int(node.X,node.Y))) node.IsWalkable = false;
            return grid;
        }
        var next = townRoute.Next(from,target,TownGrid,(a,b) => player.WalkableMap.CanWalkTo(a,b) && !Blocked(b),DiagonalMovement.AllowCornerCutting);
        if (next == null) { Finish("Stalled", "No walkable town path to " + target); return; }
        player.ControllingTownAlly.SetFacing(Character.GetFacing(next.Value - from));
        player.SetAction(new TownMovement(player, from, next.Value)); Log("Town move " + next);
    }

    private static float EquipmentScore(EquipmentItemDefinition item) => item.StatModification.Strength + item.StatModification.Defense;
    private static bool IsUpgrade(TownAlly ally, EquipmentItemDefinition item)
    {
        var equipped = ally.Equipment.GetEquippedItems().OfType<EquipableInventoryItem>().Where(e =>
            e.EquipmentSlot == item.EquipmentSlot || (item.EquipmentSlot == EquipmentSlot.TwoHand && e.EquipmentSlot != EquipmentSlot.Accessory) ||
            (item.EquipmentSlot != EquipmentSlot.Accessory && e.EquipmentSlot == EquipmentSlot.TwoHand)).ToArray();
        return equipped.Length == 0 || EquipmentScore(item) > equipped.Sum(e => EquipmentScore(e.EquipmentItemDefinition));
    }
    private static void EquipParty(Town town)
    {
        foreach (var ally in town.TownPlayer.RecruitedAllies)
            foreach (var item in town.TownPlayer.Inventory.OfType<EquipableInventoryItem>().ToArray())
                if (IsUpgrade(ally,item.EquipmentItemDefinition)) town.Services.ToggleEquipment(ally,item);
    }
    private static bool WantsOffer(Town town, TownShopOffer offer)
    {
        if (offer.Price > town.TownPlayer.Gold) return false;
        if (offer.Item is EquipmentItemDefinition equipment)
            return town.TownPlayer.RecruitedAllies.Any(a => IsUpgrade(a,equipment));
        return offer.Item.ItemEffectDefinition is ModifyStatsItemEffectDefinition effect &&
            (effect.VitalModification.Hp > 0 || effect.VitalModification.Hunger > 0) &&
            town.TownPlayer.Inventory.Count(i => i.ItemName == offer.Item.ItemName) < 3;
    }

    private string PartyKey(CampaignContext c, IEnumerable<string> ids) => c.Completed.Count + "/" + c.Claimed.Count + "/" + c.Opened.Count + ":" + string.Join(",", ids.OrderBy(x => x));
    private static List<string[]> PartyChoices(CampaignContext c)
    {
        var ids = c.Roster.OrderBy(x => x).ToArray();
        var choices = new List<string[]> { Array.Empty<string>() };
        foreach (var id in ids)
            choices.AddRange(choices.Where(s => s.Length < 3).Select(s => s.Concat(new[] { id }).ToArray()).ToArray());
        return choices;
    }
    private void ChooseParty(Town town, CampaignContext c)
    {
        var choice = PartyChoices(c).Where(s => !triedParties.Contains(PartyKey(c,s))).OrderByDescending(s =>
        {
            var held = c.Permanent.Union(CapabilitySet.From(c.Campaign.Companions.Where(a => s.Contains(a.Id)).Select(a => a.Capability)));
            return c.Campaign.Routes.Count(r => !c.Resolved.Contains(r.Id) && r.Requirement.IsSatisfiedBy(held)) * 10 + s.Length;
        }).FirstOrDefault();
        if (choice == null) return;
        town.WriteSaveData();
        if (c.SetParty(choice)) { triedParties.Add(PartyKey(c, choice)); town.RefreshCampaignParty(); }
    }

    private void WorldTick(OverworldScene world, CampaignContext c)
    {
        if (pathWorld != world) { pathWorld = world; worldSteps.Clear(); }
        if (world.OpenGate() || world.OpenShortcut()) { worldSteps.Clear(); Log("Open gate or shortcut"); return; }
        var here = c.Location;
        bool Objective(CampaignLocation l) => IsDungeon(l) ? !c.Completed.Contains(l.Id) :
            (l.Kind == LocationKind.Town && (!visited.Contains(l.Id) || c.Campaign.Locations.Any(d => d.ParentTownId == l.Id && !c.Completed.Contains(d.Id)))) ||
            c.Campaign.Sources.Any(s => s.LocationId == l.Id && !c.Claimed.Contains(s.Id) && s.Prerequisites.IsSatisfiedBy(c.Held)) ||
            c.Campaign.Routes.Any(r => r.KeyLocationId == l.Id && !c.Gates.HasKey(r) && r.KeyCondition == KeyAcquisition.AtLocation) ||
            (Options.ExploreAll && !visited.Contains(l.Id));
        if (here != null)
        {
            bool wanted = Objective(here);
            visited.Add(here.Id);
            if (wanted) { worldSteps.Clear(); world.ClaimRewards(); Log("Interact " + here.Id); return; }
        }
        if (worldSteps.Count > 0)
        {
            if (MoveWorldStep(world,worldSteps.Peek())) { worldSteps.Dequeue(); return; }
            worldSteps.Clear();
        }
        var goals = c.Campaign.Locations.Where(l => l.ParentTownId == null && Objective(l)).Select(l => c.Grid.Locations[l.Id]).ToHashSet();
        foreach (var gate in c.Grid.Locks)
        {
            var route = c.Campaign.Routes.First(r => r.Id == gate.RouteId);
            if (!c.Gates.NeedsOpening(route) || !(route.ShortcutKind == ShortcutKind.Keyed ? c.Gates.HasKey(route) : route.CanTraverse(c.Held,c.Resolved))) continue;
            foreach (var cell in gate.Cells)
                foreach (var offset in Steps.Take(4)) goals.Add(new GridPoint(cell.X + offset.x, cell.Y + offset.y));
        }
        foreach (var route in c.Campaign.Routes.Where(r => r.UnlockingEndpoint != null && !c.Resolved.Contains(r.Id)))
            goals.Add(c.Grid.Locations[route.UnlockingEndpoint]);
        goals.Remove(c.Position);
        var path = WorldPath(world, goals);
        if (path == null)
        {
            if (PartyChoices(c).All(s => triedParties.Contains(PartyKey(c,s))))
            { Finish("Stalled","No reachable objective remains, and every available party loadout has been tried at this progression state."); return; }
            // Return to a real town to change capability companions when this loadout is exhausted.
            var townGoals = c.Campaign.Locations.Where(l => l.Kind == LocationKind.Town).Select(l => c.Grid.Locations[l.Id]).ToHashSet();
            if (here?.Kind == LocationKind.Town) { Common.Instance.Travel.EnterLocation(); Log("Return to town for party change"); return; }
            path = WorldPath(world, townGoals);
        }
        if (path == null || path.Count == 0) { Finish("Stalled", "No reachable objective or town with current capabilities. Inspect campaign and action log."); return; }
        foreach (var step in path) worldSteps.Enqueue(step);
        if (MoveWorldStep(world,worldSteps.Peek())) worldSteps.Dequeue();
    }

    private bool MoveWorldStep(OverworldScene world, GridPoint next)
    {
        var c = world.Context;
        var warp = c.Grid.WarpsAt(c.Position).FirstOrDefault(r => c.Grid.TryWarp(r.Id,c.Position,c.Held,c.Resolved,out var to) && to.Equals(next));
        if (warp != null && world.Warp(warp.Id)) { Log("Warp " + warp.Id); return true; }
        if (world.TryMove(next.X - c.Position.X,next.Y - c.Position.Y)) { Log("Overworld move " + next); return true; }
        return false;
    }

    private List<GridPoint> WorldPath(OverworldScene world, HashSet<GridPoint> goals)
    {
        var c = world.Context;
        var queue = new Queue<GridPoint>(); var previous = new Dictionary<GridPoint,GridPoint>();
        queue.Enqueue(c.Position); previous[c.Position] = c.Position;
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (goals.Contains(p))
            {
                var path = new List<GridPoint>();
                while (!p.Equals(c.Position)) { path.Add(p); p = previous[p]; }
                path.Reverse(); return path;
            }
            var adjacent = Steps.Select(s => new GridPoint(p.X+s.x,p.Y+s.y)).Where(n => world.CanStep(p,n)).ToList();
            foreach (var warp in c.Grid.WarpsAt(p))
                if (c.Grid.TryWarp(warp.Id,p,c.Held,c.Resolved,out var to)) adjacent.Add(to);
            foreach (var n in adjacent) if (!previous.ContainsKey(n)) { previous[n] = p; queue.Enqueue(n); }
        }
        return null;
    }

    private void DungeonTick(Game game)
    {
        if (!game.IsReady) return;
        if (game.GameOverScreen.gameObject.activeSelf || !game.Allies.Any(a => a != null && a.Vitals.HP > 0))
        { Finish("Defeat", "Party defeated by normal gameplay. No retry or difficulty adjustment applied."); return; }
        if (game.TurnManager.IsProcessingTurn || game.NewFloorMessage.gameObject.activeSelf) return;
        if (MenuManager.Instance.CurrentDialog is StairConfirm stairsPrompt) { stairsPrompt.YesClicked(); Log("Confirm stairs/exit"); return; }
        if (MenuManager.Instance.Opened) { Finish("Unsupported", "Unhandled dungeon dialog: " + MenuManager.Instance.CurrentDialog.GetType().Name); return; }
        var player = game.PlayerController; var ally = player.ControlledAlly; var dungeon = game.CurrentDungeon;
        if (ally == null) return;
        if (Options.DebugPlaythrough && Options.InfiniteResources) player.Gold = 1000000;
        string floor = Common.Instance.CampaignContext.State.PendingDungeon + "/" + player.Floor;
        if (floorIdentity != floor)
        {
            floorIdentity = floor; discovered.Clear(); repetitions.Clear(); dungeonRoute.Clear(); approachRoute.Clear(); combatObjective = null;
            // Deliberate demo assistance: route directly to objectives using the entire generated floor.
            for (int x=0;x<dungeon.dungeonWidth;x++) for (int y=0;y<dungeon.dungeonHeight;y++)
                if (dungeon.IsWalkable(new Vector3Int(x,y))) discovered.Add(new Vector3Int(x,y));
            Log("Floor " + floor);
        }
        var supplies = player.Inventory.InventoryItems.Where(i => i.ItemDefinition.ItemEffectDefinition is ModifyStatsItemEffectDefinition effect &&
            ((ally.Vitals.HP < ally.FinalStats.HPMax * .5f && effect.VitalModification.Hp > 0) ||
             (ally.Vitals.Hunger < ally.FinalStats.HungerMax * .25f && effect.VitalModification.Hunger > 0))).ToArray();
        foreach (var item in supplies)
        {
            var use = new UseInventoryItemAction(player.Inventory,ally,item);
            if (use.IsValid(ally)) { Report.ItemsUsed++; Act(ally,use,"Use " + item.ItemName); return; }
        }
        var attack = new AllyAttackPolicy(game,ally,0);
        if (attack.ShouldRun()) { var action = attack.GetActions().FirstOrDefault(a => a.IsValid(ally)); if (action != null) { Act(ally,action,"Attack"); return; } }
        var stairs = dungeon.Interactables.OfType<Stairs>().FirstOrDefault();
        if (stairs == null) { Finish("Unsupported","Ready dungeon has no stairs objective."); return; }
        if (stairs != null && stairs.Position == ally.TilemapPosition)
        {
            // The regular prompt normally handles this. Re-enter to trigger it if a status effect interrupted arrival.
            ally.currentInteractable = stairs; ally.MovedThisTurn = true; player.StartTurn(); return;
        }
        bool CanOccupy(Vector3Int cell) => discovered.Contains(cell) && dungeon.IsWalkable(cell) &&
            dungeon.OverlapsAnyOtherCharacter(ally,Character.ToBounds(ally.FootPrint,cell),true) == null;
        bool CanStep(Vector3Int a, Vector3Int b) => dungeon.CanWalkTo(a,b) && CanOccupy(b);
        AStar.Node[,] DungeonGrid()
        {
            // Reuse the character positioning grid and mask hostile footprints.
            var grid = Character.GetAStarGrid();
            foreach (var node in grid) node.IsWalkable &= CanOccupy(new Vector3Int(node.X,node.Y));
            return grid;
        }
        Vector3Int? RouteTo(Vector3Int target) => dungeonRoute.Next(ally.TilemapPosition,target,DungeonGrid,CanStep);
        Vector3Int? next = RouteTo(stairs.Position);
        if (next.HasValue) { approachRoute.Clear(); combatObjective = null; }
        else if (combatObjective.HasValue && combatObjective.Value != ally.TilemapPosition && CanOccupy(combatObjective.Value))
            next = approachRoute.Next(ally.TilemapPosition,combatObjective.Value,DungeonGrid,CanStep);
        // A creature can seal a narrow corridor on the route to the stairs.
        // Approach its nearest footprint edge so normal sight and attack rules can handle it.
        if (next == null)
            foreach (var target in discovered.Where(p => p != ally.TilemapPosition && CanOccupy(p) && game.Enemies.Any(e => e != null && e.Vitals.HP > 0 &&
                Steps.Any(s => discovered.Contains(p+s) && e.ToBounds().Contains(p+s)))).OrderBy(p => (p-ally.TilemapPosition).sqrMagnitude))
                if ((next = approachRoute.Next(ally.TilemapPosition,target,DungeonGrid,CanStep)).HasValue) { combatObjective = target; break; }
        if (next == null)
        {
            Act(ally,new WaitAction(),"Wait for blocked objective path"); return;
        }
        var other = game.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == next.Value);
        ally.SetFacingByTargetPosition(next.Value);
        GameAction movement = other == null ? new MovementAction(ally,ally.TilemapPosition,next.Value) : new SwapAllyPositionAction(ally,other);
        Act(ally,movement,"Dungeon move " + next);
    }

    private void Act(Ally ally, GameAction action, string description)
    {
        if (!action.IsValid(ally)) { Finish("Unsupported", "Policy selected invalid action: " + description); return; }
        Report.DungeonTurns++; Log(description); if (Running) ally.SetAction(action);
    }

    [Serializable] private sealed class Event { public double Seconds; public int Action, Floor, HP, SP, Hunger, Gold; public string Scene, Location, Message, Position; }
    private void Log(string message)
    {
        Status = message; Report.Actions++;
        var c = Common.Instance?.CampaignContext; var game = FindFirstObjectByType<Game>();
        var town = FindFirstObjectByType<Town>(); var ally = game?.PlayerController?.ControlledAlly;
        string position = ally != null ? ally.TilemapPosition.ToString() : town?.TownPlayer?.ControllingTownAlly != null ? town.TownPlayer.ControllingTownAlly.TilemapPosition.ToString() : c != null && c.State.Scene == "Overworld" ? c.Position.ToString() : "";
        actions.WriteLine(JsonUtility.ToJson(new Event { Seconds = Time.realtimeSinceStartupAsDouble-started-pausedSeconds, Action = Report.Actions,
            Scene = SceneManager.GetActiveScene().name, Location = c?.State.PendingDungeon.Length > 0 ? c.State.PendingDungeon : c?.Location?.Id,
            Floor = game != null ? game.PlayerController.Floor : 0, HP = ally != null ? ally.Vitals.HP : 0, SP = ally != null ? ally.Vitals.SP : 0,
            Hunger = ally != null ? ally.Vitals.Hunger : 0, Gold = game != null ? game.PlayerController.Gold : town != null ? town.TownPlayer.Gold : 0,
            Position = position, Message = message }));
        string combat = game == null ? "" : string.Join(",",game.Enemies.Where(e => e != null).Select(e => e.GetInstanceID() + ":" + e.Vitals.HP));
        string signature = floorIdentity + "/" + progressSignature + "/" + position + "/" + message + "/" + combat;
        repetitions.TryGetValue(signature,out var count); repetitions[signature] = count+1;
        if (count > 30) { Finish("Stalled", "Repeated action/state loop: " + message); return; }
        lastProgress = Time.realtimeSinceStartupAsDouble;
        if (Report.Actions % 50 == 0) { Capture(); WriteReport(); }
    }

    public void SetSpeed(float speed)
    {
        if (!Running || float.IsNaN(speed) || float.IsInfinity(speed)) return;
        Options.Speed = Mathf.Clamp(speed, .5f, 8f);
        if (!Paused) Time.timeScale = Options.Speed;
        nextTick = Time.unscaledTime;
        WriteReport();
    }

    public void SetPaused(bool paused)
    {
        if (!Running || Paused == paused) return;
        Paused = paused;
        if (paused) pausedAt = Time.realtimeSinceStartupAsDouble;
        else { var duration = Time.realtimeSinceStartupAsDouble-pausedAt; pausedSeconds += duration; lastProgress += duration; }
        Time.timeScale = paused ? 0 : Options.Speed;
    }
    public void Stop() => Finish("Stopped", "Stopped manually; no result inferred.");

    public void ExitDemo()
    {
        if (exiting) return;
        exiting = true; ReturnPromptOpen = false;
        Stop();
        // Dispose the isolated store only after all demo scene callbacks have been unloaded.
        StartCoroutine(ExitRoutine());
    }
    public void RequestReturn()
    {
        if (!appSession || exiting || ReturnPromptOpen) return;
        wasPausedBeforePrompt = Paused;
        SetPaused(true); ReturnPromptOpen = true;
        acceptInputAfter = Time.unscaledTime + .2f;
    }
    public void ConfirmReturn(bool confirm)
    {
        if (!ReturnPromptOpen) return;
        ReturnPromptOpen = false;
        if (confirm) ExitDemo();
        else { SetPaused(wasPausedBeforePrompt); acceptInputAfter = Time.unscaledTime + .5f; }
    }
    private Rect PlaybackPanel => new Rect(Mathf.Max(0, Screen.width - 340), 16, Mathf.Min(324, Screen.width), 340);
    private bool OverPlaybackPanel(Vector2 position) => PlaybackPanel.Contains(new Vector2(position.x, Screen.height - position.y));

    private bool UserInput()
    {
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
        var mouse = Mouse.current;
        // Pointer motion must remain free so viewers can reach the playback controls.
        if (mouse != null && !OverPlaybackPanel(mouse.position.ReadValue()) &&
            (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0)) return true;
        if (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true &&
            !OverPlaybackPanel(Touchscreen.current.primaryTouch.position.ReadValue())) return true;
        foreach (var pad in Gamepad.all)
            if (pad.allControls.OfType<ButtonControl>().Any(b => b.wasPressedThisFrame) ||
                pad.leftStick.ReadValue().sqrMagnitude > .2f || pad.rightStick.ReadValue().sqrMagnitude > .2f) return true;
        return false;
    }
    private IEnumerator ExitRoutine()
    {
        Time.timeScale = originalTimeScale;
        var common = Common.Instance;
        common.ScreenTransition.HoldClosed(); // Cancel a queued transition callback before restoring the original campaign.
        CampaignParty.ClearLiveParty(common);
        common.CampaignContext = originalContext;
        common.GameSaveData = originalSave;
        common.CurrentTownConfiguration = originalTownConfiguration;
        common.Travel.SceneReady();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        common.ScreenTransition.DoOpen();
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (Options == null) return;
        if (ReturnPromptOpen)
        {
            GUI.depth = -100;
            GUILayout.BeginArea(new Rect((Screen.width-360)/2f,(Screen.height-180)/2f,360,180),GUI.skin.box);
            GUILayout.Label("Return to main menu?");
            GUILayout.Label("Your saved game is unchanged.");
            if (GUILayout.Button("Return to main menu (Enter / A)")) ConfirmReturn(true);
            if (GUILayout.Button("Keep watching (Esc / B)")) ConfirmReturn(false);
            GUILayout.EndArea(); return;
        }
        GUILayout.BeginArea(PlaybackPanel, GUI.skin.box);
        GUILayout.Label(Options.DebugPlaythrough ? "AUTOPLAY — DEBUG PLAYTHROUGH" : "AUTOPLAY — NORMAL PLAYTHROUGH");
        if (Options.DebugPlaythrough) GUILayout.Label("Godmode + infinite strength: " + Options.Godmode + " | Infinite resources: " + Options.InfiniteResources);
        GUILayout.Label(Status.Length > 220 ? Status.Substring(0,220) + "…" : Status);
        GUILayout.Label("Actions: " + Report.Actions + " | Dungeon turns: " + Report.DungeonTurns);
        if (Running)
        {
            GUILayout.Label("Playback: " + Options.Speed.ToString("0.#") + "x" + (Paused ? " (paused)" : ""));
            GUILayout.BeginHorizontal();
            foreach (float speed in new[] { .5f, 1f, 2f, 4f, 8f })
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !Mathf.Approximately(Options.Speed, speed);
                if (GUILayout.Button(speed.ToString("0.#") + "x")) SetSpeed(speed);
                GUI.enabled = previousEnabled;
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(Paused ? "Resume" : "Pause")) SetPaused(!Paused);
        }
        if (!appSession && Running && GUILayout.Button("Stop and report")) Stop();
        if (appSession && GUILayout.Button("Return to main menu")) RequestReturn();
        if (appSession) GUILayout.Label("Input outside playback controls: return to main menu");
        if (!Running) GUILayout.Label(appSession ? "Playthrough report saved." : "Report saved: " + DirectoryPath);
        GUILayout.EndArea();
    }

    private void Capture()
    {
        var common = FindFirstObjectByType<Common>(); var game = FindFirstObjectByType<Game>();
        var c = common?.CampaignContext;
        Report.Seconds = Time.realtimeSinceStartupAsDouble-started-pausedSeconds-(Paused ? Time.realtimeSinceStartupAsDouble-pausedAt : 0);
        Report.Scene = SceneManager.GetActiveScene().name; Report.Campaign = c?.Capture();
        Report.Location = c?.State.PendingDungeon.Length > 0 ? c.State.PendingDungeon : c?.Location?.Id;
        Report.Visited = visited.OrderBy(x => x).ToArray();
        if (common?.GameSaveData != null) SaveSystem.SaveData(common.GameSaveData);
        if (game == null || !game.IsReady)
        {
            var town = FindFirstObjectByType<Town>();
            Report.Floor = 0; Report.Party.Clear(); Report.Enemies.Clear();
            Report.Gold = town != null ? town.TownPlayer.Gold : common?.GameSaveData?.TownSaveData.Gold ?? 0;
            Report.Inventory = town != null ? town.TownPlayer.Inventory.Select(ItemLabel).ToArray() : Array.Empty<string>();
            if (town != null) Report.Party = town.TownPlayer.RecruitedAllies.Select(a => new AutoplayActor {
                Id = a.Id, Name = a.Name, X = a.TilemapPosition.x, Y = a.TilemapPosition.y,
                Equipment = a.Equipment.GetEquippedItems().Select(ItemLabel).ToArray(), Skills = a.Skills.ToArray() }).ToList();
            return;
        }
        Report.Floor = game.PlayerController.Floor; Report.Gold = game.PlayerController.Gold;
        Report.Inventory = game.PlayerController.Inventory.InventoryItems.Select(ItemLabel).ToArray();
        Report.Party = game.Allies.Concat(game.DeadUnits.OfType<Ally>()).Where(a => a != null).Distinct().Select(a => new AutoplayActor {
            Id = a.TownAllyId, Name = common.GameSaveData.Roster.FirstOrDefault(r => r.AllyId == a.TownAllyId)?.AllyName ?? a.name,
            HP = a.Vitals.HP, MaxHP = a.FinalStats.HPMax, SP = a.Vitals.SP, Hunger = a.Vitals.Hunger,
            Level = a.Vitals.Level, Strength = a.FinalStats.Strength, Defense = a.FinalStats.Defense, X = a.TilemapPosition.x, Y = a.TilemapPosition.y,
            Equipment = a.Equipment.GetEquippedItems().Select(i => i.ItemName).ToArray(), Skills = a.Skills.Select(s => s.SkillName).ToArray() }).ToList();
        Report.Enemies = game.Enemies.Where(e => e != null).Select(e => new AutoplayActor { Name = e.name, HP = e.Vitals.HP,
            MaxHP = e.FinalStats.HPMax, Level = e.Vitals.Level, Strength = e.FinalStats.Strength, Defense = e.FinalStats.Defense, X = e.TilemapPosition.x, Y = e.TilemapPosition.y }).ToList();
        if (!Running)
        {
            var dungeon = game.CurrentDungeon; var walkable = new List<Vector3Int>();
            for (int x=0;x<dungeon.dungeonWidth;x++) for (int y=0;y<dungeon.dungeonHeight;y++)
                if (dungeon.IsWalkable(new Vector3Int(x,y))) walkable.Add(new Vector3Int(x,y));
            Report.WalkableTiles = walkable.ToArray(); Report.DiscoveredTiles = discovered.ToArray();
            Report.Stairs = dungeon.Interactables.OfType<Stairs>().Select(s => s.Position).ToArray();
        }
    }
    private static string ItemLabel(InventoryItem item) => item.ItemName + (item.StackStock.HasValue ? " x" + item.StackStock : "");
    private void WriteReport() => File.WriteAllText(Path.Combine(DirectoryPath,"report.json"),JsonUtility.ToJson(Report,true));
    public void Finish(string outcome, string reason)
    {
        if (!Running) return;
        if (Report.ValidationOnly) Report.EligibleForBalance = false;
        Report.Outcome = outcome; Report.Reason = reason; Report.EndedUtc = DateTime.UtcNow.ToString("O");
        Status = outcome + ": " + reason;
        try
        {
            try { Capture(); } catch (Exception snapshotError) { Report.Reason += "\nSnapshot incomplete: " + snapshotError.Message; }
            WriteReport(); ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath,"final.png"));
        }
        finally { Time.timeScale = 0; actions?.Flush(); }
        // Keep the isolated store installed until Play Mode exits, so inspecting the failed run cannot overwrite player data.
    }
    private void OnDestroy()
    {
        if (disposed || Options == null) return;
        disposed = true;
        try { if (Running) Finish("Stopped","Play Mode ended."); }
        finally { Application.logMessageReceived -= OnLog; actions?.Dispose(); saveScope?.Dispose(); Active = null; Time.timeScale = originalTimeScale; UnityEngine.Random.state = originalRandom; }
    }

    private sealed class AutoplayStore : ISaveStore
    {
        private string json; private readonly string path;
        public AutoplayStore(string directory) { path = Path.Combine(directory,"save.json"); }
        public string Read() => json;
        public void Write(string value) { json = value; File.WriteAllText(path,value); }
        public void Clear() { json = null; }
    }
}
