using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Picks a footprint for a building door. Every shape returned can hold a shop room.</summary>
internal static class BuildingShapes
{
    public const int MinHalf = 3, MaxHalf = 4;      // widths 7 and 9
    public const int MinDepth = 7, MaxDepth = 8;    // a 5x5 floor needs front wall + 5 rows + back wall
    public const int MinRoomSide = 5;
    private const int NotchPercent = 40;
    private const int FurnishedRoomSide = 7;

    /// <summary>The smallest block: 7x7, holding a 5x5 room.</summary>
    public static BuildingFootprint Default(GridPoint door) => BuildingFootprint.Rectangle(door, MinHalf, MinDepth);

    /// <summary>The compact furnished block can hold any service, including an inn or trainer.</summary>
    public static BuildingFootprint FurnishedDefault(GridPoint door) => BuildingFootprint.Rectangle(door, 4, 9);

    public static BuildingFootprint FurnishedRandom(SeedStream random, GridPoint door)
    {
        // Services are assigned after doors are sorted, so every plot must accommodate
        // the largest room recipe. Keep the door and vendor column fixed as wings vary.
        int half = 4 + random.Range(2);
        int depth = 9 + random.Range(4);
        bool wantsNotch = random.Range(100) < NotchPercent;
        bool left = random.Range(2) == 0;
        int notchWidth = 2 + random.Range(half - 2);
        // Keep five usable cells along the short wing for complete furnishing
        // recipes, while still consuming the same draw for every candidate.
        int notchDepth = Math.Min(2 + random.Range(3), depth - 7);

        if (wantsNotch)
        {
            var notched = BuildingFootprint.Notched(door, half, depth, left, notchWidth, notchDepth);
            if (HasOpenArea(notched.TryBuildRoom(), FurnishedRoomSide)) return notched;
        }
        return BuildingFootprint.Rectangle(door, half, depth);
    }

    public static BuildingFootprint Random(SeedStream random, GridPoint door)
    {
        // Always draw the same numbers so a rejected notch does not shift later buildings.
        int half = MinHalf + random.Range(MaxHalf - MinHalf + 1);
        int depth = MinDepth + random.Range(MaxDepth - MinDepth + 1);
        bool wantsNotch = random.Range(100) < NotchPercent;
        bool left = random.Range(2) == 0;
        // The notch stays clear of the door column and the column beside it, so the vendor keeps a walled-in cell.
        int notchWidth = half >= 2 ? 1 + random.Range(half - 1) : 0;
        int notchDepth = 1 + random.Range(depth - 3);

        if (wantsNotch && notchWidth > 0)
        {
            var notched = BuildingFootprint.Notched(door, half, depth, left, notchWidth, notchDepth);
            if (HasOpenArea(notched.TryBuildRoom())) return notched;
        }
        var rectangle = BuildingFootprint.Rectangle(door, half, depth);
        return HasOpenArea(rectangle.TryBuildRoom()) ? rectangle : Default(door);
    }

    /// <summary>
    /// A footprint shape relative to its door, decided without building it. A footprint's cells and room are
    /// translation-invariant, so shapes and their open-area verdicts are computed once per parameter set and
    /// reused; town plot search tries thousands of candidate doors and only builds the footprints it places.
    /// </summary>
    internal readonly struct Shape
    {
        public readonly int Half, Depth, NotchWidth, NotchDepth;
        public readonly bool NotchLeft;
        public Shape(int half, int depth, bool notchLeft = false, int notchWidth = 0, int notchDepth = 0)
        { Half = half; Depth = depth; NotchLeft = notchLeft; NotchWidth = notchWidth; NotchDepth = notchDepth; }
        public bool HasNotch => NotchWidth > 0;
        public GridRect Bounds(GridPoint door) => new(door.X - Half, door.Y + 1, 2 * Half + 1, Depth);
        /// <summary>Body cells relative to the door, in BuildingFootprint.Cells order.</summary>
        public GridPoint[] Offsets => Template(this).Offsets;
        public BuildingFootprint Build(GridPoint door) => HasNotch
            ? BuildingFootprint.Notched(door, Half, Depth, NotchLeft, NotchWidth, NotchDepth)
            : BuildingFootprint.Rectangle(door, Half, Depth);
    }

    private sealed class ShapeTemplate { public GridPoint[] Offsets = Array.Empty<GridPoint>(); public bool OpenArea, FurnishedOpenArea; }
    private static readonly Dictionary<(int, int, bool, int, int), ShapeTemplate> Templates = new();

    private static ShapeTemplate Template(Shape shape)
    {
        var key = (shape.Half, shape.Depth, shape.NotchLeft, shape.NotchWidth, shape.NotchDepth);
        lock (Templates)
        {
            if (Templates.TryGetValue(key, out var template)) return template;
            var footprint = shape.Build(new GridPoint(0, 0));
            var room = footprint.TryBuildRoom();
            template = new ShapeTemplate { Offsets = footprint.Cells.ToArray(), OpenArea = HasOpenArea(room), FurnishedOpenArea = HasOpenArea(room, FurnishedRoomSide) };
            Templates[key] = template;
            return template;
        }
    }

    public static Shape DefaultShape => new(MinHalf, MinDepth);
    public static Shape FurnishedDefaultShape => new(4, 9);

    /// <summary>Same draws and same choice as FurnishedRandom, without building the footprint.</summary>
    public static Shape FurnishedRandomShape(SeedStream random)
    {
        int half = 4 + random.Range(2);
        int depth = 9 + random.Range(4);
        bool wantsNotch = random.Range(100) < NotchPercent;
        bool left = random.Range(2) == 0;
        int notchWidth = 2 + random.Range(half - 2);
        int notchDepth = Math.Min(2 + random.Range(3), depth - 7);
        if (wantsNotch)
        {
            var notched = new Shape(half, depth, left, notchWidth, notchDepth);
            if (Template(notched).FurnishedOpenArea) return notched;
        }
        return new Shape(half, depth);
    }

    /// <summary>Same draws and same choice as Random, without building the footprint.</summary>
    public static Shape RandomShape(SeedStream random)
    {
        int half = MinHalf + random.Range(MaxHalf - MinHalf + 1);
        int depth = MinDepth + random.Range(MaxDepth - MinDepth + 1);
        bool wantsNotch = random.Range(100) < NotchPercent;
        bool left = random.Range(2) == 0;
        int notchWidth = half >= 2 ? 1 + random.Range(half - 1) : 0;
        int notchDepth = 1 + random.Range(depth - 3);
        if (wantsNotch && notchWidth > 0)
        {
            var notched = new Shape(half, depth, left, notchWidth, notchDepth);
            if (Template(notched).OpenArea) return notched;
        }
        var rectangle = new Shape(half, depth);
        return Template(rectangle).OpenArea ? rectangle : DefaultShape;
    }

    /// <summary>True when the room's floor holds the required open square.</summary>
    private static bool HasOpenArea(ShopRoom? room, int minimumSide = MinRoomSide)
    {
        if (room == null) return false;
        var floor = new HashSet<GridPoint>(room.Floor);
        foreach (var origin in room.Floor)
        {
            bool open = true;
            for (int dx = 0; dx < minimumSide && open; dx++)
                for (int dy = 0; dy < minimumSide; dy++)
                    if (!floor.Contains(new GridPoint(origin.X + dx, origin.Y + dy))) { open = false; break; }
            if (open) return true;
        }
        return false;
    }
}
