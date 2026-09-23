namespace EternalEnigma.Core.Generation;

public sealed class DungeonFloorOptions : IEquatable<DungeonFloorOptions>
{
    public int Seed { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsThroneFloor { get; }
    public int EnemyCount { get; }
    public int GoldCount { get; }
    public int ItemCount { get; }
    public int TrapCount { get; }

    public DungeonFloorOptions(int seed, int width = 32, int height = 32, bool isThroneFloor = false,
        int enemyCount = 10, int goldCount = 5, int itemCount = 5, int trapCount = 5)
    {
        if (width < 12 || width > 128)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be between 12 and 128.");
        if (height < 12 || height > 128)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be between 12 and 128.");
        if (enemyCount < 0 || enemyCount > 64)
            throw new ArgumentOutOfRangeException(nameof(enemyCount), "Enemy count must be between 0 and 64.");
        if (goldCount < 0 || goldCount > 64)
            throw new ArgumentOutOfRangeException(nameof(goldCount), "Gold count must be between 0 and 64.");
        if (itemCount < 0 || itemCount > 64)
            throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count must be between 0 and 64.");
        if (trapCount < 0 || trapCount > 64)
            throw new ArgumentOutOfRangeException(nameof(trapCount), "Trap count must be between 0 and 64.");

        if (isThroneFloor)
        {
            if (width != 12 || height != 12 || enemyCount != 0 || goldCount != 0 || itemCount != 0 || trapCount != 0)
                throw new ArgumentException("Throne floors are fixed 12x12 with no placements.");
        }

        Seed = seed;
        Width = width;
        Height = height;
        IsThroneFloor = isThroneFloor;
        EnemyCount = enemyCount;
        GoldCount = goldCount;
        ItemCount = itemCount;
        TrapCount = trapCount;
    }

    public static DungeonFloorOptions Throne(int seed) => new(seed, 12, 12, true, 0, 0, 0, 0);

    public bool Equals(DungeonFloorOptions? other) =>
        other != null &&
        Seed == other.Seed &&
        Width == other.Width &&
        Height == other.Height &&
        IsThroneFloor == other.IsThroneFloor &&
        EnemyCount == other.EnemyCount &&
        GoldCount == other.GoldCount &&
        ItemCount == other.ItemCount &&
        TrapCount == other.TrapCount;

    public override bool Equals(object? obj) => Equals(obj as DungeonFloorOptions);

    public override int GetHashCode() =>
        unchecked(
            Seed * 397 ^
            Width * 397 ^
            Height * 397 ^
            IsThroneFloor.GetHashCode() * 397 ^
            EnemyCount * 397 ^
            GoldCount * 397 ^
            ItemCount * 397 ^
            TrapCount * 397
        );

    public override string ToString() =>
        $"DungeonFloorOptions(Seed={Seed}, {Width}x{Height}, IsThroneFloor={IsThroneFloor}, " +
        $"Enemies={EnemyCount}, Gold={GoldCount}, Items={ItemCount}, Traps={TrapCount})";
}
