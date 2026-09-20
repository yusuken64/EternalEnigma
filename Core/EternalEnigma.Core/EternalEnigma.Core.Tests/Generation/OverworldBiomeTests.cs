using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class OverworldBiomeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    public void RegionsHaveDistinctBiomesAndWaterRequiresBoatEveryTime(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        Assert.Contains(campaign.Manifest, c => c.Id == Capability.Boat);
        Assert.Equal(OverworldBiome.Grassland, grid.BiomeAt(grid.PlayerStart));
        Assert.Equal(campaign.Regions.Count, grid.RegionBiomes.Values.Distinct().Count());
        var withoutBoat = CapabilitySet.From(campaign.Manifest.Select(c => c.Id).Where(c => c != Capability.Boat));
        var all = withoutBoat.Union(CapabilitySet.Of(Capability.Boat));
        var resolved = campaign.Routes.Select(r => r.Id).ToHashSet();
        int waterCount = 0, shores = 0;
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
        {
            var cell = new GridPoint(x, y);
            Assert.Equal(grid.IsGround(cell) ? 1 : 0, Enum.GetValues<OverworldBiome>().Count(b => grid.Layers[OverworldLayers.Biome(b)][x, y]));
            Assert.Equal(grid.IsWalkable(cell, CapabilitySet.Empty), grid.Layers[OverworldLayers.Walkable][x, y]);
            if (x < 2 || y < 2 || x >= grid.Width - 2 || y >= grid.Height - 2)
            { Assert.True(grid.Layers[OverworldLayers.Water][x, y]); Assert.False(grid.IsWalkable(cell, all)); }
            if (!grid.RequiresBoat(cell)) continue;
            waterCount++;
            Assert.Equal(OverworldBiome.Water, grid.BiomeAt(cell));
            Assert.True(grid.IsGround(cell));
            Assert.True(grid.Layers[OverworldLayers.Water][x, y]);
            Assert.False(grid.IsWalkable(cell, withoutBoat, resolved));
            Assert.True(grid.IsWalkable(cell, all));
            foreach (var shore in OverworldMovement.Neighbors(cell).Where(p => p.X == x || p.Y == y))
                if (grid.IsWalkable(shore, withoutBoat))
                {
                    shores++;
                    Assert.False(grid.CanStep(shore, cell, withoutBoat));
                    Assert.True(grid.CanStep(shore, cell, all));
                }
        }
        Assert.True(waterCount >= 3); // Boat's optional payoff remains a water crossing even without a water region.
        if (grid.RegionBiomes.Values.Contains(OverworldBiome.Water))
            Assert.Contains(Enumerable.Range(0, grid.Width * grid.Height).Select(i => new GridPoint(i % grid.Width, i / grid.Width)),
                p => grid.RequiresBoat(p) && grid.LockAt(p) == null);
        Assert.True(shores > 0);
        var reachable = new HashSet<GridPoint> { grid.PlayerStart };
        var queue = new Queue<GridPoint>(); queue.Enqueue(grid.PlayerStart);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var next in OverworldMovement.Neighbors(at))
                if (grid.CanStep(at, next, withoutBoat) && reachable.Add(next)) queue.Enqueue(next);
        }
        Assert.Contains(campaign.Sources.Where(s => s.Capability == Capability.Boat), s => reachable.Contains(grid.Locations[s.LocationId]));
        foreach (var cell in grid.Locations.Values) Assert.False(grid.RequiresBoat(cell));
    }

    [Fact]
    public void BiomesAreSampledFromTheWholePoolWithAGrasslandStart()
    {
        var seen = new HashSet<OverworldBiome>();
        var selections = new HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            var campaign = CampaignGenerator.Generate(seed);
            var grid = OverworldGridGenerator.Generate(campaign);
            string startRegion = campaign.Locations.Single(l => l.Id == campaign.StartLocationId).RegionId;
            Assert.Equal(OverworldBiome.Grassland, grid.RegionBiomes[startRegion]);
            var others = grid.RegionBiomes.Where(p => p.Key != startRegion).Select(p => p.Value).ToArray();
            Assert.DoesNotContain(OverworldBiome.Grassland, others);
            Assert.Equal(others.Length, others.Distinct().Count());
            seen.UnionWith(others);
            selections.Add(string.Join(",", others.OrderBy(b => b)));
        }
        Assert.Equal(7, seen.Count);
        Assert.True(selections.Count > 5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    public void ExpandedRegionsKeepTwoTileBarriersWithoutRemovingOriginalConnections(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var original = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: 0));
        var owners = new string?[grid.Width, grid.Height];
        foreach (var region in campaign.Regions)
            for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
                if (grid.Layers[OverworldLayers.Region(region.Id)][x, y]) owners[x, y] = region.Id;
        var all = CapabilitySet.From(campaign.Manifest.Select(c => c.Id));
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
        {
            var cell = new GridPoint(x, y);
            if (original.IsGround(cell)) { Assert.True(grid.IsGround(cell)); continue; }
            if (!grid.IsGround(cell)) { Assert.False(grid.IsWalkable(cell, all)); continue; }
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
            {
                var other = new GridPoint(x + dx, y + dy);
                if (grid.IsGround(other)) Assert.Equal(owners[x, y], owners[other.X, other.Y]);
            }
        }
        foreach (var route in original.Routes) Assert.Equal(route.Value, grid.Routes[route.Key]);
        foreach (var gate in original.Locks) Assert.Equal(gate.Cells, grid.Locks.Single(g => g.RouteId == gate.RouteId).Cells);
    }
}
