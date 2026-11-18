using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Benchmarks;

/// <summary>The generation work one playthrough of a campaign asks Core for.</summary>
public static class Workloads
{
    public const int Seed = 42;
    public static readonly Campaign Campaign = CampaignGenerator.Generate(Seed);
    public static readonly TownPlanOptions[] Towns = Campaign.Locations
        .Where(l => l.Kind == LocationKind.Town).Select(l => TownLayout.OptionsFor(Seed, l)).ToArray();
    public static readonly DungeonFloorOptions[] Floors =
        (from biome in Enum.GetValues<OverworldBiome>().Where(b => b != OverworldBiome.Water)
         from tier in Enumerable.Range(0, 5)
         select DungeonLayoutProfile.Options(Seed * 31 + (int)biome * 7 + tier, biome, tier, floorNumber: 3)).ToArray();
    public static readonly DungeonFloorOptions[] TownFloors =
        Enumerable.Range(0, 5).Select(tier => DungeonLayoutProfile.Options(Seed + tier, OverworldBiome.Grassland, tier,
            locationId: "story-3", variant: DungeonVariant.Town, floorNumber: 3)).ToArray();

    public static object CampaignGen() => CampaignGenerator.Generate(Seed);
    public static object AllTowns() { object? last = null; foreach (var o in Towns) last = TownPlanGenerator.Generate(o); return last!; }
    public static object AllFloors() { object? last = null; foreach (var o in Floors) last = DungeonFloorGenerator.Generate(o); return last!; }
    public static object AllTownFloors() { object? last = null; foreach (var o in TownFloors) last = DungeonFloorGenerator.Generate(o); return last!; }
    public static object Overworld() => OverworldGridGenerator.Generate(Campaign);
}

[MemoryDiagnoser]
public class GenerationBenchmarks
{
    [Benchmark] public object Campaign() => Workloads.CampaignGen();
    [Benchmark] public object Overworld() => Workloads.Overworld();
    [Benchmark] public object AllCampaignTowns() => Workloads.AllTowns();
    [Benchmark] public object DungeonFloors_AllBiomesTiers() => Workloads.AllFloors();
    [Benchmark] public object TownDistrictFloors() => Workloads.AllTownFloors();
}

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--leak")) { LeakCheck(); return; }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    /// <summary>
    /// Runs each workload repeatedly, discarding results, and reports live managed memory after a full
    /// blocking GC. Flat after the first round (static caches warm up once) means nothing is retained.
    /// </summary>
    private static void LeakCheck()
    {
        var workloads = new (string Name, Func<object> Run)[]
        {
            ("Campaign", Workloads.CampaignGen), ("Overworld", Workloads.Overworld), ("AllCampaignTowns", Workloads.AllTowns),
            ("DungeonFloors", Workloads.AllFloors), ("TownDistrictFloors", Workloads.AllTownFloors),
        };
        const int rounds = 6, perRound = 5;
        foreach (var (name, run) in workloads)
        {
            var live = new List<long>();
            for (int r = 0; r < rounds; r++)
            {
                for (int i = 0; i < perRound; i++) run();
                live.Add(LiveBytes());
            }
            long growth = live[^1] - live[1];
            Console.WriteLine($"{name,-20} live MB per round: {string.Join(" ", live.Select(b => (b / 1048576.0).ToString("F1")))}" +
                $"  | growth after warm-up: {growth / 1048576.0:+0.00;-0.00} MB {(Math.Abs(growth) < 1 << 20 ? "OK" : "SUSPECT")}");
        }
    }

    private static long LiveBytes()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(false);
    }
}
