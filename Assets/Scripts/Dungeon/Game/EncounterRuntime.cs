using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Encounter objectives share normal characters, actions, collision and the dungeon turn loop.</summary>
public sealed class EncounterRuntime
{
    readonly Game game;
    readonly DungeonFloor floor;
    readonly Dictionary<string, GameObject> visuals = new();
    readonly Dictionary<string, EncounterActor> actors = new();
    int witnessStep;
    bool witnessDiverged;
    public DungeonObjectiveProgress Progress { get; }
    public PuzzleState Puzzle { get; }
    public bool CanExit => Progress.CanExit;
    public bool AllowsReset => floor.Objective.AllowsReset;
    public string ObjectiveText => floor.Objective.Kind switch {
        DungeonObjectiveKind.DefeatEnemies => $"Defeat the required enemies ({Progress.RemainingEnemyIds.Count} remaining).",
        DungeonObjectiveKind.DefeatBoss => "Defeat the arena boss.",
        DungeonObjectiveKind.PositionObjects => "Push every boulder onto a marked goal. Reset Floor restores the entry state.",
        DungeonObjectiveKind.MatchLights => "Step on lights to match the target board.",
        DungeonObjectiveKind.TrapEnemy => "Lure the invulnerable pursuer into the marked trap.",
        DungeonObjectiveKind.GuideEnemy => "Guide the fleeing creature onto its marked destination.",
        DungeonObjectiveKind.RecoverPossessions => "Recover the party's possessions from the guarded stash.",
        _ => "Reach the exit."
    };

    internal EncounterRuntime(Game game)
    {
        this.game = game; floor = game.CurrentDungeon.Floor;
        Progress = new DungeonObjectiveProgress(floor); Puzzle = new PuzzleState(floor);
    }

    internal void Populate(int difficulty)
    {
        foreach (var placement in floor.PuzzleObjects)
        {
            if (placement.Kind == PuzzleObjectKind.BasicSupplies)
            { EncounterStash.Create(game.CurrentDungeon, placement.Position.ToCell(), false); continue; }
            Color color = placement.Kind switch {
                PuzzleObjectKind.Boulder => new Color(.4f,.43f,.5f),
                PuzzleObjectKind.Goal => new Color(.2f,.9f,.4f),
                PuzzleObjectKind.Trap => new Color(1,.25f,.25f),
                PuzzleObjectKind.Destination => new Color(.2f,.9f,.95f),
                _ => placement.InitialState ? Color.yellow : new Color(.15f,.17f,.25f)
            };
            if (placement.Kind == PuzzleObjectKind.LightTarget) color = placement.TargetState ? Color.yellow : new Color(.15f,.17f,.25f);
            var marker = Marker(game.CurrentDungeon, placement.Id, placement.Position.ToCell(), color, placement.Kind == PuzzleObjectKind.Boulder ? .72f : .08f);
            visuals.Add(placement.Id, marker);
        }
        foreach (var stash in floor.RecoveryStashes) EncounterStash.Create(game.CurrentDungeon, stash.Position.ToCell(), true);
        foreach (var placement in floor.ObjectiveEnemies)
        {
            var prefab = placement.IsBoss ? LargeBossBehavior.Prefab(placement.Family) :
                game.EnemyManager.EnemyPrefabs.FirstOrDefault(e=>e!=null && e.name=="Enemy_"+placement.Family.Replace(" ","")) ??
                game.EnemyManager.GetEnemyPrefab(difficulty, StableHash(placement.Id)^floor.Seed,
                    placement.Footprint == 3 ? FootPrint.Size3x3 : FootPrint.Size1x1);
            if (prefab == null) throw new InvalidOperationException($"Missing objective enemy prefab: {placement.Family}");
            var enemy = UnityEngine.Object.Instantiate(prefab, game.transform);
            enemy.UpdateCachedStats(); enemy.InitialzeVitalsFromStats();
            enemy.IsDormant = false; enemy.TilemapPosition = placement.Position.ToCell();
            if (!SkillMovement.CanOccupy(enemy,enemy.TilemapPosition))
                throw new InvalidOperationException($"Objective actor {placement.Id} has a blocked {placement.Footprint}x{placement.Footprint} footprint at {placement.Position}.");
            enemy.InitializeSpawn();
            game.Enemies.Add(enemy);
            Register(enemy, placement.Id, placement.Invulnerable);
        }
        RefreshVisuals();
        if (floor.Objective.Kind != DungeonObjectiveKind.ReachExit) GameMessages.Post(ObjectiveText);
    }

    internal static int StableHash(string id)
    { unchecked { int hash = 17; foreach (char c in id) hash = hash * 31 + c; return hash; } }

    internal void Register(Character actor, string id, bool invulnerable = false)
    {
        var identity = actor.GetComponent<EncounterActor>() ?? actor.gameObject.AddComponent<EncounterActor>();
        identity.Id = id; identity.Invulnerable = invulnerable; actors[id] = identity;
        if (!Progress.IsRequired(id)) Progress.RegisterReinforcement(id);
    }

    internal void OnDeath(Character actor)
    {
        var identity = actor.GetComponent<EncounterActor>();
        if (identity == null || identity.Resolved) return;
        identity.Resolved = true;
        bool before = CanExit; Progress.Defeat(identity.Id); AnnounceCompletion(before);
    }

    internal bool Blocks(Vector3Int cell) => Puzzle.Boulders.Values.Any(p => p.ToCell() == cell);
    internal bool CanPush(Character actor, Vector3Int destination)
    {
        if (actor != game.PlayerController.ControlledAlly || actor.IsMovementBlocked || !Blocks(destination)) return false;
        var delta = destination - actor.TilemapPosition;
        if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1) return false;
        var beyond = destination + delta;
        return game.CurrentDungeon.IsWalkable(beyond) && game.CurrentDungeon.OverlapsAnyOtherCharacter(actor, Character.ToBounds(beyond)) == null;
    }

    internal bool Push(Character actor, Vector3Int destination)
    {
        if (!CanPush(actor, destination)) return false;
        bool before = CanExit;
        Puzzle.SetPlayerPosition(new GridPoint(actor.TilemapPosition.x, actor.TilemapPosition.y));
        if (!Puzzle.TryPush(new GridPoint(destination.x, destination.y))) return false;
        Progress.ObservePuzzle(Puzzle); RefreshVisuals(); AnnounceCompletion(before); return true;
    }

    internal void OnMoved(Character actor, Vector3Int from, Vector3Int to)
    {
        if (from == to) return;
        bool before = CanExit;
        if (actor == game.PlayerController.ControlledAlly)
        {
            if (AllowsReset && !CanExit)
            {
                if (witnessStep < floor.SolutionWitness.Count && floor.SolutionWitness[witnessStep].ToCell() == to) witnessStep++;
                else witnessDiverged = true;
            }
            Puzzle.ObservePlayerEntry(new GridPoint(to.x, to.y));
            Progress.ObservePosition(new GridPoint(to.x, to.y), floor);
        }
        else if (actor.GetComponent<EncounterActor>() is { Invulnerable: true })
            Puzzle.ObserveEnemyPosition(new GridPoint(to.x, to.y));
        Progress.ObservePuzzle(Puzzle);
        if (Puzzle.Complete && floor.Objective.Kind is DungeonObjectiveKind.TrapEnemy or DungeonObjectiveKind.GuideEnemy)
            foreach (var identity in actors.Values.Where(a => a != null && a.Invulnerable && !a.Resolved))
            {
                identity.Resolved = true;
                game.Enemies.Remove(identity.GetComponent<Enemy>());
                identity.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(identity.gameObject);
            }
        RefreshVisuals(); AnnounceCompletion(before);
    }

    internal GameAction EnemyAction(Enemy actor)
    {
        if (actor.GetComponent<EncounterActor>() is not { Invulnerable: true } identity) return null;
        if (identity.Resolved) return new WaitAction();
        var player = game.PlayerController.ControlledAlly.TilemapPosition;
        var next = PuzzleState.NextEnemyPosition(new GridPoint(player.x, player.y), new GridPoint(actor.TilemapPosition.x, actor.TilemapPosition.y),
            p => game.CurrentDungeon.IsWalkable(p.ToCell()) && game.CurrentDungeon.OverlapsAnyOtherCharacter(actor, Character.ToBounds(p.ToCell())) == null,
            floor.Objective.Kind == DungeonObjectiveKind.GuideEnemy).ToCell();
        return next == actor.TilemapPosition ? new WaitAction() : new MovementAction(actor, actor.TilemapPosition, next);
    }

    internal void Recovered()
    { bool before = CanExit; Progress.RecoverPossessions(); AnnounceCompletion(before); }

    internal GameAction AutoplayPuzzleAction(Ally ally)
    {
        if (!AllowsReset || CanExit) return null;
        if (floor.Objective.Kind is DungeonObjectiveKind.TrapEnemy or DungeonObjectiveKind.GuideEnemy)
            return PlanTargetStep(ally);
        if (witnessDiverged && game.CanResetFloor) return new ResetFloorAction();
        if (witnessStep >= floor.SolutionWitness.Count) return new WaitAction();
        var destination = floor.SolutionWitness[witnessStep].ToCell();
        var follower = game.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == destination);
        GameAction step = follower != null ? new SwapAllyPositionAction(ally,follower) : new MovementAction(ally,ally.TilemapPosition,destination);
        return step.IsValid(ally) ? step : new WaitAction();
    }

    GameAction PlanTargetStep(Ally ally)
    {
        var enemy = actors.Values.FirstOrDefault(a=>a!=null&&a.Invulnerable&&!a.Resolved)?.GetComponent<Enemy>();
        if (enemy == null) return new WaitAction();
        var destination = floor.PuzzleObjects.First(p=>p.Kind is PuzzleObjectKind.Trap or PuzzleObjectKind.Destination).Position;
        var initial = (Player:new GridPoint(ally.TilemapPosition.x,ally.TilemapPosition.y), Enemy:new GridPoint(enemy.TilemapPosition.x,enemy.TilemapPosition.y));
        var queue = new Queue<(GridPoint Player,GridPoint Enemy)>();queue.Enqueue(initial);
        var first = new Dictionary<(GridPoint Player,GridPoint Enemy),GridPoint> { [initial]=initial.Player };
        var followers = new HashSet<GridPoint>(game.Allies.Where(a=>a!=ally).Select(a=>new GridPoint(a.TilemapPosition.x,a.TilemapPosition.y)));
        while (queue.Count>0 && first.Count<24000)
        {
            var current=queue.Dequeue();
            if(current.Enemy.Equals(destination))
            {
                var cell=first[current].ToCell();
                var follower=game.Allies.FirstOrDefault(a=>a!=ally&&a.TilemapPosition==cell);
                GameAction action=follower!=null?new SwapAllyPositionAction(ally,follower):new MovementAction(ally,ally.TilemapPosition,cell);
                return action.IsValid(ally)?action:new WaitAction();
            }
            foreach(var player in PuzzleState.CardinalNeighbors(current.Player).Where(p=>floor.IsWalkable(p)&&!p.Equals(current.Enemy)))
            {
                // Replan after each real turn so ordinary follower movement and swaps remain authoritative.
                var nextEnemy=PuzzleState.NextEnemyPosition(player,current.Enemy,p=>floor.IsWalkable(p)&&!followers.Contains(p),floor.Objective.Kind==DungeonObjectiveKind.GuideEnemy);
                var next=(player,nextEnemy);
                if(first.ContainsKey(next))continue;
                first.Add(next,current.Equals(initial)?player:first[current]);queue.Enqueue(next);
            }
        }
        return new WaitAction();
    }

    internal Vector3Int? AutoplayDestination(Ally ally)
    {
        if (CanExit) return null;
        if (floor.Objective.Kind == DungeonObjectiveKind.RecoverPossessions)
            return game.CurrentDungeon.Interactables.OfType<EncounterStash>().FirstOrDefault(s => s.IsRecovery)?.Position;
        if (floor.Objective.Kind is DungeonObjectiveKind.DefeatBoss or DungeonObjectiveKind.DefeatEnemies)
        {
            var targets = actors.Values.Where(a => a != null && !a.Resolved).Select(a=>a.GetComponent<Enemy>()).Where(e=>e!=null).ToArray();
            return targets.SelectMany(e=>game.CurrentDungeon.GetWalkableNeighborhoodTiles(e.TilemapPosition)
                    .Concat(e.ToBounds().Cells().SelectMany(c=>new[] {c+Vector3Int.up,c+Vector3Int.down,c+Vector3Int.left,c+Vector3Int.right})))
                .Where(p=>game.CurrentDungeon.IsWalkable(p)&&game.CurrentDungeon.OverlapsAnyOtherCharacter(ally,Character.ToBounds(p),true)==null)
                .Where(p=>targets.Any(e=>e.ToBounds().Cells().Any(cell=>game.CurrentDungeon.CanWalkTo(p,cell))))
                .OrderBy(p=>TileWorldDungeon.ChevDistance(ally.TilemapPosition,p)).ThenBy(p=>p.x).ThenBy(p=>p.y).Cast<Vector3Int?>().FirstOrDefault();
        }
        return null;
    }

    void AnnounceCompletion(bool previous)
    { if (!previous && CanExit) GameMessages.Post("Objective complete. The exit is open."); }

    void RefreshVisuals()
    {
        foreach (var boulder in Puzzle.Boulders)
            if (visuals.TryGetValue(boulder.Key, out var go)) PositionMarker(game.CurrentDungeon, go, boulder.Value.ToCell(), .72f);
        foreach (var light in Puzzle.Lights)
            if (visuals.TryGetValue(light.Key, out var go)) go.GetComponent<Renderer>().material.color = light.Value ? Color.yellow : new Color(.15f,.17f,.25f);
    }

    internal static GameObject Marker(TileWorldDungeon dungeon, string name, Vector3Int cell, Color color, float height = .08f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(dungeon.transform, false);
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = color };
        go.AddComponent<EncounterMarkerMaterial>();
        PositionMarker(dungeon, go, cell, height); return go;
    }
    static void PositionMarker(TileWorldDungeon dungeon, GameObject go, Vector3Int cell, float height)
    {
        float size = (dungeon.CellToWorld(Vector3Int.right) - dungeon.CellToWorld(Vector3Int.zero)).x;
        go.transform.localScale = new Vector3(size * .8f, size * .8f, size * height);
        go.transform.position = dungeon.CellToWorld(cell) + new Vector3(size * .5f,size * .5f,DungeonPresentation.GroundPlaneZ - size * height * .5f - .02f);
    }
}

internal sealed class EncounterMarkerMaterial : MonoBehaviour
{
    void OnDestroy() { var renderer = GetComponent<Renderer>(); if (renderer != null) Destroy(renderer.sharedMaterial); }
}

public sealed class EncounterStash : Interactable
{
    bool recovery;
    internal bool IsRecovery => recovery;
    internal static void Create(TileWorldDungeon dungeon, Vector3Int cell, bool recovery)
    {
        var go = EncounterRuntime.Marker(dungeon, recovery ? "Possessions stash" : "Escape supplies", cell, recovery ? new Color(.7f,.4f,1) : Color.cyan, .45f);
        var stash = go.AddComponent<EncounterStash>(); stash.Position = cell; stash.recovery = recovery;
        dungeon.Interactables.Add(stash);
    }
    internal override string GetInteractionText() => recovery ? "Recover possessions" : "Take escape supplies";
    internal override List<GameAction> GetInteractionSideEffects(Character character)
    {
        if (character != Game.Instance.PlayerController.ControlledAlly) return new();
        return new() { new DynamicGameAction(c => {
            var game = Game.Instance;
            if (recovery)
            {
                game.Prison?.Recover(game); game.Prison?.TakeOverflow(game);
                if (game.Prison?.Recovered == true) game.Encounter?.Recovered();
            }
            else if (!Opened)
            {
                var supplies = Common.Instance.ItemManager.StartingItems.Where(i => i != null).Take(4).ToArray();
                foreach (var item in supplies) if (game.PlayerController.Inventory.CanAdd()) game.PlayerController.Inventory.Add(item.AsInventoryItem(null));
                Opened = true; GameMessages.Post("The party found basic escape supplies.");
            }
            return new();
        }, null, () => character == Game.Instance.PlayerController.ControlledAlly) };
    }
}
