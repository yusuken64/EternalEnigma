using System.Collections.ObjectModel;

namespace EternalEnigma.Core.World;

/// <summary>Controlled-character puzzle actions and the exact deterministic target policy used by generation and hosts.</summary>
public sealed class PuzzleState
{
    private readonly DungeonFloor floor;
    private readonly Dictionary<string, GridPoint> boulders;
    private readonly Dictionary<string, bool> lights;
    public GridPoint Player { get; private set; }
    public GridPoint? Enemy { get; private set; }
    public bool Complete { get; private set; }
    public IReadOnlyDictionary<string, GridPoint> Boulders => new ReadOnlyDictionary<string, GridPoint>(boulders);
    public IReadOnlyDictionary<string, bool> Lights => new ReadOnlyDictionary<string, bool>(lights);
    public PuzzleState(DungeonFloor floor)
    {
        this.floor = floor; Player = floor.Start;
        boulders = floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Boulder).ToDictionary(p => p.Id, p => p.Position, StringComparer.Ordinal);
        lights = floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Light).ToDictionary(p => p.Id, p => p.InitialState, StringComparer.Ordinal);
        Enemy = floor.ObjectiveEnemies.FirstOrDefault(e => e.Invulnerable)?.Position;
        Evaluate();
    }
    public bool TryMove(GridPoint destination, bool controlledCharacter = true, bool advanceEnemy = true)
    {
        if (!controlledCharacter) return false;
        bool Free(GridPoint p) => floor.IsWalkable(p) && !boulders.Values.Contains(p) && (!Enemy.HasValue || !Enemy.Value.Equals(p));
        var boulder = boulders.FirstOrDefault(b => b.Value.Equals(destination));
        if (boulder.Key != null)
        {
            int dx = destination.X - Player.X, dy = destination.Y - Player.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            var pushed = new GridPoint(destination.X + dx, destination.Y + dy);
            if (!Free(pushed)) return false;
            boulders[boulder.Key] = pushed;
        }
        else if (!GridSteps.CanStep(Player, destination, Free, DiagonalRule.RequireOpenSides)) return false;
        Player = destination;
        foreach (var tile in floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Light && p.Position.Equals(destination))) lights[tile.Id] = !lights[tile.Id];
        if (advanceEnemy) AdvanceEnemy();
        Evaluate(); return true;
    }
    /// <summary>Host synchronization after switching the controlled party member; does not trigger an entry action.</summary>
    public void SetPlayerPosition(GridPoint position) { if (!floor.IsWalkable(position)) throw new ArgumentException("Player must be on the floor."); Player = position; }
    /// <summary>Observe an actual controlled-character entry, including displacement and movement skills.</summary>
    public void ObservePlayerEntry(GridPoint destination)
    {
        SetPlayerPosition(destination);
        foreach (var tile in floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Light && p.Position.Equals(destination))) lights[tile.Id] = !lights[tile.Id];
        Evaluate();
    }
    public void ObserveEnemyPosition(GridPoint position) { if (!floor.IsWalkable(position)) throw new ArgumentException("Target must be on the floor."); Enemy = position; Evaluate(); }
    public bool TryPush(GridPoint boulder)
    {
        var entry = boulders.FirstOrDefault(b => b.Value.Equals(boulder));
        int dx = boulder.X - Player.X, dy = boulder.Y - Player.Y;
        var target = new GridPoint(boulder.X + dx, boulder.Y + dy);
        if (entry.Key == null || Math.Abs(dx) + Math.Abs(dy) != 1 || !floor.IsWalkable(target) || boulders.Values.Contains(target) || Enemy?.Equals(target) == true) return false;
        boulders[entry.Key] = target; Evaluate(); return true;
    }
    public void AdvanceEnemy()
    {
        if (!Enemy.HasValue || Complete) return;
        Enemy = NextEnemyPosition(Player, Enemy.Value, p => floor.IsWalkable(p) && !boulders.Values.Contains(p), floor.Objective.Kind == DungeonObjectiveKind.GuideEnemy);
        Evaluate();
    }
    public static GridPoint NextEnemyPosition(GridPoint player, GridPoint enemy, Func<GridPoint, bool> walkable, bool fleeing)
    {
        var candidates = CardinalNeighbors(enemy).Where(p => walkable(p) && !p.Equals(player)).ToArray();
        if (candidates.Length == 0) return enemy;
        int Manhattan(GridPoint p) => Math.Abs(p.X - player.X) + Math.Abs(p.Y - player.Y);
        if (fleeing)
        {
            // Move only farther away. East, north, west, south is the stable tie break.
            var best = candidates.OrderByDescending(Manhattan).First();
            return Manhattan(best) > Manhattan(enemy) ? best : enemy;
        }
        var distances = GridSearch.Distances(player, p => CardinalNeighbors(p).Where(walkable));
        var reachable = candidates.Where(distances.ContainsKey).OrderBy(p => distances[p]).ToArray();
        return reachable.Length == 0 ? enemy : reachable[0];
    }
    public static IEnumerable<GridPoint> CardinalNeighbors(GridPoint p)
    {
        yield return new GridPoint(p.X + 1, p.Y); yield return new GridPoint(p.X, p.Y + 1);
        yield return new GridPoint(p.X - 1, p.Y); yield return new GridPoint(p.X, p.Y - 1);
    }
    private void Evaluate()
    {
        if (Complete) return;
        switch (floor.Objective.Kind)
        {
            case DungeonObjectiveKind.PositionObjects:
                var goals = floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Goal).Select(p => p.Position).ToArray();
                Complete = goals.Length > 0 && goals.All(boulders.Values.Contains); break;
            case DungeonObjectiveKind.MatchLights:
                Complete = lights.Count > 0 && floor.PuzzleObjects.Where(p => p.Kind == PuzzleObjectKind.Light).All(p => lights[p.Id] == p.TargetState); break;
            case DungeonObjectiveKind.TrapEnemy:
            case DungeonObjectiveKind.GuideEnemy:
                Complete = Enemy.HasValue && floor.PuzzleObjects.Any(p => (p.Kind == PuzzleObjectKind.Trap || p.Kind == PuzzleObjectKind.Destination) && p.Position.Equals(Enemy.Value)); break;
        }
    }
    public static bool ValidateWitness(DungeonFloor floor)
    {
        if (!floor.Objective.AllowsReset) return true;
        var state = new PuzzleState(floor);
        foreach (var step in floor.SolutionWitness) if (!state.TryMove(step)) return false;
        return state.Complete;
    }
}
