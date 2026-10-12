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

public enum AutoplayMode { Campaign = 0, FloorLootSweep = 1, Finale = 2 }

[Serializable]
public sealed class AutoplayOptions
{
    public AutoplayMode Mode;
    public string ResumeManifestPath;
    public string ReportRoot;
    public int ActionLimit = 100000;
    public int Seed = 42;
    public int RunCount = 1;
    public bool DebugCoverage;
    public bool DebugPlaythrough, Godmode = true, InfiniteResources = true;
    public bool InfiniteStrength = true;
    public bool NoAnimations;
    public static AutoplayOptions FinaleDebug() => new() { Mode = AutoplayMode.Finale, Seed = 42, RunCount = 1,
        Speed = 4, DebugPlaythrough = true, Godmode = true, InfiniteResources = true, InfiniteStrength = false, DebugCoverage = false };
    public bool ExploreAll;
    public float Speed = 4, TimeLimitMinutes = 60, StallSeconds = 120;

    public static AutoplayOptions LootSweep() => new() { Mode = AutoplayMode.FloorLootSweep, RunCount = 10000,
        Seed = 42, Speed = 32, DebugPlaythrough = true, Godmode = true, InfiniteResources = false,
        DebugCoverage = false, TimeLimitMinutes = 60, StallSeconds = 300 };

    public bool Validate(out string reason)
    {
        reason = !Enum.IsDefined(typeof(AutoplayMode), Mode) ? "Unknown autoplay mode." :
            ActionLimit < 1 ? "Action limit must be positive." : RunCount < 1 ? "Run count must be at least 1." :
            (long)Seed + RunCount - 1 > int.MaxValue ? "The last seed would exceed 2147483647." :
            float.IsNaN(Speed) || float.IsInfinity(Speed) || Speed < .5f || Speed > 32 ? "Speed must be between 0.5 and 32." :
            float.IsNaN(TimeLimitMinutes) || float.IsInfinity(TimeLimitMinutes) || TimeLimitMinutes <= 0 ? "Time limit must be positive." :
            float.IsNaN(StallSeconds) || float.IsInfinity(StallSeconds) || StallSeconds <= 0 ? "Stall timeout must be positive." : null;
        return reason == null;
    }
    public int SeedForRun(int index)
    {
        if (index < 0 || index >= RunCount) throw new ArgumentOutOfRangeException(nameof(index));
        return checked(Seed + index);
    }
}

[Serializable]
public sealed class AutoplayReport
{
    public string ManifestId, CaseId, ContentId, Profile, GenerationOptions, GeneratedFloor;
    public int GeneratedSeed, GeneratedWidth, GeneratedHeight;
    public bool GeneratedThrone;
    public int ExpectedLoot, CollectedLoot, PlacementCorrections, RecoveryEvents;
    public List<AutoplayLootRecord> Loot = new();
    public List<string> UnresolvedLoot = new(), PlacementIssues = new();
    public string Outcome = "Running", Reason, StartedUtc, EndedUtc, GameVersion, BuildId, UnityVersion;
    public string Policy = "astar-objectives-v5-mimic-finale", Observation = "Full campaign graph, dungeon objectives and finale destinations (omniscient navigation)";
    public string RunId;
    public int Seed, RunNumber, IssueCount, WarningCount, ErrorCount;
    public bool ArtifactsComplete;
    public List<AutoplayTransaction> Purchases = new(), Training = new();
    public List<AutoplayAbilityCoverage> Abilities = new();
    public string[] UnexercisedAbilities;
    public AutoplayOptions Options;
    public bool EligibleForBalance;
    public bool InfiniteStrength;
    public bool ValidationOnly;
    public double Seconds;
    public int Actions, DungeonTurns, DamageTaken, HealingReceived, ItemsUsed;
    public string Location, Scene;
    public int Floor, Gold;
    public CampaignSnapshot Campaign;
    public AutoplayFinaleReport Finale;
    public string[] Visited, Inventory;
    public List<AutoplayActor> Party = new();
    public List<AutoplayActor> Enemies = new();
    public List<AutoplaySkillUse> SkillUses = new();
    public Vector3Int[] WalkableTiles, DiscoveredTiles, Stairs;
}

[Serializable]
public sealed class AutoplayActor
{
    public string Name, Id;
    public int HP, MaxHP, SP, Hunger, Level, Strength, Defense, X, Y;
    public string[] Equipment, Skills;
    public string Class;
}

[Serializable]
public sealed class AutoplaySkillUse
{
    public string Actor, ActorId, Skill, Evaluator;
    public int Casts, Selected, Started, Interrupted;
}

/// <summary>Opt-in isolated session shared by the app's Watch Demo and the manual editor launcher.</summary>
public sealed partial class AutoplayRunner : MonoBehaviour
{
    public const string PendingKey = "EternalEnigma.Autoplay.Pending";
    public static AutoplayRunner Active { get; private set; }
    public static bool BlocksPlayerInput => Active != null && !Active.PlayerControlled;
    internal static bool SuppressesAnimations => BlocksPlayerInput &&
        (Active.IsLootSweep || Active.Options?.NoAnimations == true);
    public bool PlayerControlled { get; private set; }
    public bool PanelVisible { get; private set; } = true;
    public bool TakingControl => takingControl;
    public bool AppSession => appSession;
    public bool CanTakeControl => startedCampaign && !unloadingRun && (!IsLootSweep || Game.Instance?.IsReady == true);
    public bool ShowPlaybackUI => Options != null && !PlayerControlled && !exiting;
    private AutoplayPanel playbackUI;
    private bool takingControl;
    public AutoplayOptions Options { get; private set; }
    public AutoplayReport Report { get; private set; }
    public string DirectoryPath { get; private set; }
    public bool Paused { get; private set; }
    public bool Running => Report?.Outcome == "Running";
    public string Status { get; private set; } = "Starting";
    private IDisposable saveScope;
    private AutoplayStore store;
    private StreamWriter actions, issues;
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
    private float originalTimeScale, originalFixedDeltaTime;
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
        StartBatch(JsonUtility.FromJson<AutoplayOptions>(json));
    }
#endif

    internal static void StartBatch(AutoplayOptions options)
    {
        if (ChaosSessionLock.Held) throw new InvalidOperationException("An input-only chaos session is active.");
        if (Active != null) throw new InvalidOperationException("An autoplay session is already active.");
        var runner = new GameObject("Manual playthrough").AddComponent<AutoplayRunner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.Initialize(options);
    }

    public static void WatchDemo(AutoplayOptions options)
    {
        if (ChaosSessionLock.Held) return;
        if (options == null || !options.Validate(out _)) throw new ArgumentException("Invalid autoplay options.");
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
        if (!options.Validate(out var reason)) throw new ArgumentException(reason);
        Active = this; Options = JsonUtility.FromJson<AutoplayOptions>(JsonUtility.ToJson(options));
        HoldSessionReload();
        acceptInputAfter = Time.unscaledTime + .75f;
        originalTimeScale = Time.timeScale; originalRandom = UnityEngine.Random.state;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        Time.fixedDeltaTime = originalFixedDeltaTime * Options.Speed;
        string root = !string.IsNullOrWhiteSpace(Options.ReportRoot) ? Options.ReportRoot :
            Application.isEditor ? "Logs" : Application.persistentDataPath;
        BatchDirectoryPath = Path.GetFullPath(Path.Combine(root, IsFinaleDebug ? "FinaleDebug" : "Playthroughs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
        Batch = new AutoplayBatch { Mode = Options.Mode, StartedUtc = DateTime.UtcNow.ToString("O"), StartingSeed = Options.Seed, RequestedRuns = Options.RunCount };
        store = new AutoplayStore();
        saveScope = SaveSystem.UseStore(store);
        Application.logMessageReceived += OnLog;
        try { InitializeSweep(); if (!batchCanceled) BeginRun(); }
        catch (Exception error) { FailSession("Unable to initialize run", error); }
        var panel = Resources.Load<AutoplayPanel>("UI/AutoplayPanel");
        if (panel != null) { playbackUI = Instantiate(panel, transform); playbackUI.Bind(this); }
    }

    private IEnumerator Start()
    {
        if (Options == null) yield break;
        while (Common.Instance == null) yield return null;
        if (!appSession)
        {
            originalSave = Common.Instance.GameSaveData;
            originalContext = Common.Instance.CampaignContext;
            originalTownConfiguration = Common.Instance.CurrentTownConfiguration;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }
    }

    public static bool Protects(Vitals vitals) => Active != null && Active.Running && Active.Options.DebugPlaythrough &&
        Active.Options.Godmode && IsPartyVitals(vitals);
    public static bool Replenishes(Vitals vitals) => Active != null && Active.Running && Active.Options.DebugPlaythrough &&
        Active.Options.InfiniteResources && IsPartyVitals(vitals);
    private static bool IsPartyVitals(Vitals vitals) => Game.Instance != null && Game.Instance.Allies != null &&
        Game.Instance.Allies.Any(a => a != null && (ReferenceEquals(a.Vitals, vitals) || ReferenceEquals(a.DisplayedVitals, vitals)));
    public static bool InfiniteResourcesFor(Character actor) => Active != null && Active.Running &&
        Active.Options.DebugPlaythrough && Active.Options.InfiniteResources && actor is Ally;
    public static bool InfiniteStrengthFor(Character actor) => Active != null && Active.Running &&
        Active.Options.DebugPlaythrough && Active.Options.InfiniteStrength && actor is Ally;
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
        if (PlayerControlled || disposed || Report == null) return;
        if (type == LogType.Warning || type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Issue(type.ToString(), message, stack);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            pendingError ??= message + "\n" + stack;
    }

    private void Update()
    {
        // Unity serializes MonoBehaviour fields across script reload, but cannot restore
        // file writers, save scopes or coroutines. A reloaded attempt must be resumed
        // from its manifest, never continued with a partially reconstructed session.
        if (store == null) { enabled = false; return; }
        if (PlayerControlled || takingControl || exiting) return;
        if (!ReturnPromptOpen && Keyboard.current?.f8Key.wasPressedThisFrame == true)
        { SetPanelVisible(!PanelVisible); return; }
        if (!exiting)
        {
            if (ReturnPromptOpen)
            {
                if (Time.unscaledTime > acceptInputAfter)
                {
                    if (Keyboard.current?.enterKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true) ConfirmReturn(true);
                    else if (Keyboard.current?.tKey.wasPressedThisFrame == true || Gamepad.current?.buttonWest.wasPressedThisFrame == true) TakeControl();
                    else if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true) ConfirmReturn(false);
                }
                return;
            }
            if (appSession && Time.unscaledTime > acceptInputAfter && UserInput()) { RequestReturn(); return; }
        }
        if (pendingReportingError != null) { FailSession("Reporting failed", new IOException(pendingReportingError)); pendingReportingError = null; return; }
        if (pendingError != null && !Running && (Finalizing || Advancing)) { FailSession("Game error during run transition", new Exception(pendingError),"GameError"); pendingError = null; return; }
        if (Running && pendingError != null) { Finish("GameError", pendingError); return; }
        if (!Running || Paused) return;
        var now = Time.realtimeSinceStartupAsDouble;
        if (now - started - pausedSeconds > Options.TimeLimitMinutes * 60) { Finish(IsLootSweep ? "Failed" : "TimeLimit", "Active time limit reached."); return; }
        // A validated five-map preflight and terrain builds can span several frames or block one frame.
        // The overall time limit still bounds generation; the combat stall clock starts when play is ready.
        if (Common.Instance?.Travel.IsTransitioning == true || Game.Instance is { IsReady: false }) lastProgress = now;
        if (now - lastProgress > Options.StallSeconds) { Finish(IsLootSweep ? "Failed" : "Stalled", "No meaningful progress: " + Status); return; }
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + .03f / Options.Speed;
        try
        {
            // Commands may finish synchronously without animations. Use the same
            // bounded pacing for campaigns and sweeps, always respecting turn state.
            {
                // At accelerated playback, submit another command only after the
                // production turn has completed. Bound work so controls stay responsive.
                double deadline = Time.realtimeSinceStartupAsDouble + (SuppressesAnimations ? .025 : .006);
                for (int i = 0; i < Mathf.CeilToInt(Options.Speed); i++)
                {
                    int before = Report.Actions;
                    Tick();
                    var game = Game.Instance;
                    if (!Running || Paused || pendingError != null || Report.Actions == before || game?.TurnManager.IsProcessingTurn == true ||
                        game?.PlayerController.ControlledAlly?.PendingCast != null || Time.realtimeSinceStartupAsDouble >= deadline) break;
                }
            }
        }
        catch (Exception error) { Finish("AutomationError", error.ToString()); }
    }

    private void Tick()
    {
        var common = Common.Instance;
        if (common == null || common.Travel.IsTransitioning) return;
        if (IsLootSweep) { SweepTick(common); return; }
        if (!startedCampaign)
        {
            var menu = FindFirstObjectByType<MainMenu>();
            if (menu == null) return;
            if (!menu.IsReady) return;
            UnityEngine.Random.InitState(CurrentSeed);
            if (IsFinaleDebug)
            {
                common.GameSaveData = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Dungeon, CurrentSeed);
                common.GameSaveData.IsSandbox = false;
                // Save only the pre-finale fixture inside the already isolated store.
                common.CampaignContext = null;
                SaveSystem.SaveData(common.GameSaveData);
                startedCampaign = true; common.Travel.Continue();
                Log("Direct-start debug finale"); return;
            }
            common.GameSaveData = menu.CreateNewSave(CurrentSeed);
            startedCampaign = true;
            common.Travel.NewCampaign(CurrentSeed);
            Log("New campaign"); return;
        }
        var context = common.CampaignContext;
        if (context == null) { Finish("Unsupported", "Campaign context disappeared."); return; }
        TrackProgress(context);
        if (context.State.Finished && Report.Finale?.FinalExitCompleted == true && common.Travel.HomecomingCompleted &&
            Report.Finale.Floors.Count == 5 && Report.Finale.Floors.All(f => f.Regions.All(r => r.Cleared && r.Remaining == 0)) &&
            (IsFinaleDebug || !Options.ExploreAll || context.Campaign.Locations.All(l =>
            IsDungeon(l) ? context.Completed.Contains(l.Id) : visited.Contains(l.Id))))
        { Finish("Victory", Options.ExploreAll ? "Campaign complete; every generated destination visited and dungeon completed once." : "Campaign Finished state reached."); return; }
        if (context.State.Finished && common.Travel.HomecomingPending) return;
        var game = Game.Instance;
        if (game != null) { DungeonTick(game); return; }
        var town = FindFirstObjectByType<Town>();
        if (town != null) { if (town.IsReady) TownTick(town, context); return; }
        var world = FindFirstObjectByType<OverworldScene>();
        if (world != null && world.IsReady && !world.IsMoving) WorldTick(world, context);
    }

    private void TrackProgress(CampaignContext context)
    {
        string signature = context.State.Scene + "/" + context.State.PendingDungeon + "/" + context.State.LocationId + "/" +
            context.Completed.Count + "/" + context.Claimed.Count + "/" + context.Opened.Count + "/" +
            context.Finale?.CurrentFloor + "/" + context.Finale?.Defeated.Count + "/" + context.Finale?.Unlocked.Count;
        if (signature == progressSignature) return;
        progressSignature = signature; repetitions.Clear(); lastProgress = Time.realtimeSinceStartupAsDouble;
        Capture(); WriteReport();
    }

    private static bool IsDungeon(CampaignLocation location) => location.Encounter != null || location.Kind == LocationKind.StoryDungeon ||
        location.Kind == LocationKind.RepeatableDungeon || location.Kind == LocationKind.FinalDungeon;

    private void TownTick(Town town, CampaignContext context)
    {
        var player = town.TownPlayer;
        if (player.IsBusy || player.ControllingTownAlly == null) return;
        visited.Add(town.Configuration.Id);
        if (Options.DebugPlaythrough && Options.InfiniteResources) player.Gold = 1000000;
        if (preparedTown != town)
        {
            preparedTown = town; shopped.Clear(); failedPurchases.Clear(); failedTraining.Clear(); townRoute.Clear();
            ChooseParty(town, context);
            foreach (var source in context.Campaign.Sources.Where(s => s.LocationId == town.Configuration.Id)) context.Claim(source.Id);
            EquipParty(town);
            Log("Prepare town party");
        }
        var interior = context.Campaign.Locations.FirstOrDefault(l => l.ParentTownId == town.Configuration.Id && !context.Completed.Contains(l.Id));
        Vector3Int target;
        var shop = town.TownBuildings.FirstOrDefault(b => !shopped.Contains(b) && town.Services.Catalog(b.Definition).Any(o => WantsOffer(town,b.Definition,o)));
        var training = NextTraining(town);
        var trainer = training.ally == null ? null : town.TownBuildings.FirstOrDefault(b => IsTrainer(town,b));
        if (shop != null)
        {
            target = ServicePosition(town,shop);
            if (player.ControllingTownAlly.TilemapPosition == target)
            {
                foreach (var offer in town.Services.Catalog(shop.Definition))
                    while (Running && WantsOffer(town,shop.Definition,offer))
                        if (!BuyOffer(town,shop.Definition,offer)) break;
                shopped.Add(shop); return;
            }
        }
        else if (trainer != null)
        {
            target = ServicePosition(town,trainer);
            if (player.ControllingTownAlly.TilemapPosition == target)
            { LearnOffer(town,training.ally,training.offer); return; }
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
        bool Blocked(Vector3Int cell) => cell != target && (town.TownBuildings.Any(b => !b.HasInterior && b.TilemapPosition == cell) ||
            town.TownAllies.Any(a => a.TilemapPosition == cell)) || !town.CanEnter(cell);
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

    private static float EquipmentScore(EquipmentItemDefinition item) => item.StatModification.Strength + item.StatModification.MagicPower +
        item.StatModification.Defense + item.StatModification.HPMax*.1f + item.StatModification.SPMax*.2f;
    private static bool IsUpgrade(TownAlly ally, EquipmentItemDefinition item)
    {
        if (!HeroClass.AllowsItem(ally.PrimaryClass,ally.SecondaryClass,(EquipableInventoryItem)item.AsInventoryItem(null))) return false;
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

    internal static bool TryGateActions(OverworldScene world, string routeId = null)
    {
        foreach (var route in world.Context.Gates.Nearby(world.Position).Where(r => routeId == null || r.Id == routeId))
        {
            using var session = world.BeginGateInteraction(route.Id);
            if (session == null) continue;
            foreach (var key in world.CollectedKeys)
                if (world.AttemptGate(session, new KeyLockAction(key)) == LockOutcome.Opened) return true;
            foreach (var capability in world.Held.Values)
                if (world.AttemptGate(session, new CapabilityLockAction(capability)) == LockOutcome.Opened) return true;
        }
        return false;
    }

    private void WorldTick(OverworldScene world, CampaignContext c)
    {
        if (pathWorld != world) { pathWorld = world; worldSteps.Clear(); }
        // This is a probe on every tick, not a player attempt: a missing key must not
        // reopen its modal forever and prevent routing to the key's source.
        if (TryGateActions(world) || world.OpenShortcut()) { worldSteps.Clear(); Log("Open gate or shortcut"); return; }
        var here = c.Location;
        bool Objective(CampaignLocation l) => IsDungeon(l) ? !c.Completed.Contains(l.Id) && (l.Encounter == null || l.Encounter.CanEnter(c.Held,c.Completed)) :
            (l.Kind == LocationKind.Town && (!visited.Contains(l.Id) || c.Campaign.Locations.Any(d => d.ParentTownId == l.Id && !c.Completed.Contains(d.Id)))) ||
            c.Campaign.Sources.Any(s => s.LocationId == l.Id && !c.Claimed.Contains(s.Id) && s.Prerequisites.IsSatisfiedBy(c.Held)) ||
            c.Campaign.Routes.Any(r => r.KeyLocationId == l.Id && !c.Gates.HasKey(r) && r.KeyCondition == KeyAcquisition.AtLocation) ||
            (Options.ExploreAll && !visited.Contains(l.Id));
        if (here != null)
        {
            bool wanted = Objective(here);
            visited.Add(here.Id);
            if (wanted) { worldSteps.Clear(); world.ClaimRewards(announceLocked: false); Log("Interact " + here.Id); return; }
        }
        if (worldSteps.Count > 0)
        {
            if (MoveWorldStep(world,worldSteps.Peek())) { worldSteps.Dequeue(); return; }
            worldSteps.Clear();
        }
        var goals = c.Campaign.Locations.Where(l => l.ParentTownId == null && Objective(l)).Select(l => c.Grid.Locations[l.Id]).ToHashSet();
        goals.UnionWith(GateApproaches(c));
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

    internal static IEnumerable<GridPoint> GateApproaches(CampaignContext c)
    {
        foreach (var gate in c.Grid.Locks)
        {
            var route = c.Campaign.Routes.First(r => r.Id == gate.RouteId);
            if (!c.Gates.NeedsOpening(route) || !(route.ShortcutKind == ShortcutKind.Keyed ? c.Gates.HasKey(route) : route.CanTraverse(c.Held,c.Resolved))) continue;
            // Match OverworldGates.Nearby: water is traversed with Boat, never opened as a door.
            foreach (var cell in gate.Cells.Where(cell => !c.Grid.RequiresBoat(cell)))
                foreach (var offset in Steps.Take(4)) yield return new GridPoint(cell.X + offset.x, cell.Y + offset.y);
        }
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
        ObserveAbilities(game);
        if (game.GameOverScreen.gameObject.activeSelf || PartyRules.IsPartyDefeated(game))
        { Finish("Defeat", "Party defeated by normal gameplay. No retry or difficulty adjustment applied."); return; }
        // Full Control keeps the round running while waiting for each ally's order.
        // The focused ally still needs an autoplay command at that point.
        if ((game.TurnManager.IsProcessingTurn && !game.TurnManager.AwaitingCommand) ||
            game.NewFloorMessage.gameObject.activeSelf) return;
        foreach(var member in PartyRules.PartyMembers(game))
        {
            if(member==null)continue;
            while(member.PendingAttributePoints>0)
                if(!AttributeSpending.TrySpend(member,member.PrimaryClass?.PreferredAttribute ?? HeroAttribute.Str))break;
            member.AttributePromptPending=false;
        }
        if (MenuManager.Instance.CurrentDialog is StairConfirm stairsPrompt) { stairsPrompt.YesClicked(); Log("Confirm stairs/exit"); return; }
        if (game.Finale != null && MenuManager.Instance.Opened && SelectFinaleDestination(game.Finale)) return;
        if (MenuManager.Instance.Opened) { Finish("Unsupported", "Unhandled dungeon dialog: " + MenuManager.Instance.CurrentDialog.GetType().Name); return; }
        var player = game.PlayerController; var ally = player.ControlledAlly; var dungeon = game.CurrentDungeon;
        if (ally == null) return;
        if (Options.DebugPlaythrough && Options.InfiniteResources) player.Gold = 1000000;
        string floor = Common.Instance.CampaignContext.State.PendingDungeon + "/" + player.Floor +
            (game.Finale == null ? "" : "/" + game.Finale.Session.CurrentFloor.ToString());
        if (floorIdentity != floor)
        {
            floorIdentity = floor; discovered.Clear(); repetitions.Clear(); dungeonRoute.Clear(); approachRoute.Clear(); combatObjective = null;
            ResetFinaleRoute();
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
        if (player.PartyLeader != null && ally != player.PartyLeader)
        {
            var action = ally.ChooseAutonomousActions(includeControlled: true).FirstOrDefault() ?? new WaitAction();
            Act(ally, action, "Companion " + action.GetType().Name);
            return;
        }
        if (game.Finale?.IsTransitioning == true) return;
        if (game.Finale?.OnOverworld == true) { FinaleTick(game, ally); return; }
        var skills = new AllySkillPolicy(game, ally, 0) { IncludeControlledAlly = true };
        var puzzleAction = game.Encounter?.AutoplayPuzzleAction(ally);
        if (puzzleAction != null) { Act(ally,puzzleAction,"Solve puzzle through "+puzzleAction.GetType().Name); return; }
        var stash = dungeon.Interactables.OfType<EncounterStash>().FirstOrDefault(s=>s.Position==ally.TilemapPosition &&
            (s.IsRecovery ? game.Prison?.Recovered != true || game.Prison.Overflow.Count>0 && player.Inventory.CanAdd() : !s.Opened));
        if (stash != null) { Act(ally,new InteractAction(stash),"Recover encounter supplies"); return; }
        if (skills.ShouldRun())
        {
            var cast = skills.GetActions().FirstOrDefault();
            if (cast != null) { Act(ally, cast, "Skill " + skills.LastChoice.Option.Skill.SkillName + " (" + skills.LastChoice.Evaluator + ")"); return; }
        }
        var attack = new AllyAttackPolicy(game,ally,0);
        if (attack.ShouldRun()) { var action = attack.GetActions().FirstOrDefault(a => a.IsValid(ally)); if (action != null) { Act(ally,action,"Attack"); return; } }
        var mimicInteraction = NearbyMimicInteraction(game, ally);
        if (mimicInteraction != null) { Act(ally, mimicInteraction, "Open treasure chest disguise"); return; }
        var stairs = dungeon.Interactables.OfType<Stairs>().FirstOrDefault();
        if (stairs == null) { Finish("Unsupported","Ready dungeon has no stairs objective."); return; }
        if (stairs != null && stairs.Position == ally.TilemapPosition && game.CanExitFloor)
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
        var goal = game.Encounter?.AutoplayDestination(ally) ?? stairs.Position;
        Vector3Int? next = RouteTo(goal);
        if (next.HasValue) { approachRoute.Clear(); combatObjective = null; }
        else if (combatObjective.HasValue && combatObjective.Value != ally.TilemapPosition && CanOccupy(combatObjective.Value))
            next = approachRoute.Next(ally.TilemapPosition,combatObjective.Value,DungeonGrid,CanStep);
        // A creature can seal a narrow corridor on the route to the stairs.
        // Approach its nearest footprint edge so normal sight and attack rules can handle it.
        if (next == null)
            foreach (var target in discovered.Where(p => p != ally.TilemapPosition && CanOccupy(p) && game.Enemies.Any(e => e != null && e.Vitals.HP > 0 &&
                Steps.Any(s => discovered.Contains(p+s) && e.ToBounds().Contains(p+s) && dungeon.CanWalkTo(p, p+s)))).OrderBy(p => (p-ally.TilemapPosition).sqrMagnitude))
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

    internal static GameAction NearbyMimicInteraction(Game game, Ally ally)
    {
        foreach (var enemy in game.Enemies.OfType<Enemy>().Where(e => e != null && e.Vitals.HP > 0 && EnemyBehavior.IsDisguised(e)))
        {
            var interaction = new RevealMimicAction(enemy);
            if (interaction.IsValid(ally) && game.CurrentDungeon.CanWalkTo(ally.TilemapPosition, enemy.TilemapPosition))
            { ally.SetFacingByTargetPosition(enemy.TilemapPosition); return interaction; }
        }
        return null;
    }

    internal static GameAction FollowerTravelAction(Game game, Ally ally, Ally leader) =>
        AllyNavigation.FollowOrSearch(game, ally);

    private void Act(Ally ally, GameAction action, string description)
    {
        if (!action.IsValid(ally)) { Finish("Unsupported", "Policy selected invalid action: " + description); return; }
        Report.DungeonTurns++; Log(description); if (Running) ally.SetAction(action);
    }

    private void Log(string message)
    {
        Status = message; Report.Actions++;
        var c = Common.Instance?.CampaignContext; var game = Game.Instance;
        var town = game == null ? FindFirstObjectByType<Town>() : null; var ally = game?.PlayerController?.ControlledAlly;
        string position = ally != null ? ally.TilemapPosition.ToString() : town?.TownPlayer?.ControllingTownAlly != null ? town.TownPlayer.ControllingTownAlly.TilemapPosition.ToString() : c != null && c.State.Scene == "Overworld" ? c.Position.ToString() : "";
        WriteEvent(new AutoplayEvent { Kind = "Action", Message = message, ActorId = ally != null ? ActorId(ally) : town?.TownPlayer?.ControllingTownAlly?.Id });
        string combat = game == null ? "" : string.Join(",",game.Enemies.Where(e => e != null).Select(e => e.GetInstanceID() + ":" + e.Vitals.HP));
        string signature = floorIdentity + "/" + progressSignature + "/" + position + "/" + message + "/" + combat;
        repetitions.TryGetValue(signature,out var count); repetitions[signature] = count+1;
        if (!IsLootSweep && count > 30) { Finish("Stalled", "Repeated action/state loop: " + message); return; }
        if (!IsLootSweep) lastProgress = Time.realtimeSinceStartupAsDouble;
        if (Report.Actions >= Options.ActionLimit) { Finish("Failed", "Floor action limit reached."); return; }
        if (Report.Actions % 50 == 0) { Capture(); WriteReport(); }
    }

    public void SetSpeed(float speed)
    {
        if (!SessionRunning || float.IsNaN(speed) || float.IsInfinity(speed)) return;
        Options.Speed = Mathf.Clamp(speed, .5f, 32f);
        Time.fixedDeltaTime = originalFixedDeltaTime * Options.Speed;
        if (!Paused && Running) Time.timeScale = Options.Speed;
        nextTick = Time.unscaledTime;
        WriteReport();
    }

    public void SetPaused(bool paused)
    {
        if (!SessionRunning || Paused == paused) return;
        Paused = paused;
        if (paused) pausedAt = Time.realtimeSinceStartupAsDouble;
        else { var duration = Time.realtimeSinceStartupAsDouble-pausedAt; pausedSeconds += duration; lastProgress += duration; }
        Time.timeScale = paused || !Running ? 0 : Options.Speed;
    }
    public void Stop()
    {
        CancelBatch("Stopped manually; no result inferred.");
        Finish("Stopped", "Stopped manually; no result inferred.");
    }

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
        if (PlayerControlled || takingControl || exiting || ReturnPromptOpen) return;
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
    public void SetPanelVisible(bool visible)
    {
        PanelVisible = visible;
        acceptInputAfter = Time.unscaledTime + .2f;
    }

    public void TakeControl()
    {
        if (!CanTakeControl || PlayerControlled || takingControl || exiting) return;
        takingControl = true;
        ReturnPromptOpen = false;
        SetPaused(false);
        CancelBatch("Player took control of the isolated session.");
        Time.timeScale = 1;
        StartCoroutine(TakeControlRoutine());
    }

    private IEnumerator TakeControlRoutine()
    {
        while (Finalizing) yield return null;
        // Finish the in-flight action before releasing input; never reset action budgets.
        yield return null;
        while (!exiting)
        {
            var common = Common.Instance;
            var game = FindFirstObjectByType<Game>();
            var town = FindFirstObjectByType<Town>();
            var world = FindFirstObjectByType<OverworldScene>();
            bool ready = common != null && !common.Travel.IsTransitioning &&
                !common.ScreenTransition.BlockScreen.activeSelf &&
                (game != null ? game.IsReady && (!game.TurnManager.IsProcessingTurn || game.TurnManager.AwaitingCommand) :
                 town != null ? town.IsReady && !town.TownPlayer.IsBusy :
                 world != null && world.IsReady && !world.IsMoving);
            bool held = Keyboard.current?.anyKey.isPressed == true || Mouse.current?.leftButton.isPressed == true ||
                Touchscreen.current?.primaryTouch.press.isPressed == true ||
                Gamepad.all.Any(p => p.allControls.OfType<ButtonControl>().Any(b => b.isPressed));
            if (!ready || held) { yield return null; continue; }
            Report.EligibleForBalance = false;
            Finish("PlayerControl", "Player took control of the isolated session.");
            WriteReport();
            if (game != null && game.PlayerController.ControlledAlly != null)
            {
                var ally = game.PlayerController.ControlledAlly;
                bool waiting = ally.IsWaitingForPlayerInput;
                game.PlayerController.FocusCommand(ally);
                ally.IsWaitingForPlayerInput = waiting;
            }
            // Preserve an open stair/shop dialog's input map.
            common.MenuInputHandler.ClearInputThisFrame();
            Paused = false;
            PanelVisible = false;
            PlayerControlled = true;
            Time.fixedDeltaTime = originalFixedDeltaTime;
            takingControl = false;
            Time.timeScale = 1;
            yield break;
        }
    }

    private Rect CollapsedPanel => new Rect(Mathf.Max(0, Screen.width - 148), 64, 132, 36);
    private Rect PlaybackPanel => new Rect(Mathf.Max(0, Screen.width - 340), 64, Mathf.Min(324, Screen.width), Mathf.Min(470, Screen.height - 80));
    private bool OverPlaybackPanel(Vector2 position) => playbackUI != null ? playbackUI.Contains(position) :
        (PanelVisible ? PlaybackPanel : CollapsedPanel).Contains(new Vector2(position.x, Screen.height - position.y));

    private bool UserInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.allKeys.Any(key => key != null && key != keyboard.f11Key && key.wasPressedThisFrame)) return true;
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
        while (Finalizing || unloadingRun) yield return null;
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
        var common = Common.Instance;
        common.ScreenTransition.HoldClosed(); // Cancel a queued transition callback before restoring the original campaign.
        CampaignParty.ClearLiveParty(common);
        common.Travel.SceneReady();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        common.CampaignContext = originalContext;
        common.GameSaveData = originalSave;
        common.CurrentTownConfiguration = originalTownConfiguration;
        common.ScreenTransition.DoOpen();
        Destroy(gameObject);
    }

    private void Capture()
    {
        var common = Common.Instance; var game = Game.Instance;
        var c = common?.CampaignContext;
        Report.Seconds = Time.realtimeSinceStartupAsDouble-started-pausedSeconds-(Paused ? Time.realtimeSinceStartupAsDouble-pausedAt : 0);
        Report.Scene = SceneManager.GetActiveScene().name; Report.Campaign = c?.Capture();
        Report.Location = c?.State.PendingDungeon.Length > 0 ? c.State.PendingDungeon : c?.Location?.Id;
        CaptureFinale(c?.Finale);
        Report.Visited = visited.OrderBy(x => x).ToArray();
        if (startedCampaign && common?.GameSaveData != null)
        {
            var liveTown = FindFirstObjectByType<Town>();
            if (liveTown != null && liveTown.IsReady) liveTown.WriteSaveData();
            SaveSystem.Capture(common);
            if (IsLootSweep && game != null && game.IsReady)
            {
                var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(common.GameSaveData));
                DungeonReturnService.Commit(snapshot, common.CurrentTownConfiguration ?? TownSceneLoader.Default, false,
                    game.PlayerController.Gold, game.PlayerController.Inventory.InventoryItems, PartyRules.PartyMembers(game), keepLoot: true);
                SaveSystem.SaveData(snapshot);
            }
            else SaveSystem.SaveData(common.GameSaveData);
        }
        if (game == null || !game.IsReady)
        {
            var town = FindFirstObjectByType<Town>();
            Report.Floor = 0; Report.Party.Clear(); Report.Enemies.Clear();
            Report.Gold = town != null ? town.TownPlayer.Gold : common?.GameSaveData?.TownSaveData.Gold ?? 0;
            Report.Inventory = town != null ? town.TownPlayer.Inventory.Select(ItemLabel).ToArray() : Array.Empty<string>();
            if (town != null) Report.Party = town.TownPlayer.RecruitedAllies.Select(a => new AutoplayActor {
                Id = a.Id, Name = a.Name, Level = a.Level, HP = a.Hp, SP = a.Sp, X = a.TilemapPosition.x, Y = a.TilemapPosition.y,
                Equipment = a.Equipment.GetEquippedItems().Select(ItemLabel).ToArray(), Skills = a.Skills.ToArray(), Class = HeroClass.Label(a.PrimaryClass, a.SecondaryClass) }).ToList();
            return;
        }
        Report.Floor = game.PlayerController.Floor; Report.Gold = game.PlayerController.Gold;
        Report.Inventory = game.PlayerController.Inventory.InventoryItems.Select(ItemLabel).ToArray();
        Report.Party = game.Allies.Concat(game.DownedAllies).Concat(game.DeadUnits.OfType<Ally>()).Where(a => a != null && !PartyRules.IsSummon(a)).Distinct().Select(a => new AutoplayActor {
            Id = a.TownAllyId, Name = common.GameSaveData.Roster.FirstOrDefault(r => r.AllyId == a.TownAllyId)?.AllyName ?? a.name,
            HP = a.Vitals.HP, MaxHP = a.FinalStats.HPMax, SP = a.Vitals.SP, Hunger = a.Vitals.Hunger,
            Level = a.Vitals.Level, Strength = a.FinalStats.Strength, Defense = a.FinalStats.Defense, X = a.TilemapPosition.x, Y = a.TilemapPosition.y,
            Equipment = a.Equipment.GetEquippedItems().Select(i => i.ItemName).ToArray(), Skills = a.Skills.Select(s => s.SkillName).ToArray(), Class = HeroClass.Label(a.PrimaryClass, a.SecondaryClass) }).ToList();
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
    private void OnDestroy()
    {
        if (disposed || Options == null) return;
        disposed = true;
        try { FinalizeOnDestroy(); }
        finally
        {
            Application.logMessageReceived -= OnLog;
            try { actions?.Dispose(); issues?.Dispose(); saveScope?.Dispose(); }
            finally
            {
                Active = null; Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
                UnityEngine.Random.state = originalRandom; ReleaseSessionReload();
            }
        }
    }

    private sealed class AutoplayStore : ISaveStore
    {
        private string json, path;
        public bool Sealed;
        public void Begin(string directory) { path = Path.Combine(directory,"save.json"); json = null; Sealed = false; }
        public string Read(int slot) => json;
        public void Write(int slot, string value) { if (path == null) return; if (!Sealed) File.WriteAllText(path,value); json = value; }
        public void Clear(int slot) { json = null; }
    }
}
