using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>One live overworld floor, finite populations, and production combat through homecoming.</summary>
public sealed class FinaleRuntime
{
    readonly Game game;
    readonly Dictionary<string, FinaleActor> actors = new();
    readonly HashSet<string> spawned = new();
    GameObject terrain;
    bool transitioning;
    int round;
    PartyMenuPicker destinationPicker;
    readonly Dictionary<string, UnityEngine.UI.Button> destinationButtons = new();
    public FinaleSession Session { get; }
    public bool OnOverworld => true;
    public bool IsTransitioning => transitioning;
    public string Title => $"The reverse world - Floor {Session.CurrentFloor}";
    internal static string LoadingTitle(int floor) =>
        $"Loading...\n<size=70%>The reverse world - Floor {floor}</size>";
    public string ObjectiveText
    {
        get
        {
            var region = Session.Floor.Regions.FirstOrDefault(r => !Session.ClearedRegions.Contains(r.Id));
            return region == null ? "Floor cleared. Ascend at the starting town." :
                $"{region.Name} has {Session.RemainingIn(region.Id)} enemies remaining. {Session.Remaining} remain on this floor.";
        }
    }
    internal FinaleRuntime(Game game, FinaleSession session) { this.game = game; Session = session; }
    internal IEnumerator Start()
    {
        yield return LoadFloor();
        GameMessages.Post("Clear every region. Towns and dungeons hide mimics; the last building revealed in each biome hides a boss.");
    }
    internal bool CanAct(Enemy enemy) => enemy.GetComponent<FinaleActor>() is not { } actor ||
        !actor.Hidden && Session.Unlocked.Contains(actor.RegionId) && actor.RevealedRound < round;
    internal Enemy Reveal(Enemy enemy, Character from)
    {
        var actor = enemy.GetComponent<FinaleActor>();
        if (actor == null || from != game.PlayerController.ControlledAlly ||
            !Session.Reveal(actor.Id, from.TilemapPosition.ToGridPoint(), out var swappedWith)) return null;
        if (swappedWith != null)
        {
            // Both actors are still hidden. Move the existing prefabs with their combat behavior
            // to the chosen buildings without adding actors or inventing death events.
            var other = actors[swappedWith];
            var otherEnemy = other.GetComponent<Enemy>();
            var position = enemy.TilemapPosition;
            (actor.Id, other.Id) = (other.Id, actor.Id);
            (actor.IsTown, other.IsTown) = (other.IsTown, actor.IsTown);
            var disguises = actor.Disguises.ToArray();
            actor.Disguises.Clear(); actor.Disguises.AddRange(other.Disguises);
            other.Disguises.Clear(); other.Disguises.AddRange(disguises);
            enemy.SetPosition(otherEnemy.TilemapPosition); otherEnemy.SetPosition(position);
            actors[actor.Id] = actor; actors[other.Id] = other;
            actor = other; enemy = otherEnemy;
        }
        actor.Hidden = false; actor.RevealedRound = round;
        foreach (var model in actor.Disguises) if (model != null) model.SetActive(false);
        enemy.VisualParent.SetActive(true);
        enemy.IsDormant = false; enemy.PursuitTarget = from; enemy.PursuitPosition = from.TilemapPosition;
        GameMessages.Post(enemy.IsBoss ? $"{GameMessages.Name(enemy)} was hiding in the building!" :
            actor.IsTown ? "The town was a mimic!" : "The dungeon was a mimic!");
        AutoplayRunner.FinaleEvent("MimicRevealed", actor.Id, actor.Family);
        game.UpdateMiniMap(); return enemy;
    }
    internal void OnDeath(Character enemy)
    {
        var actor = enemy.GetComponent<FinaleActor>();
        if (actor == null || enemy.Vitals.HP > 0 || !Session.RecordDeath(actor.Id)) return;
        AutoplayRunner.FinaleEvent("EnemyDied", actor.Id, actor.Family);
    }
    internal void EndTurn()
    {
        round++;
        bool wasCleared = Session.FloorCleared;
        var opened = Session.ReconcileClearance();
        foreach (string region in opened)
        { AutoplayRunner.FinaleEvent("RegionOpened", region); GameMessages.Post("Region cleared. The next boundary is open."); }
        SpawnUnlockedRegions();
        if (!wasCleared && Session.FloorCleared) GameMessages.Post("All enemies defeated. The ascending stairs are open.");
        if (opened.Count > 0 || wasCleared != Session.FloorCleared) RefreshGates();
        foreach (var id in spawned)
            if (!Session.Defeated.Contains(id) && (!actors.TryGetValue(id, out var actor) || actor == null))
                throw new InvalidOperationException("Required finale actor disappeared without dying: " + id);
    }
    internal void RegisterSummon(Character source, Enemy enemy)
    {
        var parent = source?.GetComponent<FinaleActor>();
        if (parent == null) throw new InvalidOperationException("Finale summon has no source identity.");
        string id = parent.Id + "/summon-" + ++parent.Children;
        if (!Session.RegisterChild(parent.Id, id)) throw new InvalidOperationException("Cannot register finale summon " + id);
        Register(enemy, id, parent.RegionId, enemy.DisplayName);
    }
    void Register(Enemy enemy, string id, string region, string family, FinaleEnemyPlacement mimic = null)
    {
        var actor = enemy.GetComponent<FinaleActor>() ?? enemy.gameObject.AddComponent<FinaleActor>();
        actor.Id = id; actor.RegionId = region; actor.Family = family; actor.Hidden = mimic != null;
        actor.IsTown = mimic?.IsTown == true; actor.RevealedRound = -1; actor.Children = 0; actor.Disguises.Clear();
        enemy.transform.SetParent(game.CurrentDungeon.transform, true);
        actors.Add(id, actor); spawned.Add(id);
        if (mimic != null)
        {
            actor.Disguises.AddRange(terrain.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Location " + mimic.LocationId || t.name == "Town " + mimic.LocationId).Select(t => t.gameObject));
            if (actor.Disguises.Count == 0) throw new InvalidOperationException("Missing miniature for " + mimic.LocationId);
            enemy.VisualParent.SetActive(false);
        }
        AutoplayRunner.FinaleEvent("EnemySpawned", id, family);
    }
    void SpawnUnlockedRegions()
    {
        var floor = Session.Floor;
        var reserved = new HashSet<Vector3Int>(floor.Reserved.Select(p => p.ToCell()));
        foreach (var p in floor.Enemies) reserved.Add(p.Position.ToCell());
        foreach (var trap in floor.Traps) reserved.Add(trap.Cell.ToCell());
        foreach (var placement in floor.Enemies.Where(e => Session.Unlocked.Contains(e.RegionId) && !spawned.Contains(e.Id)))
        {
            if (placement.IsMimic)
            {
                var prefab = FinaleEnemyStats.Prefab(placement.Family, placement.IsBoss);
                if (prefab == null) throw new InvalidOperationException("Missing finale mimic: " + placement.Family);
                var enemy = UnityEngine.Object.Instantiate(prefab, game.CurrentDungeon.transform);
                FinaleEnemyStats.Apply(enemy, FinalePlan.CombatDifficulty, placement.IsBoss);
                enemy.UpdateCachedStats(); enemy.InitialzeVitalsFromStats(); enemy.TilemapPosition = placement.Position.ToCell();
                Register(enemy, placement.Id, placement.RegionId, placement.Family, placement);
                enemy.InitializeSpawn(); game.Enemies.Add(enemy);
            }
            else
            {
                var prefab = game.EnemyManager.GetEnemyPrefab(FinalePlan.CombatDifficulty, placement.Roll);
                var pack = game.EnemyManager.SpawnPack(prefab, placement.Position.ToCell(), reserved,
                    cell => Character.ToBounds(prefab.FootPrint, cell).Cells().All(p => floor.RegionAt(p.ToGridPoint()) == placement.RegionId));
                if (pack.Count == 0) throw new InvalidOperationException("No enemy could occupy finale placement " + placement.Id);
                for (int i = 0; i < pack.Count; i++)
                {
                    string id = i == 0 ? placement.Id : placement.Id + "/member-" + i;
                    if (i > 0 && !Session.RegisterChild(placement.Id, id)) throw new InvalidOperationException("Duplicate finale pack identity.");
                    Register(pack[i], id, placement.RegionId, pack[i].DisplayName);
                }
            }
        }
    }
    internal bool RequestExit()
    {
        if (transitioning || !Session.FloorCleared || game.PlayerController.ControlledAlly.TilemapPosition != Session.Floor.Stairs.ToCell()) return false;
        transitioning = true; game.StartCoroutine(Ascend()); return true;
    }
    IEnumerator Ascend()
    {
        while (game.TurnManager.IsProcessingTurn) yield return null;
        if (!Session.UseStairs(game.PlayerController.ControlledAlly.TilemapPosition.ToGridPoint()))
        { transitioning = false; yield break; }
        AutoplayRunner.FinaleEvent("StairsUsed", "floor-" + Session.StairTransitions.Last());
        if (Session.FinalExitCompleted)
        {
            AutoplayRunner.CaptureFinaleState(Session);
            if (!Common.Instance.Travel.FinishDungeon(true, game.PlayerController))
                throw new InvalidOperationException("Finale stairs could not complete homecoming.");
            yield break;
        }
        game.SetFinaleReady(false);
        Common.Instance.ScreenTransition.HoldClosed(LoadingTitle(Session.CurrentFloor));
        yield return ScreenTransition.PresentLoadingScreen();
        SummonRules.DespawnClones(game); PartyRules.RestoreAllDowned(game, 1);
        foreach (var ally in game.Allies) { global::PendingCast.Cancel(ally, "floor changed"); ally.currentInteractable = null; }
        terrain.SetActive(false); UnityEngine.Object.Destroy(terrain);
        game.Enemies.Clear(); actors.Clear(); spawned.Clear();
        foreach (var dead in game.DeadUnits) if (dead != null) UnityEngine.Object.Destroy(dead.gameObject);
        game.DeadUnits.Clear(); game.CurrentDungeon = null;
        yield return null; yield return LoadFloor();
        transitioning = false; game.SetFinaleReady(true); Common.Instance.ScreenTransition.DoOpen();
    }
    IEnumerator LoadFloor()
    {
        game.PlayerController.Floor = Session.CurrentFloor;
        yield return game.DungeonGenerator.GenerateFinaleTerrain(Session.Floor);
        terrain = game.DungeonGenerator.FinaleTerrainRoot;
        game.ActivateFinaleWorld(game.DungeonGenerator.GeneratedDungeon, new(), new());
        game.CurrentDungeon.InitializeCache();
        game.SetFinaleEncounter(new EncounterRuntime(game)); PlaceParty(Session.Floor.Start.ToCell());
        SpawnUnlockedRegions();
        game.CurrentDungeon.SetStairs(Session.Floor.Stairs.ToCell());
        foreach (var trap in game.CurrentDungeon.Floor.Traps) game.CurrentDungeon.SetTrap(trap.Cell.ToCell(), trap.Roll);
        foreach (var cell in Session.Floor.Grid.Warps.SelectMany(w => new[] { Session.Floor.Grid.Locations[w.From], Session.Floor.Grid.Locations[w.To] })
                     .Append(Session.Floor.Start).Distinct().Where(p => !p.Equals(Session.Floor.Stairs)))
            FinalePortal.Create(game.CurrentDungeon, cell.ToCell(), "Choose a warp or leave the reverse world", () => RequestLocation(cell));
        var map = game.PlayerController.Minimap;
        map.FogOverlay.Initialize(game.CurrentDungeon); map.Initialize(game.CurrentDungeon);
        game.FloorReveal.LayoutRevealed = true; map.RevealLayout();
        foreach (var ally in game.Allies)
        {
            ally.currentInteractable = game.CurrentDungeon.GetInteractable(ally.TilemapPosition);
            ally.Vitals.ActionsPerTurnLeft = ally.FinalStats.ActionsPerTurnMax;
            ally.Vitals.AttacksPerTurnLeft = ally.FinalStats.AttacksPerTurnMax; ally.SyncDisplayedStats();
        }
        ClassPassives.OnFloorStart(game);
        game.UpdateMiniMap(); game.PlayerController.StartTurn();
        game.PlayerController.CameraController.SnapToFollowTarget();
        game.NewFloorMessage.ShowNewFloor(Session.CurrentFloor);
        AutoplayRunner.FinaleEvent("FloorEntered", "floor-" + Session.CurrentFloor);
    }
    void PlaceParty(Vector3Int start)
    {
        var reserved = new HashSet<Vector3Int>();
        foreach (var ally in game.Allies.OrderBy(a => a == game.PlayerController.ControlledAlly ? 0 : 1))
        {
            var cell = game.CurrentDungeon.Floor.NearestFree(start.ToGridPoint(), p => Session.IsWalkable(p) && !reserved.Contains(p.ToCell()) &&
                !game.Enemies.Any(e => e != null && e.Vitals.HP > 0 && e.ToBounds().Contains(p.ToCell()))).ToCell();
            ally.SetPosition(cell); reserved.Add(cell);
        }
    }
    void RefreshGates()
    {
        game.CurrentDungeon.DoorsChanged();
        var map = game.PlayerController.Minimap.dungeonMap;
        for (int y = 0; y < Session.Floor.Grid.Height; y++) for (int x = 0; x < Session.Floor.Grid.Width; x++)
            map[x, y].isWall = !Session.IsWalkable(new GridPoint(x, y));
        game.PlayerController.Minimap.InvalidateTerrain();
        game.PlayerController.Minimap.RevealLayout();
    }
    internal List<AStar.Node> PursuitPath(Character character)
    {
        using var measurement = new FinalePerformance.Scope("Enemy pursuit");
        if (!character.PursuitPosition.HasValue) return null;
        var from = character.TilemapPosition; var target = character.PursuitPosition.Value;
        if (!FinaleActor.AllowsPosition(character, target)) return null;
        var occupied = game.AllCharacters.Select(c => c.TilemapPosition).ToHashSet();
        var traps = game.CurrentDungeon.Interactables.OfType<Trap>()
            .Where(t => t.VisualObject.activeInHierarchy).Select(t => t.Position).ToHashSet();
        List<AStar.Node> Search(bool local)
        {
            int width = Session.Floor.Grid.Width, height = Session.Floor.Grid.Height;
            var grid = new AStar.Node[width, height];
            int left = local ? Math.Max(0, Math.Min(from.x, target.x) - 12) : 0;
            int right = local ? Math.Min(width, Math.Max(from.x, target.x) + 13) : width;
            int bottom = local ? Math.Max(0, Math.Min(from.y, target.y) - 12) : 0;
            int top = local ? Math.Min(height, Math.Max(from.y, target.y) + 13) : height;
            for (int x = left; x < right; x++) for (int y = bottom; y < top; y++)
            {
                var cell = new Vector3Int(x, y);
                bool open = Character.ToBounds(character.FootPrint, cell).Cells()
                    .All(p => FinaleActor.AllowsPosition(character, p));
                int cost = game.CurrentDungeon.IsHazard(cell) ? 100 : occupied.Contains(cell) || traps.Contains(cell) ? 5 : 0;
                grid[x, y] = new AStar.Node(x, y, open, cost);
            }
            return AStar.FindPath(grid, grid[from.x, from.y], grid[target.x, target.y]);
        }
        // Normal pursuit costs and corner rules, with a full-region fallback for long detours.
        return Search(true) ?? Search(false);
    }
    internal string GateHint(Vector3Int from, Vector3Int to) =>
        Session.Floor.IsGround(to.ToGridPoint()) && !Session.IsWalkable(to.ToGridPoint()) ? ObjectiveText : null;
    internal bool RequestLocation(GridPoint from)
    {
        if (transitioning || !game.PlayerController.ControlledAlly.TilemapPosition.ToGridPoint().Equals(from)) return false;
        var choices = new List<(string Label, string Description, Action Execute)>(); var ids = new List<string>();
        destinationButtons.Clear();
        if (from.Equals(Session.Floor.Start))
        { ids.Add("retreat"); choices.Add(("Leave the reverse world", "Return with loot. Finale progress resets.", () => game.PlayerController.PendingRetreat = true)); }
        foreach (var warp in Session.Floor.Grid.WarpsAt(from))
        {
            string id = warp.Id; bool available = Session.WarpDestinations(from).Any(w => w.RouteId == id);
            var destination = Session.Floor.Campaign.Locations.Single(l => l.Id == (Session.Floor.Grid.Locations[warp.From].Equals(from) ? warp.To : warp.From));
            ids.Add("warp/" + id);
            choices.Add(("Warp to " + Session.Floor.Campaign.Regions.Single(r => r.Id == destination.RegionId).DisplayName,
                available ? "Travel through the open warp." : "Clear the regions before using this warp.", available ? () => RequestWarp(from, id) : null));
        }
        if (choices.Count == 0) return false;
        destinationPicker = PartyMenuPicker.BuildDetailed(AuthoredUI.Require<PartyMenu>(game.transform).transform.parent, "Choose a destination", choices);
        var rows = destinationPicker.Rows.GetComponentsInChildren<AuthoredButton>();
        for (int i = 0; i < ids.Count; i++) destinationButtons.Add(ids[i], rows[i].Button);
        MenuManager.Open(destinationPicker); return true;
    }
    internal bool SelectDestination(string id)
    {
        if (id == null || destinationPicker == null || MenuManager.Instance.CurrentDialog != destinationPicker ||
            !destinationButtons.TryGetValue(id, out var button) || !button.interactable) return false;
        button.onClick.Invoke(); return true;
    }
    internal bool RequestWarp(GridPoint from, string routeId)
    {
        if (transitioning || !game.PlayerController.ControlledAlly.TilemapPosition.ToGridPoint().Equals(from)) return false;
        var routes = Session.WarpDestinations(from).Where(w => w.RouteId == routeId).ToArray();
        if (routes.Length == 0 || game.Enemies.Any(e => e != null && e.Vitals.HP > 0 && e.ToBounds().Contains(routes[0].Destination.ToCell()))) return false;
        transitioning = true; game.StartCoroutine(Warp(routes[0].Destination)); return true;
    }
    IEnumerator Warp(GridPoint target)
    {
        while (game.TurnManager.IsProcessingTurn) yield return null;
        game.SetFinaleReady(false); PlaceParty(target.ToCell());
        foreach (var ally in game.Allies) ally.currentInteractable = game.CurrentDungeon.GetInteractable(ally.TilemapPosition);
        game.UpdateMiniMap(); game.PlayerController.CameraController.SnapToFollowTarget();
        transitioning = false; game.SetFinaleReady(true);
    }
}

public sealed class FinaleActor : MonoBehaviour
{
    public string Id, RegionId, Family;
    public bool Hidden, IsTown;
    internal int RevealedRound = -1, Children;
    internal readonly List<GameObject> Disguises = new();
    public static bool AllowsPosition(Character actor, Vector3Int cell)
    {
        var finale = Game.Instance?.Finale;
        if (finale == null || finale.IsTransitioning || Game.Instance.CurrentDungeon == null) return true;
        return finale.Session.IsWalkable(cell.ToGridPoint()) && (actor?.GetComponent<FinaleActor>() is not { } identity ||
            finale.Session.Floor.RegionAt(cell.ToGridPoint()) == identity.RegionId);
    }
}

public partial class Game
{
    public FinaleRuntime Finale { get; private set; }
    IEnumerator StartFinale() { IsReady = false; yield return Finale.Start(); IsReady = true; }
    internal void SetFinaleReady(bool ready) { IsReady = ready; if (!ready) TurnManager.InteruptTurn(); }
    internal void ActivateFinaleWorld(TileWorldDungeon dungeon, List<Character> enemies, FloorRevealState reveal)
    {
        CurrentDungeon = dungeon; Enemies = enemies; FloorReveal = reveal; CurrentDungeon.IsExitFloor = false;
        foreach (var ally in Allies) ally.currentInteractable = null;
    }
    internal void SetFinaleEncounter(EncounterRuntime encounter) => Encounter = encounter;
}

public sealed class FinalePortal : Interactable
{
    string label; Func<bool> activate;
    internal static void Create(TileWorldDungeon dungeon, Vector3Int cell, string text, Func<bool> action)
    {
        var marker = EncounterRuntime.Marker(dungeon, "Warp", cell, new Color(.35f, .65f, 1f), .03f);
        var portal = marker.AddComponent<FinalePortal>(); portal.Position = cell; portal.label = text; portal.activate = action; dungeon.Interactables.Add(portal);
    }
    internal override string GetInteractionText() => label;
    internal override List<GameAction> GetInteractionSideEffects(Character character) => character != Game.Instance.PlayerController.ControlledAlly ? new() :
        new() { new DynamicGameAction(_ => { activate(); return new(); }, null, () => character == Game.Instance.PlayerController.ControlledAlly) };
}
