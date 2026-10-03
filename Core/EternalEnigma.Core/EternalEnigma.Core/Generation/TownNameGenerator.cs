using System.Globalization;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>Presentation-only vocabulary. Never uses the layout random stream.</summary>
public static class TownNameGenerator
{
    public const int Version = 1;
    private static readonly string[][] Roots = {
        new[] { "Amber", "Wheat", "Oak", "Sunny", "Clover", "Gold", "Meadow", "Lark" },
        new[] { "Saffron", "Dune", "Brass", "Sun", "Ochre", "Sandal", "Mirage", "Date" },
        new[] { "Pearl", "Shell", "Tide", "Coral", "Foam", "Azure", "Salt", "Gull" },
        new[] { "Granite", "Quartz", "Iron", "High", "Slate", "Eagle", "Rune", "Silver" },
        new[] { "Willow", "Acorn", "Fern", "Cedar", "Moss", "Leaf", "Alder", "Stag" },
        new[] { "Frost", "Snow", "Pale", "Winter", "Ice", "Wolf", "White", "Aurora" },
        new[] { "Reed", "Fen", "Glow", "Heron", "Mire", "Sedge", "Bog", "Wisp" },
        new[] { "Ember", "Ash", "Cinder", "Basalt", "Obsidian", "Coal", "Flame", "Smoke" }
    };
    private static readonly string[][] Ends = {
        new[] { "field", "brook", "vale", "ford", "stead", "hill", "haven", "mead" },
        new[] { "well", "rest", "spire", "arch", "reach", "oasis", "gate", "sands" },
        new[] { "bay", "shore", "haven", "cove", "port", "strand", "reef", "harbor" },
        new[] { "peak", "hold", "crag", "pass", "ridge", "watch", "hearth", "rock" },
        new[] { "grove", "wood", "glade", "bough", "hollow", "shade", "root", "dell" },
        new[] { "hearth", "shelter", "drift", "watch", "fell", "rest", "pine", "cairn" },
        new[] { "fen", "water", "hollow", "marsh", "pool", "rest", "mire", "bank" },
        new[] { "forge", "fall", "hearth", "reach", "gate", "crest", "hold", "scar" }
    };
    public static uint Hash(int seed, string identity)
    {
        unchecked {
            uint h = 2166136261;
            foreach (char c in "town-names/v1/" + seed.ToString(CultureInfo.InvariantCulture) + "/" + identity)
                h = (h ^ c) * 16777619;
            h ^= h >> 16; h *= 0x7feb352d; return h ^ (h >> 15);
        }
    }
    public static string Generate(int seed, string townId, OverworldBiome biome, ISet<string> reserved)
    {
        int b = (int)biome;
        if (b < 0 || b >= Roots.Length) throw new ArgumentOutOfRangeException(nameof(biome));
        uint hash = Hash(seed, townId);
        for (int attempt = 0; ; attempt++) {
            int choice = (int)((hash + (uint)attempt) % 64);
            string name = Roots[b][choice / 8] + Ends[b][choice % 8];
            if (attempt >= 64) name += " " + (attempt / 64 + 1).ToString(CultureInfo.InvariantCulture);
            if (reserved.Add(name)) return name;
        }
    }
}
