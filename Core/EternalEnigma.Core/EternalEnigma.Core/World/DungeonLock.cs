namespace EternalEnigma.Core.World;

/// <summary>
/// A soft lock: a one-cell door sealing an optional side room. The Start→Stairs route never crosses it,
/// so a small key, lockpicking or bashing the door are all optional. Keys belong to the dungeon run,
/// not the floor, so a key from this floor may open a door on a later one.
/// </summary>
public sealed class DungeonLock
{
    public GridPoint Door { get; }
    /// <summary>Damage needed to bash the door open without a key.</summary>
    public int HitPoints { get; }
    /// <summary>Cells reachable only through <see cref="Door"/>.</summary>
    public IReadOnlyList<GridPoint> Vault { get; }
    public DungeonLock(GridPoint door, int hitPoints, IEnumerable<GridPoint> vault)
    { Door = door; HitPoints = hitPoints; Vault = Array.AsReadOnly(vault.ToArray()); }
}
