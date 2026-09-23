namespace EternalEnigma.Core.World;

public readonly struct GridRect : IEquatable<GridRect>
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public GridRect(int x, int y, int width, int height)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), "Width and height must be at least 1.");
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Right => X + Width - 1;
    public int Top => Y + Height - 1;
    public GridPoint Center => new GridPoint(X + Width / 2, Y + Height / 2);

    public bool Contains(GridPoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Top;

    public bool Intersects(GridRect other, int margin = 0)
    {
        // Both rectangles are expanded by margin, then checked for overlap
        int thisLeft = X - margin;
        int thisRight = Right + margin;
        int thisTop = Top + margin;
        int thisY = Y - margin;

        int otherLeft = other.X - margin;
        int otherRight = other.Right + margin;
        int otherTop = other.Top + margin;
        int otherY = other.Y - margin;

        return !(thisRight < otherLeft || thisLeft > otherRight || thisTop < otherY || thisY > otherTop);
    }

    public IEnumerable<GridPoint> Cells()
    {
        for (int x = X; x <= Right; x++)
        for (int y = Y; y <= Top; y++)
            yield return new GridPoint(x, y);
    }

    public bool Equals(GridRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is GridRect other && Equals(other);
    public override int GetHashCode() => unchecked(X * 397 ^ Y * 397 ^ Width * 397 ^ Height);
    public override string ToString() => $"({X},{Y},{Width}x{Height})";
}
