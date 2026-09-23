namespace EternalEnigma.Core.World;

public readonly struct Placement : IEquatable<Placement>
{
    public GridPoint Cell { get; }
    public int Roll { get; }

    public Placement(GridPoint cell, int roll)
    {
        if (roll < 0)
            throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be non-negative.");
        Cell = cell;
        Roll = roll;
    }

    public bool Equals(Placement other) => Cell.Equals(other.Cell) && Roll == other.Roll;

    public override bool Equals(object? obj) => obj is Placement other && Equals(other);

    public override int GetHashCode() => unchecked(Cell.GetHashCode() * 397 ^ Roll);

    public override string ToString() => $"{Cell}#{Roll}";
}
