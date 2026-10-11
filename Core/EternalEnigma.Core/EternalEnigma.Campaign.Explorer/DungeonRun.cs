using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public sealed class DungeonRun
{
    private readonly OverworldBiome biome;
    private readonly int layoutVersion;
    public int CampaignSeed { get; }
    public CampaignLocation Location { get; }
    public int Floor { get; private set; }
    public (int Start, int End) Floors { get; }
    public DungeonFloor Current { get; private set; }
    public DungeonRunPlan Plan { get; }
    public PuzzleState Puzzle { get; private set; }
    public DungeonObjectiveProgress Objective { get; private set; }
    public GridPoint Position { get; internal set; }
    public string Message { get; private set; } = "";
    public HashSet<GridPoint> Visited { get; } = new();

    public DungeonRun(int campaignSeed, CampaignLocation location, OverworldBiome biome = OverworldBiome.Grassland, int layoutVersion = 0, int? initialFloor = null)
    {
        this.biome=biome; this.layoutVersion=layoutVersion;
        CampaignSeed = campaignSeed;
        Location = location;
        Plan = new DungeonRunPlan(campaignSeed, location, biome, layoutVersion);
        Floors = (Plan.StartFloor, Plan.EndFloor);
        Floor = initialFloor ?? Floors.Start;
        Current = DungeonFloorGenerator.Generate(Plan.Floor(Floor).Options);
        Puzzle = new PuzzleState(Current); Objective = new DungeonObjectiveProgress(Current);
        Position = Current.Start;
        Visited.Add(Position);
    }

    public bool Move(int dx, int dy)
    {
        var target = new GridPoint(Position.X + dx, Position.Y + dy);

        if (Current.Objective.AllowsReset)
        {
            Puzzle.SetPlayerPosition(Position);
            if (!Puzzle.TryMove(target)) { Message = "Blocked."; return false; }
            Position = Puzzle.Player; Visited.Add(Position); Objective.ObservePuzzle(Puzzle);
            Message = Objective.CanExit ? "Objective complete. Find the exit." : Current.Objective.Kind.ToString(); return true;
        }

        if (!Current.CanStep(Position, target) || !GridSteps.CanStep(Position, target, p=>Current.IsWalkable(p) && !Current.Scenery.Any(s=>s.Cell.Equals(p) && s.Kind!=DungeonSceneryKind.Hazard),DiagonalRule.RequireOpenSides))
        {
            Message = "Blocked.";
            return false;
        }

        Position = target;
        Visited.Add(Position);
        Objective.ObservePosition(Position, Current);

        if (OnStairs)
        {
            Message = IsLastFloor ? "Stairs | Enter: finish dungeon" : "Stairs | Enter: descend";
        }
        else
        {
            Message = "";
        }

        return true;
    }

    public bool OnStairs => Position.Equals(Current.Stairs);

    public bool IsLastFloor => Floor == Floors.End;
    public bool CanExit => Objective.CanExit;
    public bool Interact()
    {
        var enemy = Current.ObjectiveEnemies.FirstOrDefault(e => !e.Invulnerable && Objective.RemainingEnemyIds.Contains(e.Id) &&
            Math.Abs(e.Position.X - Position.X) + Math.Abs(e.Position.Y - Position.Y) <= 1);
        if (enemy != null) { Objective.Defeat(enemy.Id); Message = "Simulated defeat: " + enemy.Id; return true; }
        if (Current.RecoveryStashes.Any(s => Math.Abs(s.Position.X - Position.X) + Math.Abs(s.Position.Y - Position.Y) <= 1))
        { Objective.RecoverPossessions(); Message = "Possessions recovered."; return true; }
        return false;
    }
    public bool Reset()
    {
        if (!Current.Objective.AllowsReset) return false;
        Current = DungeonFloorGenerator.Generate(Plan.Floor(Floor).Options);
        Puzzle = new PuzzleState(Current); Objective = new DungeonObjectiveProgress(Current); Position = Current.Start;
        Visited.Clear(); Visited.Add(Position); Message = "Floor reset."; return true;
    }

    public void Descend()
    {
        if (IsLastFloor)
            throw new InvalidOperationException("Cannot descend from the last floor.");
        if (!CanExit) throw new InvalidOperationException("The floor objective is unfinished.");

        Floor++;
        Current = DungeonFloorGenerator.Generate(Plan.Floor(Floor).Options);
        Puzzle = new PuzzleState(Current); Objective = new DungeonObjectiveProgress(Current);
        Position = Current.Start;
        Visited.Clear();
        Visited.Add(Position);
    }
}
