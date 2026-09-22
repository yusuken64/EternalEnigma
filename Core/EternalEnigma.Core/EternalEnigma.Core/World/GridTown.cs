namespace EternalEnigma.Core.World;

/// <summary>A walled overworld miniature. Only Entrance is walkable; it opens the existing town scene.</summary>
public sealed class GridTown
{
    public const int Size = 5;
    public string LocationId { get; }
    public GridPoint Entrance { get; }
    /// <summary>Cardinal direction from the gateway into the miniature.</summary>
    public GridPoint Inward { get; }
    public GridPoint Approach => new(Entrance.X - Inward.X, Entrance.Y - Inward.Y);
    public IReadOnlyList<GridPoint> Cells { get; }
    public IReadOnlyList<GridPoint> Walls { get; }
    public IReadOnlyList<GridPoint> Interior { get; }

    public GridTown(string locationId, GridPoint entrance, GridPoint inward)
    {
        if (Math.Abs(inward.X) + Math.Abs(inward.Y) != 1) throw new ArgumentException("Town direction must be cardinal.", nameof(inward));
        LocationId = locationId;
        Entrance = entrance;
        Inward = inward;
        var cells = new List<GridPoint>();
        var walls = new List<GridPoint>();
        var interior = new List<GridPoint>();
        for (int depth = 0; depth < Size; depth++)
        for (int side = -Size / 2; side <= Size / 2; side++)
        {
            var cell = Cell(side, depth);
            cells.Add(cell);
            if (cell.Equals(Entrance)) continue;
            if (depth == 0 || depth == Size - 1 || Math.Abs(side) == Size / 2) walls.Add(cell);
            else interior.Add(cell);
        }
        Cells = Array.AsReadOnly(cells.ToArray());
        Walls = Array.AsReadOnly(walls.ToArray());
        Interior = Array.AsReadOnly(interior.ToArray());
    }

    public GridPoint Cell(int side, int depth) => new(
        Entrance.X + Inward.X * depth - Inward.Y * side,
        Entrance.Y + Inward.Y * depth + Inward.X * side);
}
