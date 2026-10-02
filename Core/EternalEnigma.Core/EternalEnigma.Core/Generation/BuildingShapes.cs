using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Picks a footprint for a building door. Every shape returned can hold a shop room.</summary>
internal static class BuildingShapes
{
    public const int MinHalf = 3, MaxHalf = 4;      // widths 7 and 9
    public const int MinDepth = 7, MaxDepth = 8;    // a 5x5 floor needs front wall + 5 rows + back wall
    public const int MinRoomSide = 5;
    private const int NotchPercent = 40;

    /// <summary>The smallest block: 7x7, holding a 5x5 room.</summary>
    public static BuildingFootprint Default(GridPoint door) => BuildingFootprint.Rectangle(door, MinHalf, MinDepth);

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

    /// <summary>True when the room's floor holds a square of at least MinRoomSide x MinRoomSide cells.</summary>
    private static bool HasOpenArea(ShopRoom? room)
    {
        if (room == null) return false;
        var floor = new HashSet<GridPoint>(room.Floor);
        foreach (var origin in room.Floor)
        {
            bool open = true;
            for (int dx = 0; dx < MinRoomSide && open; dx++)
                for (int dy = 0; dy < MinRoomSide; dy++)
                    if (!floor.Contains(new GridPoint(origin.X + dx, origin.Y + dy))) { open = false; break; }
            if (open) return true;
        }
        return false;
    }
}
