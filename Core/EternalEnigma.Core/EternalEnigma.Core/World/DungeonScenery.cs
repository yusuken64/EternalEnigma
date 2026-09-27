namespace EternalEnigma.Core.World;

public enum DungeonSceneryKind { Container, Destructible, Hazard }
public enum SceneryReward { None, Gold, Item }

public sealed class DungeonScenery
{
    public GridPoint Cell { get; }
    public DungeonSceneryKind Kind { get; }
    public int HitPoints { get; }
    public int Roll { get; }
    public SceneryReward Reward { get; }
    public DungeonScenery(GridPoint cell, DungeonSceneryKind kind, int hitPoints, int roll, SceneryReward reward)
    { Cell = cell; Kind = kind; HitPoints = hitPoints; Roll = roll; Reward = reward; }
}
