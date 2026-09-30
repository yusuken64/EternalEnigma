namespace EternalEnigma.Core.World;

/// <summary>
/// The blocked cells of one building, directly north of its door. A footprint is a rectangle
/// (odd width, so the door is centred) with an optional rectangular notch cut from a top corner (an L shape).
/// Its shop room, when it has one, is derived from the same cells.
/// </summary>
public sealed class BuildingFootprint
{
    public GridPoint Door { get; }
    /// <summary>Body cells; empty for a door that has no body.</summary>
    public IReadOnlyList<GridPoint> Cells { get; }
    public GridRect Bounds { get; }
    /// <summary>Rows from the front wall to the back wall, inclusive.</summary>
    public int Depth { get; }
    public bool HasNotch { get; }

    private BuildingFootprint(GridPoint door, IEnumerable<GridPoint> cells, GridRect bounds, int depth, bool hasNotch)
    {
        Door = door;
        Cells = cells.ToList().AsReadOnly();
        Bounds = bounds;
        Depth = depth;
        HasNotch = hasNotch;
    }

    /// <summary>A door with no body, used when a building position had to be completed without room for a body.</summary>
    public static BuildingFootprint Bodyless(GridPoint door) => new(door, Array.Empty<GridPoint>(), new GridRect(door.X, door.Y, 1, 1), 0, false);

    /// <param name="half">Cells either side of the door column; the body is 2 * half + 1 wide.</param>
    /// <param name="depth">Rows of the body north of the door, including front and back walls.</param>
    public static BuildingFootprint Rectangle(GridPoint door, int half, int depth) => Create(door, half, depth, false, 0, 0);

    /// <summary>A rectangle with an <paramref name="notchWidth"/> x <paramref name="notchDepth"/> block removed from a top corner.</summary>
    public static BuildingFootprint Notched(GridPoint door, int half, int depth, bool notchLeft, int notchWidth, int notchDepth) =>
        Create(door, half, depth, notchLeft, notchWidth, notchDepth);

    private static BuildingFootprint Create(GridPoint door, int half, int depth, bool notchLeft, int notchWidth, int notchDepth)
    {
        if (half < 1) throw new ArgumentOutOfRangeException(nameof(half));
        if (depth < 4) throw new ArgumentOutOfRangeException(nameof(depth));
        if (notchWidth < 0 || notchDepth < 0 || (notchWidth > 0) != (notchDepth > 0))
            throw new ArgumentException("A notch needs both a width and a depth.");
        var bounds = new GridRect(door.X - half, door.Y + 1, 2 * half + 1, depth);
        bool notch = notchWidth > 0;
        var cells = bounds.Cells().Where(c =>
        {
            if (!notch) return true;
            bool inColumns = notchLeft ? c.X < bounds.X + notchWidth : c.X > bounds.Right - notchWidth;
            return !(inColumns && c.Y > bounds.Top - notchDepth);
        });
        return new BuildingFootprint(door, cells, bounds, depth, notch);
    }

    /// <summary>The floor cell against the back wall, in the door column, where the vendor stands.</summary>
    public GridPoint VendorAnchor => new(Door.X, Door.Y + Depth - 1);

    /// <summary>
    /// Builds the walk-in room for this footprint: the outer ring of cells is wall, the doorway in the front
    /// wall and everything enclosed is floor. Null when the shape cannot hold a usable room (vendor cell not
    /// enclosed, or the floor is not one connected space).
    /// </summary>
    public ShopRoom? TryBuildRoom()
    {
        if (Cells.Count == 0) return null;
        var body = new HashSet<GridPoint>(Cells);
        var doorway = new GridPoint(Door.X, Door.Y + 1);
        if (!body.Contains(doorway)) return null;

        var wall = new List<GridPoint>();
        var floor = new List<GridPoint>();
        foreach (var cell in Cells)
        {
            bool edge = false;
            for (int dx = -1; dx <= 1 && !edge; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && !body.Contains(new GridPoint(cell.X + dx, cell.Y + dy))) { edge = true; break; }
            // The doorway is the gap in the front wall, so it is floor even though it touches the outside.
            if (edge && !cell.Equals(doorway)) wall.Add(cell); else floor.Add(cell);
        }

        var anchor = VendorAnchor;
        if (!floor.Contains(anchor)) return null;
        var floorSet = new HashSet<GridPoint>(floor);
        var seen = new HashSet<GridPoint> { doorway };
        var pending = new Queue<GridPoint>();
        pending.Enqueue(doorway);
        while (pending.Count > 0)
        {
            var at = pending.Dequeue();
            foreach (var step in new[] { new GridPoint(0, 1), new GridPoint(0, -1), new GridPoint(1, 0), new GridPoint(-1, 0) })
            {
                var next = new GridPoint(at.X + step.X, at.Y + step.Y);
                if (floorSet.Contains(next) && seen.Add(next)) pending.Enqueue(next);
            }
        }
        if (seen.Count != floor.Count) return null;

        // Floor ordered from the doorway inwards, like the original fixed room.
        floor.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return new ShopRoom(Door, floor, wall, anchor);
    }
}
