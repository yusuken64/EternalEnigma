using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Picks a footprint for a building door. Every shape returned can hold a shop room.</summary>
internal static class BuildingShapes
{
    public const int MinHalf = 1, MaxHalf = 3;      // widths 3, 5 and 7
    public const int MinDepth = 4, MaxDepth = 6;
    private const int NotchPercent = 40;

    /// <summary>The original 3x4 block.</summary>
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
            if (notched.TryBuildRoom() != null) return notched;
        }
        var rectangle = BuildingFootprint.Rectangle(door, half, depth);
        return rectangle.TryBuildRoom() != null ? rectangle : Default(door);
    }
}
