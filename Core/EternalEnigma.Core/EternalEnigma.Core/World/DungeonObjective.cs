namespace EternalEnigma.Core.World;

public enum DungeonObjectiveKind { ReachExit, DefeatEnemies, DefeatBoss, PositionObjects, MatchLights, TrapEnemy, GuideEnemy, RecoverPossessions }
public enum PuzzleObjectKind { Boulder, Goal, Light, LightTarget, Trap, Destination, BasicSupplies }

public sealed class DungeonObjectiveDefinition : IEquatable<DungeonObjectiveDefinition>
{
    public DungeonObjectiveKind Kind { get; }
    public bool AllowsReset => Kind == DungeonObjectiveKind.PositionObjects || Kind == DungeonObjectiveKind.MatchLights || Kind == DungeonObjectiveKind.TrapEnemy || Kind == DungeonObjectiveKind.GuideEnemy;
    public DungeonObjectiveDefinition(DungeonObjectiveKind kind = DungeonObjectiveKind.ReachExit) { Kind = kind; }
    public bool Equals(DungeonObjectiveDefinition? other) => other != null && Kind == other.Kind;
    public override bool Equals(object? obj) => Equals(obj as DungeonObjectiveDefinition);
    public override int GetHashCode() => (int)Kind;
}

public sealed class PuzzleObjectPlacement
{
    public string Id { get; }
    public PuzzleObjectKind Kind { get; }
    public GridPoint Position { get; }
    public bool InitialState { get; }
    public bool TargetState { get; }
    public PuzzleObjectPlacement(string id, PuzzleObjectKind kind, GridPoint position, bool initialState = false, bool targetState = false)
    { Id = id; Kind = kind; Position = position; InitialState = initialState; TargetState = targetState; }
}

public sealed class ObjectiveEnemyPlacement
{
    public string Id { get; }
    public GridPoint Position { get; }
    public string Family { get; }
    public bool Invulnerable { get; }
    public int Footprint { get; }
    public bool IsBoss => Footprint == 3;
    public ObjectiveEnemyPlacement(string id, GridPoint position, string family = "", bool invulnerable = false, int footprint = 1)
    { Id = id; Position = position; Family = family; Invulnerable = invulnerable; Footprint = footprint; }
}

public sealed class DungeonExitPlacement
{
    public string Id { get; }
    public GridPoint Position { get; }
    public DungeonExitPlacement(string id, GridPoint position) { Id = id; Position = position; }
}

public sealed class RecoveryStashPlacement
{
    public string Id { get; }
    public GridPoint Position { get; }
    public RecoveryStashPlacement(string id, GridPoint position) { Id = id; Position = position; }
}

/// <summary>Stable objective identities survive conversion/despawn attempts and include required reinforcements.</summary>
public sealed class DungeonObjectiveProgress
{
    private readonly HashSet<string> remaining;
    private readonly HashSet<string> required;
    public DungeonObjectiveDefinition Definition { get; }
    public bool Complete { get; private set; }
    public IReadOnlyCollection<string> RemainingEnemyIds => Array.AsReadOnly(remaining.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    public DungeonObjectiveProgress(DungeonFloor floor)
    {
        Definition = floor.Objective;
        required = new HashSet<string>(floor.ObjectiveEnemies.Select(e => e.Id), StringComparer.Ordinal);
        remaining = new HashSet<string>(required, StringComparer.Ordinal);
    }
    public bool IsRequired(string id) => required.Contains(id);
    public bool RegisterReinforcement(string id)
    {
        if (Complete || string.IsNullOrWhiteSpace(id) || !required.Add(id)) return false;
        remaining.Add(id); return true;
    }
    public bool Defeat(string id)
    {
        if (Definition.Kind != DungeonObjectiveKind.DefeatEnemies && Definition.Kind != DungeonObjectiveKind.DefeatBoss) return false;
        if (!remaining.Remove(id)) return false;
        Complete |= remaining.Count == 0; return true;
    }
    public void ObservePosition(GridPoint position, DungeonFloor floor) { if (Definition.Kind == DungeonObjectiveKind.ReachExit && position.Equals(floor.Stairs)) Complete = true; }
    public void ObservePuzzle(PuzzleState state) { if (Definition.AllowsReset && state.Complete) Complete = true; }
    public void RecoverPossessions() { if (Definition.Kind == DungeonObjectiveKind.RecoverPossessions) Complete = true; }
    public bool CanExit => Definition.Kind == DungeonObjectiveKind.ReachExit || Complete;
}
