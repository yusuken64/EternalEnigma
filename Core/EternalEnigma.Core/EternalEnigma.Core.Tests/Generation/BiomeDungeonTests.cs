using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class BiomeDungeonTests
{
    private readonly ITestOutputHelper output;
    public BiomeDungeonTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void LegacyReferenceArea()
    {
        double mean = Enumerable.Range(0, 256).Average(seed => Area(DungeonFloorGenerator.Generate(new DungeonFloorOptions(seed))));
        output.WriteLine($"Legacy reference area: {mean}");
        Assert.Equal(BiomeDungeonGenerator.ReferenceArea,mean);
    }

    static int Area(DungeonFloor floor) => floor.Layers[DungeonLayers.Floor].ToArray().Cast<bool>().Count(v => v);

    [Theory]
    [InlineData(OverworldBiome.Grassland)] [InlineData(OverworldBiome.Forest)]
    [InlineData(OverworldBiome.Desert)] [InlineData(OverworldBiome.Water)]
    [InlineData(OverworldBiome.Mountain)] [InlineData(OverworldBiome.Tundra)]
    [InlineData(OverworldBiome.Marsh)] [InlineData(OverworldBiome.Volcanic)]
    public void SeedSweep(OverworldBiome biome)
    {
        double previous = 0;
        for (int tier = 0; tier <= 4; tier++)
        {
            double area = 0;
            foreach (DungeonFloorRole role in Enum.GetValues(typeof(DungeonFloorRole)))
            for (int seed = 0; seed < 24; seed++)
            {
                var options = DungeonLayoutProfile.Options(seed, biome, tier, role);
                var f = DungeonFloorGenerator.Generate(options);
                var again = DungeonFloorGenerator.Generate(options);
                var layer = f.Layers[DungeonLayers.Floor];
                Assert.Equal(layer.ToArray().Cast<bool>(), again.Layers[DungeonLayers.Floor].ToArray().Cast<bool>());
                Assert.Equal(f.Scenery.Select(p => (p.Cell, p.Kind, p.Roll, p.Reward)), again.Scenery.Select(p => (p.Cell, p.Kind, p.Roll, p.Reward)));
                int count = Area(f);
                Assert.Equal(count, GridSearch.VisitOrder(f.Start, f.Neighbors).Count);
                for (int x = 0; x < f.Width; x++) { Assert.False(layer[x, 0]); Assert.False(layer[x, f.Height - 1]); }
                for (int y = 0; y < f.Height; y++) { Assert.False(layer[0, y]); Assert.False(layer[f.Width - 1, y]); }
                foreach (var room in f.Rooms) foreach (var p in room.Cells()) Assert.True(layer.At(p));
                var blocked = f.Scenery.Where(p => p.Kind != DungeonSceneryKind.Hazard).Select(p => p.Cell).ToHashSet();
                Assert.Equal(count - blocked.Count, GridSearch.VisitOrder(f.Start, p => BiomeDungeonGenerator.Neighbors(layer, p, blocked)).Count);
                blocked.UnionWith(f.Scenery.Where(p => p.Kind == DungeonSceneryKind.Hazard).Select(p => p.Cell));
                Assert.Contains(f.Stairs, GridSearch.VisitOrder(f.Start, p => BiomeDungeonGenerator.Neighbors(layer, p, blocked)));
                Assert.All(f.Scenery, p => Assert.True(Math.Max(Math.Abs(p.Cell.X-f.Start.X),Math.Abs(p.Cell.Y-f.Start.Y)) > 3));
                Assert.DoesNotContain(f.Scenery.GroupBy(p => p.Cell), g => g.Count() > 1);
                if (role != DungeonFloorRole.Regular) { Assert.Empty(f.Enemies); Assert.Empty(f.Scenery); Assert.InRange(f.Width,16,24); }
                else
                {
                    area += count;
                    int budget = (int)Math.Round(5 * BiomeDungeonGenerator.Density(count), MidpointRounding.AwayFromZero);
                    Assert.Equal(budget, f.Items.Count + f.Scenery.Count(p => p.Reward == SceneryReward.Item));
                    Assert.Equal(budget, f.Gold.Count + f.Scenery.Count(p => p.Reward == SceneryReward.Gold));
                }
            }
            output.WriteLine($"{biome} tier {tier}: mean walkable {area/24:F2}");
            Assert.True(area > previous, $"{biome} tier {tier}: {area} <= {previous}"); previous = area;
        }
    }

    [Fact]
    public void StartersAndDefaultsRemainLegacy()
    {
        foreach (var id in new[] { "repeatable-0", "story-0" })
        {
            Assert.Equal(new DungeonFloorOptions(42), DungeonLayoutProfile.Options(42, OverworldBiome.Grassland, 0, locationId:id));
            Assert.Equal(DungeonFloorOptions.Throne(42), DungeonLayoutProfile.Options(42, OverworldBiome.Grassland, 0, DungeonFloorRole.Entry, locationId:id));
        }
        var a = DungeonLayoutProfile.Options(42, OverworldBiome.Grassland, 0);
        Assert.NotEqual(new DungeonFloorOptions(42), a);
        Assert.NotEqual(a, DungeonLayoutProfile.Options(42, OverworldBiome.Forest, 0));
    }
}
