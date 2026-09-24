namespace EternalEnigma.Core.World;

public readonly struct GatheringSite : IEquatable<GatheringSite>
{
    public GridPoint Cell { get; }
    public GatheringKind Kind { get; }
    public int Roll { get; }

    public GatheringSite(GridPoint cell, GatheringKind kind, int roll)
    {
        if (!Enum.IsDefined(typeof(GatheringKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (roll < 0)
            throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be non-negative.");
        Cell = cell; Kind = kind; Roll = roll;
    }

    public bool Equals(GatheringSite other) => Cell.Equals(other.Cell) && Kind == other.Kind && Roll == other.Roll;
    public override bool Equals(object? obj) => obj is GatheringSite other && Equals(other);
    public override int GetHashCode() => unchecked((Cell.GetHashCode() * 397 ^ (int)Kind) * 397 ^ Roll);
    public override string ToString() => $"{Cell}:{Kind}#{Roll}";
}
