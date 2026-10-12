using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable]
public sealed class AutoplayFinaleFloorReport
{
    public int Floor, Seed;
    public string Fingerprint;
    public List<AutoplayFinaleRegionReport> Regions = new();
}
[Serializable]
public sealed class AutoplayFinaleRegionReport
{
    public string Id, Biome;
    public int Planned, Registered, Remaining;
    public bool Cleared;
}
[Serializable]
public sealed class AutoplayFinaleReport
{
    public int PlanVersion, CurrentFloor;
    public bool DirectStart, PristineStart, FinalExitCompleted, HomecomingCompleted;
    public List<AutoplayFinaleFloorReport> Floors = new();
    public string[] DefeatedEnemies, RevealedMimics, ClearedRegions, MimicFamilies;
    public List<string> GateOpenings = new();
    public int[] StairTransitions;
    public int MapMovementCommands, WarpSelections;
}

public sealed partial class AutoplayRunner
{
    readonly Queue<GridPoint> finaleSteps = new();
    string finaleChoice;
    int finaleNavigationRevision = -1;
    public bool IsFinaleDebug => Options?.Mode == AutoplayMode.Finale;
    void ResetFinaleRoute() { finaleSteps.Clear(); finaleChoice = null; finaleNavigationRevision = -1; }

    internal static void CaptureFinaleState(FinaleSession session) => Active?.CaptureFinale(session);
    internal static void FinaleEvent(string kind, string id, string family = null)
    {
        var run = Active;
        if (run == null || !run.Running) return;
        run.CaptureFinale(Common.Instance?.CampaignContext?.Finale);
        if (kind == "RegionOpened" && run.Report.Finale != null) run.Report.Finale.GateOpenings.Add(id);
        if (kind == "HomecomingCompleted" && run.Report.Finale != null) run.Report.Finale.HomecomingCompleted = true;
        run.WriteEvent(new AutoplayEvent { Kind = kind, TargetId = id, Message = family });
    }
    void CaptureFinale(FinaleSession session)
    {
        if (session == null) return;
        var report = Report.Finale;
        if (report == null)
        {
            report = Report.Finale = new AutoplayFinaleReport { PlanVersion = FinalePlan.Version, DirectStart = IsFinaleDebug,
                PristineStart = session.CurrentFloor == 1 && session.Defeated.Count == 0 && session.Revealed.Count == 0 &&
                    session.Unlocked.Count == 1 && !session.FinalExitCompleted };
            foreach (var floor in session.Plan.Floors)
                report.Floors.Add(new AutoplayFinaleFloorReport { Floor = floor.Number, Seed = floor.Seed, Fingerprint = floor.Fingerprint,
                    Regions = floor.Regions.Select(r => new AutoplayFinaleRegionReport { Id = r.Id, Biome = r.Biome.ToString(), Planned = r.Enemies.Count }).ToList() });
        }
        report.CurrentFloor = session.CurrentFloor;
        report.DefeatedEnemies = session.Defeated.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.RevealedMimics = session.Revealed.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.ClearedRegions = session.ClearedRegions.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.MimicFamilies = session.Plan.Floors.SelectMany(f => f.Enemies).Where(e => e.IsMimic && session.Revealed.Contains(e.Id))
            .Select(e => session.MimicFamily(e.Id)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.StairTransitions = session.StairTransitions.ToArray(); report.FinalExitCompleted = session.FinalExitCompleted;
        foreach (var region in report.Floors.SelectMany(f => f.Regions))
        {
            region.Registered = session.EnemyRegions.Count(p => p.Value == region.Id);
            region.Remaining = session.RemainingIn(region.Id);
            region.Cleared = session.ClearedRegions.Contains(region.Id);
        }
    }
    bool SelectFinaleDestination(FinaleRuntime finale)
    {
        if (!finale.SelectDestination(finaleChoice)) return false;
        Report.Finale.WarpSelections++; Log("Finale destination " + finaleChoice); finaleChoice = null; return true;
    }
    void FinaleTick(Game game, Ally ally)
    {
        var finale = game.Finale; var session = finale.Session;
        CaptureFinale(session);
        var interaction = NearbyMimicInteraction(game, ally);
        if (interaction != null) { finaleSteps.Clear(); Act(ally, interaction, "Enter disguised location"); return; }
        var skills = new AllySkillPolicy(game, ally, 0) { IncludeControlledAlly = true };
        if (skills.ShouldRun())
        {
            var cast = skills.GetActions().FirstOrDefault();
            if (cast != null) { finaleSteps.Clear(); Act(ally, cast, "Finale skill " + skills.LastChoice.Option.Skill.SkillName); return; }
        }
        var attack = new AllyAttackPolicy(game, ally, 0);
        if (attack.ShouldRun())
        {
            var action = attack.GetActions().FirstOrDefault(a => a.IsValid(ally));
            if (action != null) { finaleSteps.Clear(); Act(ally, action, "Finale attack"); return; }
        }
        var position = ally.TilemapPosition.ToGridPoint();
        if (session.FloorCleared && position.Equals(session.Floor.Stairs))
        { Act(ally, new InteractAction(game.CurrentDungeon.Interactables.OfType<Stairs>().Single()), "Ascend finale stairs"); return; }
        if (finaleSteps.Count > 0 && finaleSteps.Peek().Equals(position)) finaleSteps.Dequeue();
        int revision = session.Defeated.Count + session.Revealed.Count;
        if (revision != finaleNavigationRevision) { finaleSteps.Clear(); finaleNavigationRevision = revision; }
        if (finaleSteps.Count == 0)
        {
            var path = FinalePath(game, ally);
            if (path == null) { Finish("Stalled", "No reachable finale enemy or stairs. " + finale.ObjectiveText); return; }
            foreach (var step in path) finaleSteps.Enqueue(step);
        }
        if (finaleSteps.Count == 0) { Act(ally, new WaitAction(), "Wait for finale combat"); return; }
        var next = finaleSteps.Peek();
        if (!session.CanStep(position, next))
        {
            var warp = session.WarpDestinations(position).FirstOrDefault(w => w.Destination.Equals(next));
            if (warp.RouteId == null) { finaleSteps.Clear(); return; }
            finaleChoice = "warp/" + warp.RouteId;
            var portal = game.CurrentDungeon.GetInteractable(ally.TilemapPosition) as FinalePortal;
            if (portal == null) { Finish("Unsupported", "Missing finale warp interaction."); return; }
            Act(ally, new InteractAction(portal), "Open finale warp"); return;
        }
        var cell = next.ToCell();
        var other = game.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == cell);
        ally.SetFacingByTargetPosition(cell);
        GameAction movement = other == null ? new MovementAction(ally, ally.TilemapPosition, cell) : new SwapAllyPositionAction(ally, other);
        if (!movement.IsValid(ally)) { finaleSteps.Clear(); Act(ally, new WaitAction(), "Replan blocked finale path"); return; }
        Report.Finale.MapMovementCommands++; Act(ally, movement, "Finale move " + next);
    }
    internal static List<GridPoint> FinalePath(Game game, Ally ally)
    {
        using var measurement = new FinalePerformance.Scope("Autoplay routes");
        var session = game.Finale.Session; var from = ally.TilemapPosition.ToGridPoint();
        var blocked = game.Enemies.Where(e => e != null && e.Vitals.HP > 0).SelectMany(e => e.ToBounds().Cells()).Select(p => p.ToGridPoint()).ToHashSet();
        bool Walk(GridPoint p) => session.IsWalkable(p) && !blocked.Contains(p);
        var goals = session.FloorCleared ? new HashSet<GridPoint> { session.Floor.Stairs } :
            blocked.SelectMany(p => GridSteps.EightWay.Select(d => new GridPoint(p.X + d.X, p.Y + d.Y))
                .Where(q => Walk(q) && session.CanStep(q, p))).ToHashSet();
        var queue = new Queue<GridPoint>(); var previous = new Dictionary<GridPoint, GridPoint> { [from] = from };
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (goals.Contains(cell))
            {
                var path = new List<GridPoint>();
                while (!cell.Equals(from)) { path.Add(cell); cell = previous[cell]; }
                path.Reverse(); return path;
            }
            var neighbors = GridSteps.EightWay.Select(d => new GridPoint(cell.X + d.X, cell.Y + d.Y))
                .Where(p => session.CanStep(cell, p) && Walk(p))
                .Concat(session.WarpDestinations(cell).Select(w => w.Destination).Where(Walk));
            foreach (var next in neighbors) if (!previous.ContainsKey(next)) { previous.Add(next, cell); queue.Enqueue(next); }
        }
        return null;
    }
}
