using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public sealed class TownPlanOptions : IEquatable<TownPlanOptions>
{
    public int Seed { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<bool> ShopFlags { get; }
    public int BuildingCount => ShopFlags.Count;
    public int AllyCount { get; }
    public GridPoint PartySpawn { get; }
    public GridPoint Exit { get; }

    public TownPlanOptions(int seed, int width = 15, int height = 15, IReadOnlyList<bool>? shopFlags = null,
        int allyCount = 3, GridPoint? partySpawn = null, GridPoint? exit = null)
    {
        if (width < 15 || width > 64)
            throw new ArgumentException("Width must be between 15 and 64.");
        if (height < 15 || height > 64)
            throw new ArgumentException("Height must be between 15 and 64.");

        shopFlags ??= new[] { false, true, false, false };
        partySpawn ??= new GridPoint(10, 2);
        exit ??= new GridPoint(10, 0);

        if (shopFlags.Count < 1 || shopFlags.Count > 12)
            throw new ArgumentException("Building count must be between 1 and 12.");
        if (allyCount < 0 || allyCount > 12)
            throw new ArgumentException("Ally count must be between 0 and 12.");

        // Validate PartySpawn and Exit: must be in bounds and in corridor
        if (!IsValidCorridorPoint(partySpawn.Value, width, height))
            throw new ArgumentException("PartySpawn must be within bounds and in the reserved corridor (X in 8..12, Y in 0..height/2).");
        if (!IsValidCorridorPoint(exit.Value, width, height))
            throw new ArgumentException("Exit must be within bounds and in the reserved corridor (X in 8..12, Y in 0..height/2).");

        Seed = seed;
        Width = width;
        Height = height;
        ShopFlags = new List<bool>(shopFlags).AsReadOnly();
        AllyCount = allyCount;
        PartySpawn = partySpawn.Value;
        Exit = exit.Value;
    }

    private static bool IsValidCorridorPoint(GridPoint point, int width, int height)
    {
        // Must be in bounds
        if (point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)
            return false;
        // Must be in corridor: X in 8..12 and Y in 0..height/2
        return point.X >= 8 && point.X <= 12 && point.Y >= 0 && point.Y <= height / 2;
    }

    public bool Equals(TownPlanOptions? other) =>
        other != null &&
        Seed == other.Seed &&
        Width == other.Width &&
        Height == other.Height &&
        AllyCount == other.AllyCount &&
        PartySpawn.Equals(other.PartySpawn) &&
        Exit.Equals(other.Exit) &&
        ShopFlags.SequenceEqual(other.ShopFlags);

    public override bool Equals(object? obj) => Equals(obj as TownPlanOptions);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Seed * 397 ^ Width * 397 ^ Height * 397 ^ AllyCount * 397 ^
                       PartySpawn.GetHashCode() * 397 ^ Exit.GetHashCode() * 397;
            foreach (var flag in ShopFlags)
                hash = hash * 397 ^ flag.GetHashCode();
            return hash;
        }
    }

    public override string ToString() =>
        $"TownPlanOptions(Seed={Seed}, {Width}x{Height}, Buildings={BuildingCount}, Allies={AllyCount}, " +
        $"PartySpawn={PartySpawn}, Exit={Exit})";
}
