using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal enum FastWalkStop
{
    None, CornerOrWall, Branch, RoomBoundary, EnemyVisible, PointOfInterest, PartyEvent,
    BlockedFollower, InputRequired, Interrupted, NoProgress, StepLimit
}

/// <summary>Runs ordinary dungeon turns up to the next decision, without intermediate movement playback.</summary>
internal sealed class DungeonFastWalk
{
    private readonly PlayerController player;
    private Ally leader;
    private TileWorldDungeon dungeon;
    private Vector3Int direction, previousInput, consumedInput;
    private bool consumed, pendingLaunch;
    private Vector3Int lastStepFrom;
    private readonly HashSet<Vector3Int> visited = new();
    private readonly HashSet<Vector3Int> sight = new();
    private readonly Dictionary<Ally, Vector3Int> followers = new();
    private readonly HashSet<Interactable> leavingPoints = new();
    internal bool Running { get; private set; }
    internal FastWalkStop StopReason { get; private set; }
    internal int Steps { get; private set; }
    internal double ElapsedSeconds { get; private set; }
    private bool partyEvent, showingFeedback;
    private double started;
    internal static DungeonFastWalk Active => Game.Instance?.PlayerController?.FastWalk;
    internal static bool SuppressPlayback => Active?.Running == true;
    internal static bool DeferPresentation => Active is { Running: true, showingFeedback: false };
    internal bool ShowsFeedback(GameAction action) => partyEvent && action.InterruptsWalking &&
        action is not MovementAction and not SwapAllyPositionAction and not WaitAction;

    internal DungeonFastWalk(PlayerController player) { this.player = player; }

    // The modifier may stay held between runs. Only a fresh direction launches another run.
    internal bool ReadInput(Vector2 move, bool fast, bool canLaunch)
    {
        var next = GridMovement.InputDirection(move);
        bool fresh = next != Vector3Int.zero && next != previousInput;
        previousInput = next;
        if (next != consumedInput) consumed = false;
        if (Running) { consumed = next != Vector3Int.zero; consumedInput = next; return true; }
        bool available = Game.Instance != null && Game.Instance.IsReady && Game.Instance.CurrentDungeon != null &&
            Game.Instance.Finale?.OnOverworld != true &&
            Game.Instance.CurrentDungeon.Floor.Variant != EternalEnigma.Core.World.DungeonVariant.Town;
        if (!fast || !available || !canLaunch || next == Vector3Int.zero) pendingLaunch = false;
        else if (fresh) pendingLaunch = true;
        if (pendingLaunch && Launch(next))
        {
            consumed = true; consumedInput = next;
            pendingLaunch = false;
        }
        return Running || consumed || fast && available && next != Vector3Int.zero;
    }

    internal bool Launch(Vector3Int next)
    {
        var game = Game.Instance;
        if (Running || game == null || !game.IsReady || !MissileTargeting.IsDirection(next) ||
            game.Finale?.OnOverworld == true || game.CurrentDungeon?.Floor.Variant == EternalEnigma.Core.World.DungeonVariant.Town ||
            game.TurnManager.IsProcessingTurn && !game.TurnManager.AwaitingCommand) return false;
        leader = player.ControlledAlly; dungeon = game.CurrentDungeon;
        if (leader == null || dungeon == null || leader.Vitals.HP <= 0) return false;
        direction = next; Steps = 0; StopReason = FastWalkStop.None;
        partyEvent = showingFeedback = false;
        visited.Clear(); visited.Add(leader.TilemapPosition);
        leavingPoints.Clear();
        foreach (var point in dungeon.Interactables)
            if (point != null && TileWorldDungeon.ChevDistance(leader.TilemapPosition, point.Position) <= 1 &&
                TileWorldDungeon.ChevDistance(leader.TilemapPosition + direction, point.Position) >
                TileWorldDungeon.ChevDistance(leader.TilemapPosition, point.Position)) leavingPoints.Add(point);
        started = Time.realtimeSinceStartupAsDouble;
        Running = true; player.StopHeldWalk();
        GameInputDiagnostics.Record("FastWalkStarted", leader);
        // A launch can supply the currently awaited full-control command, never another hero's command.
        if (game.TurnManager.AwaitingCommand) SubmitStep();
        return true;
    }

    internal void RequestStop(FastWalkStop reason)
    {
        if (Running && reason == FastWalkStop.PartyEvent) partyEvent = true;
        if (Running && StopReason == FastWalkStop.None) StopReason = reason;
    }

    internal void BeginFeedback()
    {
        if (!Running || showingFeedback) return;
        showingFeedback = true;
        Game.Instance.UpdateMiniMap();
        GameMessages.FinishAction();
        player.CameraController.SnapToFollowTarget();
    }

    internal void Tick()
    {
        if (!Running) return;
        var game = Game.Instance;
        if (game == null || game.CurrentDungeon != dungeon || leader == null)
        { RequestStop(FastWalkStop.Interrupted); Finish(); return; }
        if (player.ControlledAlly != leader)
        { RequestStop(FastWalkStop.InputRequired); Finish(); return; }
        if (MenuManager.Instance.Opened || Common.Instance.GlobalSettings.IsOpen || Common.Instance.Travel.IsTransitioning)
            RequestStop(FastWalkStop.InputRequired);
        if (game.TurnManager.AwaitingCommand)
        { RequestStop(FastWalkStop.InputRequired); Finish(); return; }
        if (game.TurnManager.IsProcessingTurn) return;
        // Bound long simulations while keeping a normal corridor traversal within a few frames.
        double deadline = Time.realtimeSinceStartupAsDouble + .025;
        try
        {
            do
            {
                if (StopReason != FastWalkStop.None) { Finish(); return; }
                if (Steps >= 1024) { RequestStop(FastWalkStop.StepLimit); Finish(); return; }
                SubmitStep();
                if (game.TurnManager.AwaitingCommand)
                { RequestStop(FastWalkStop.InputRequired); Finish(); return; }
                if (game.TurnManager.IsProcessingTurn) return;
            } while (Running && Time.realtimeSinceStartupAsDouble < deadline);
        }
        catch { RequestStop(FastWalkStop.Interrupted); Finish(); throw; }
    }

    private void SubmitStep()
    {
        using var measurement = new FinalePerformance.Scope("Fast walk step");
        var game = Game.Instance;
        var from = leader.TilemapPosition;
        RefreshSight();
        var reason = DecisionAt(from, Steps == 0);
        if (reason != FastWalkStop.None) { RequestStop(reason); return; }
        var to = from + direction;
        // An explicit restart can enter a nearby point, but must stop there even if picking it up removes it.
        bool enteringPoint = Steps == 0 && dungeon.Interactables.Any(item => IsVisibleStoppingPoint(item) && item.Position == to);
        var occupant = dungeon.OverlapsAnyOtherCharacter(leader, Character.ToBounds(leader.FootPrint, to));
        GameAction action;
        if (occupant == null) action = new MovementAction(leader, from, to) { ManualWalkCommand = true };
        else if (occupant is Ally ally && ally.Team == leader.Team && ally.CanMove())
            action = new SwapAllyPositionAction(leader, ally) { ManualWalkCommand = true };
        else { RequestStop(FastWalkStop.CornerOrWall); return; }
        followers.Clear();
        foreach (var member in game.Allies.Where(a => a != null && a != leader && !a.IsSummon && a.AllyStrategy != AllyStrategy.HoldPosition))
            followers[member] = member.TilemapPosition;
        lastStepFrom = from;
        leader.SetFacing(Character.GetFacing(direction));
        if (!game.TurnManager.SubmitCommand(leader, action)) { RequestStop(FastWalkStop.NoProgress); return; }
        Steps++;
        if (enteringPoint) RequestStop(FastWalkStop.PointOfInterest);
        // The turn may finish synchronously; otherwise TurnCompleted performs the same checks.
    }

    internal void TurnCompleted()
    {
        if (!Running || leader == null) return;
        if (!visited.Add(leader.TilemapPosition)) RequestStop(FastWalkStop.NoProgress);
        foreach (var pair in followers)
            if (pair.Key != null && pair.Key.Vitals.HP > 0 && pair.Key.TilemapPosition == pair.Value &&
                TileWorldDungeon.ChevDistance(pair.Key.TilemapPosition, leader.TilemapPosition) > 1)
                RequestStop(FastWalkStop.BlockedFollower);
        RefreshSight();
        RequestStop(DecisionAt(leader.TilemapPosition, false));
    }

    private void RefreshSight()
    {
        sight.Clear();
        foreach (var ally in Game.Instance.Allies)
            if (ally != null && ally.Vitals.HP > 0) sight.UnionWith(dungeon.GetVisibleTiles(ally, ally.TilemapPosition));
    }

    private bool IsVisibleStoppingPoint(Interactable item)
    {
        if (item == null || !sight.Contains(item.Position)) return false;
        if (item is Trap trap && (trap.VisualObject == null || !trap.VisualObject.activeSelf)) return false;
        if (item is DungeonProp prop && !prop.IsClosedDoor && !dungeon.IsHazard(prop.Position) &&
            (prop.Opened || prop.Definition.Kind != EternalEnigma.Core.World.DungeonSceneryKind.Container)) return false;
        return true;
    }

    internal FastWalkStop DecisionAt(Vector3Int cell, bool launching)
    {
        if (leader.IsMovementBlocked || leader.PendingCast != null || leader.Vitals.HP <= 0)
            return FastWalkStop.PartyEvent;
        if (Game.Instance.Allies.Any(a => a != null && a.AttributePromptPending)) return FastWalkStop.InputRequired;
        foreach (var enemy in Game.Instance.AllCharacters)
            if (enemy != null && enemy.Vitals.HP > 0 && enemy.Team != leader.Team && enemy.Team != Team.Neutral &&
                !EnemyBehavior.IsDisguised(enemy) && !EncounterActor.Protected(enemy) &&
                enemy.GetComponent<FinaleActor>()?.Hidden != true &&
                Character.ToBounds(enemy.FootPrint, enemy.TilemapPosition).Cells().Any(sight.Contains))
                return FastWalkStop.EnemyVisible;
        foreach (var item in dungeon.Interactables)
        {
            // The first step is the player's new decision; proximity stops apply again after that step.
            if (launching || !IsVisibleStoppingPoint(item) || leavingPoints.Contains(item) ||
                TileWorldDungeon.ChevDistance(cell, item.Position) > 1) continue;
            return FastWalkStop.PointOfInterest;
        }
        var to = cell + direction;
        if (!launching && dungeon.IsHallway(cell))
        {
            // A one-cell bend has one way forward. A junction needs a new command.
            Vector3Int continuation = default;
            int exits = 0;
            foreach (var next in GridMovement.GetNeighbors(cell, p => sight.Contains(p) && dungeon.IsFloorCell(p)))
            {
                if (next == lastStepFrom) continue;
                if (++exits > 1) return FastWalkStop.Branch;
                continuation = next;
            }
            if (exits == 0 || visited.Contains(continuation)) return FastWalkStop.CornerOrWall;
            if (!dungeon.IsHallway(continuation)) return FastWalkStop.RoomBoundary;
            to = continuation;
            direction = to - cell;
        }
        if (!sight.Contains(to) || !MovementAction.FootprintCanStep(leader, cell, to))
            return FastWalkStop.CornerOrWall;
        return FastWalkStop.None;
    }

    internal static void PartyEvent(Character target)
    {
        if (target is Ally || target != null && target.Team == Team.Player) Active?.RequestStop(FastWalkStop.PartyEvent);
    }

    internal static void ObserveAction(GameAction action, Character actor)
    {
        if (!SuppressPlayback || !action.InterruptsWalking ||
            action is MovementAction or SwapAllyPositionAction or WaitAction) return;
        if (actor is Ally || actor.Team == Team.Player || action.VisualTargets.Any(t => t is Ally || t != null && t.Team == Team.Player))
            Active.RequestStop(FastWalkStop.PartyEvent);
    }

    internal void Finish()
    {
        if (!Running) return;
        Running = false;
        HudRefresh.Request(HudChange.All);
        ElapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
        player.StopHeldWalk();
        var game = Game.Instance;
        if (game != null && game.CurrentDungeon == dungeon)
        {
            foreach (var ally in game.Allies)
                if (ally != null && ally.Vitals.HP > 0) ally.PlayIdleAnimation();
            GameMessages.FinishAction();
            game.UpdateMiniMap();
            player.CameraController.SnapToFollowTarget();
        }
        GameInputDiagnostics.Record("FastWalkStopped:" + StopReason, leader);
    }
}
