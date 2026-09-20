namespace EternalEnigma.Core.World;

public readonly struct GridPoint : IEquatable<GridPoint>
{
    public int X { get; }
    public int Y { get; }
    public GridPoint(int x, int y) { X = x; Y = y; }
    public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
    public override string ToString() => $"({X},{Y})";
}

/// <summary>Eight-way, one-tile movement with sealing corners. Shared by runtime queries and validation.</summary>
public static class OverworldMovement
{
    public static bool CanStep(GridPoint from, GridPoint to, Func<GridPoint, bool> passable)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0) || !passable(from) || !passable(to)) return false;
        return dx == 0 || dy == 0 || (passable(new GridPoint(from.X + dx, from.Y)) && passable(new GridPoint(from.X, from.Y + dy)));
    }
    public static IEnumerable<GridPoint> Neighbors(GridPoint cell)
    {
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (x != 0 || y != 0) yield return new GridPoint(cell.X + x, cell.Y + y);
    }
}
