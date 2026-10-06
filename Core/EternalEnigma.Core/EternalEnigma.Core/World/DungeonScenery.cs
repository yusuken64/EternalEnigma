namespace EternalEnigma.Core.World;

// Values are appended only. Door is never in DungeonFloor.Scenery; presentation builds it from DungeonFloor.Locks.
public enum DungeonSceneryKind { Container, Destructible, Hazard, Door }
public enum SceneryReward { None, Gold, Item }

public sealed class DungeonScenery
{
    public GridPoint Cell { get; }
    public DungeonSceneryKind Kind { get; }
    public int HitPoints { get; }
    public int Roll { get; }
    public SceneryReward Reward { get; }
    /// <summary>Drops a small key when opened or broken, in addition to <see cref="Reward"/>.</summary>
    public bool HoldsKey { get; }
    public DungeonScenery(GridPoint cell, DungeonSceneryKind kind, int hitPoints, int roll, SceneryReward reward, bool holdsKey = false)
    { Cell = cell; Kind = kind; HitPoints = hitPoints; Roll = roll; Reward = reward; HoldsKey = holdsKey; }
    internal DungeonScenery WithKey() => new(Cell, Kind, HitPoints, Roll, Reward, true);
}
