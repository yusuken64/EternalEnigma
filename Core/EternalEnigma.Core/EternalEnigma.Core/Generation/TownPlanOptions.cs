using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public sealed class TownPlanOptions : IEquatable<TownPlanOptions>
{
    public const int MaxBuildings = 24;
    /// <summary>Area per building (body, shop room, margins, roads and scenery) used to size towns.</summary>
    private const int CellsPerBuilding = 70;

    /// <summary>Smallest square side that comfortably fits the buildings; never below the legacy 15x15.</summary>
    public static int SizeFor(int buildingCount) =>
        Math.Min(64, Math.Max(15, (int)Math.Ceiling(Math.Sqrt(buildingCount * CellsPerBuilding))));

    public int Seed { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<bool> ShopFlags { get; }
    public int BuildingCount => ShopFlags.Count;
    public int AllyCount { get; }
    public GridPoint PartySpawn { get; }
    public GridPoint Exit { get; }
    /// <summary>Column of the central road, the dungeon entrance and the reserved entrance corridor.</summary>
    public int SpineX { get; }
    /// <summary>Varied building footprints, a main/artery/alley road hierarchy and prop cells. False keeps the original simple layout.</summary>
    public bool Detailed { get; }

    public TownPlanOptions(int seed, int width = 15, int height = 15, IReadOnlyList<bool>? shopFlags = null,
        int allyCount = 3, GridPoint? partySpawn = null, GridPoint? exit = null, int spineX = TownPlan.DefaultSpineX,
        bool detailed = false)
    {
        if (width < 15 || width > 64)
            throw new ArgumentException("Width must be between 15 and 64.");
        if (height < 15 || height > 64)
            throw new ArgumentException("Height must be between 15 and 64.");
        if (spineX - TownPlan.CorridorHalfWidth < 0 || spineX + TownPlan.CorridorHalfWidth >= width)
            throw new ArgumentException("The entrance corridor must fit inside the town width.");

        shopFlags ??= new[] { false, true, false, false };
        partySpawn ??= new GridPoint(spineX, 2);
        exit ??= new GridPoint(spineX, 0);

        if (shopFlags.Count < 1 || shopFlags.Count > MaxBuildings)
            throw new ArgumentException($"Building count must be between 1 and {MaxBuildings}.");
        if (allyCount < 0 || allyCount > 12)
            throw new ArgumentException("Ally count must be between 0 and 12.");

        // Validate PartySpawn and Exit: must be in bounds and in corridor
        if (!IsValidCorridorPoint(partySpawn.Value, width, height, spineX))
            throw new ArgumentException("PartySpawn must be within bounds and in the reserved corridor (X within 2 of the spine, Y in 0..height/2).");
        if (!IsValidCorridorPoint(exit.Value, width, height, spineX))
            throw new ArgumentException("Exit must be within bounds and in the reserved corridor (X within 2 of the spine, Y in 0..height/2).");

        SpineX = spineX;
        Detailed = detailed;
        Seed = seed;
        Width = width;
        Height = height;
        ShopFlags = new List<bool>(shopFlags).AsReadOnly();
        AllyCount = allyCount;
        PartySpawn = partySpawn.Value;
        Exit = exit.Value;
    }

    private static bool IsValidCorridorPoint(GridPoint point, int width, int height, int spineX)
    {
        // Must be in bounds
        if (point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)
            return false;
        // Must be in corridor: X within CorridorHalfWidth of the spine and Y in 0..height/2
        return Math.Abs(point.X - spineX) <= TownPlan.CorridorHalfWidth && point.Y >= 0 && point.Y <= height / 2;
    }

    public bool Equals(TownPlanOptions? other) =>
        other != null &&
        Seed == other.Seed &&
        Width == other.Width &&
        Height == other.Height &&
        AllyCount == other.AllyCount &&
        SpineX == other.SpineX &&
        Detailed == other.Detailed &&
        PartySpawn.Equals(other.PartySpawn) &&
        Exit.Equals(other.Exit) &&
        ShopFlags.SequenceEqual(other.ShopFlags);

    public override bool Equals(object? obj) => Equals(obj as TownPlanOptions);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Seed * 397 ^ Width * 397 ^ Height * 397 ^ AllyCount * 397 ^ SpineX * 397 ^ Detailed.GetHashCode() * 397 ^
                       PartySpawn.GetHashCode() * 397 ^ Exit.GetHashCode() * 397;
            foreach (var flag in ShopFlags)
                hash = hash * 397 ^ flag.GetHashCode();
            return hash;
        }
    }

    public override string ToString() =>
        $"TownPlanOptions(Seed={Seed}, {Width}x{Height}, Buildings={BuildingCount}, Allies={AllyCount}, " +
        $"PartySpawn={PartySpawn}, Exit={Exit}, SpineX={SpineX}, Detailed={Detailed})";
}
