using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public enum DungeonFloorRole { Regular, Entry, Exit }

public static class DungeonLayoutProfile
{
    public const int CurrentVersion = 1;
    public static bool IsStarter(string id) => id == "repeatable-0" || id == "story-0";

    public static DungeonFloorOptions Options(int seed, OverworldBiome biome, int tier,
        DungeonFloorRole role = DungeonFloorRole.Regular, int version = CurrentVersion, string locationId = "")
    {
        if (tier < 0 || tier > 4) throw new ArgumentOutOfRangeException(nameof(tier));
        if (version == 0 || IsStarter(locationId))
            return role == DungeonFloorRole.Regular ? new DungeonFloorOptions(seed) : DungeonFloorOptions.Throne(seed);
        var random = new SeedStream(seed, 1000);
        int[] minimum = { 32, 34, 38, 44, 52 };
        bool regular = role == DungeonFloorRole.Regular;
        int width = regular ? minimum[tier] + (tier == 0 ? 0 : random.Range(5)) : 16 + tier * 2;
        int height = regular ? minimum[tier] + (tier == 0 ? 0 : random.Range(5)) : 16 + tier * 2;
        return new DungeonFloorOptions(seed, width, height, !regular, regular ? 10 : 0,
            regular ? 5 : 0, regular ? 5 : 0, 0, regular ? 3 : 0, version, biome, tier, role);
    }
}
