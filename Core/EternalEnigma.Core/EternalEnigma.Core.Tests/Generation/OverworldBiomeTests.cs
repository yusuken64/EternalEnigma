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
        Assert.True(waterCount >= 2); // Boat's optional payoff remains a water crossing even without a water region.
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
                if (grid.CanStep(at, next, withoutBoat, new HashSet<string> { "starter-exit" }) && reachable.Add(next)) queue.Enqueue(next);
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
    public void GrasslandHasMixedTerrainWithoutBlockingLocationsOrRoads(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var grass = grid.Layers[OverworldLayers.Landscape(OverworldBiome.Grassland)];
        foreach (var feature in new[] { OverworldLayers.Trees, OverworldLayers.Mountains, OverworldLayers.NavigableWater })
        {
            int count = 0;
            for (int y=0;y<grid.Height;y++) for (int x=0;x<grid.Width;x++)
            {
                if (!grid.Layers[feature][x,y]) continue;
                if (grass[x,y]) count++;
                if (feature != OverworldLayers.NavigableWater)
                    Assert.False(grid.IsGround(new GridPoint(x,y)));
            }
            Assert.True(count >= 10, $"Grassland needs visible {feature} patches, found {count}.");
        }
        foreach (var at in grid.Locations.Values)
        for (int dy=-2;dy<=2;dy++) for (int dx=-2;dx<=2;dx++)
        {
            var p = new GridPoint(at.X+dx,at.Y+dy);
            Assert.True(grid.IsGround(p));
            Assert.False(grid.RequiresBoat(p));
        }
        for (int y=0;y<grid.Height;y++) for (int x=0;x<grid.Width;x++)
            if (grid.Layers[OverworldLayers.Roads][x,y]) Assert.True(grid.IsGround(new GridPoint(x,y)));

        // Inspect the closed floor outline of each leaf reward, independently of its gate.
        int irregular = 0, pockets = 0;
        foreach (var route in campaign.Routes.Where(r => r.HasGate && !r.IsWarp && !r.IsProgressionBoundary &&
            campaign.Routes.Count(other => other.From == r.To || other.To == r.To) == 1))
        {
            pockets++;
            var center = grid.Locations[route.To];
            var cells = new HashSet<GridPoint> { center }; var pending = new Queue<GridPoint>(); pending.Enqueue(center);
            var radii = new List<double>();
            while (pending.Count > 0)
            {
                var at = pending.Dequeue(); bool boundary = false;
                foreach (var next in OverworldMovement.Neighbors(at).Where(p => p.X == at.X || p.Y == at.Y))
                {
                    if (!grid.IsGround(next) || grid.LockAt(next) != null) { boundary = true; continue; }
                    if (cells.Add(next)) pending.Enqueue(next);
                }
                if (boundary) radii.Add(Math.Sqrt(Math.Pow(at.X-center.X,2)+Math.Pow(at.Y-center.Y,2)));
            }
            if (radii.Max()-radii.Min() >= 2) irregular++;
        }
        Assert.True(irregular >= pockets*.5, $"Only {irregular}/{pockets} pocket outlines vary by two tiles or more.");
    }

}
