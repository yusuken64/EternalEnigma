using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class OverworldGridTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GeneratedMapIsDeterministicAndEveryLocationHasAPosition(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var again = OverworldGridGenerator.Generate(campaign);
        Assert.Equal(256, grid.Width);
        Assert.Equal(256, grid.Height);
        Assert.Equal(campaign.Locations.Count, grid.Locations.Count);
        Assert.Equal(campaign.Routes.Count(r => r.HasGate && !r.IsWarp), grid.Locks.Count);
        Assert.True(OverworldGridValidator.Validate(campaign, grid).IsValid);
        foreach (var layer in grid.Layers)
            Assert.Equal(layer.Value.ToArray().Cast<bool>(), again.Layers[layer.Key].ToArray().Cast<bool>());
        Assert.Equal(grid.Locations.OrderBy(p => p.Key), again.Locations.OrderBy(p => p.Key));
        Assert.True(grid.Layers[OverworldLayers.PlayerStart][grid.PlayerStart.X, grid.PlayerStart.Y]);
    }

    [Fact]
    public void ReturnedMasksCannotMutateTheWorldAndRegionMasksPartitionGround()
    {
        var campaign = CampaignGenerator.Generate(42);
        var grid = OverworldGridGenerator.Generate(campaign);
        var cells = grid.Layers[OverworldLayers.Ground].ToArray();
        cells[grid.PlayerStart.X, grid.PlayerStart.Y] = false;
        Assert.True(grid.IsGround(grid.PlayerStart));
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            int regions = campaign.Regions.Count(r => grid.Layers[OverworldLayers.Region(r.Id)][x, y]);
            Assert.Equal(grid.IsGround(new GridPoint(x, y)) ? 1 : 0, regions);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    public void GridReachabilityMatchesCampaignForSampledCapabilityAndResolvedLockStates(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var capabilities = campaign.Manifest.Select(c => c.Id).ToArray();
        int fullMask = (1 << capabilities.Length) - 1;
        foreach (int mask in Enumerable.Range(0, 32).Select(i => i * fullMask / 31))
        {
            var held = CapabilitySet.From(capabilities.Where((_, i) => (mask & (1 << i)) != 0));
            foreach (var resolved in new[] { new HashSet<string>(), campaign.Routes.Where(r => r.Latches).Select(r => r.Id).ToHashSet() })
            {
                var expected = new HashSet<string> { campaign.StartLocationId };
                var queue = new Queue<string>(); queue.Enqueue(campaign.StartLocationId);
                while (queue.Count > 0)
                {
                    string location = queue.Dequeue();
                    foreach (var route in campaign.Routes)
                    {
                        var next = route.Other(location);
                        if (next != null && route.CanTraverse(held, resolved) && expected.Add(next)) queue.Enqueue(next);
                    }
                }
                var cells = new HashSet<GridPoint> { grid.PlayerStart };
                var pending = new Queue<GridPoint>(); pending.Enqueue(grid.PlayerStart);
                while (pending.Count > 0)
                {
                    var at = pending.Dequeue();
                    foreach (var next in OverworldMovement.Neighbors(at))
                        if (grid.CanStep(at, next, held, resolved) && cells.Add(next)) pending.Enqueue(next);
                }
                Assert.Equal(expected.OrderBy(id => id), grid.Locations.Where(p => cells.Contains(p.Value)).Select(p => p.Key).OrderBy(id => id));
            }
        }
    }

    [Fact]
    public void AreaLocksNeverLatchButOtherLocksCanRemainResolved()
    {
        var campaign = CampaignGenerator.Generate(42);
        var grid = OverworldGridGenerator.Generate(campaign);
        foreach (var gate in grid.Locks)
        {
            var route = campaign.Routes.Single(r => r.Id == gate.RouteId);
            var cell = gate.Cells[0];
            Assert.False(grid.IsWalkable(cell, CapabilitySet.Empty));
            Assert.Equal(route.ShortcutKind != ShortcutKind.FarSide && route.ShortcutKind != ShortcutKind.Keyed, grid.IsWalkable(cell, route.Requirement.Alternatives[0]));
            Assert.Equal(route.Latches || route.ShortcutKind == ShortcutKind.FarSide || route.ShortcutKind == ShortcutKind.Keyed, grid.IsWalkable(cell, CapabilitySet.Empty, new HashSet<string> { gate.RouteId }));
        }
    }

    [Fact]
    public void SealingCornersRejectDiagonalSqueezesAndMultiTileMoves()
    {
        bool Walkable(GridPoint p) => (p.X == 0 && p.Y == 0) || (p.X == 1 && p.Y == 1);
        Assert.False(OverworldMovement.CanStep(new GridPoint(0, 0), new GridPoint(1, 1), Walkable));
        Assert.False(OverworldMovement.CanStep(new GridPoint(0, 0), new GridPoint(2, 0), _ => true));
    }

    [Fact]
    public void SmallMapsFailAndLocalCyclesAreSupported()
    {
        var campaign = CampaignGenerator.Generate(42);
        Assert.Throws<ArgumentException>(() => OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(16, 16)));
        var open = campaign.Routes.First(r => r.Requirement.IsOpen);
        var parallel = new CampaignRoute("extra-route", open.From, open.To, Requirement.Open, LockForm.None);
        var cyclic = CampaignGeneratorTests.With(campaign, routes: campaign.Routes.Concat(new[] { parallel }));
        Assert.True(OverworldGridValidator.Validate(cyclic, OverworldGridGenerator.Generate(cyclic)).IsValid);
    }

    [Fact]
    public void SeedSweepFitsDefaultDimensionsAndPreservesTopology()
    {
        for (int seed = -16; seed < 16; seed++)
        {
            var campaign = CampaignGenerator.Generate(seed);
            var grid = OverworldGridGenerator.Generate(campaign);
            Assert.Equal(campaign.Routes.Count, grid.Routes.Count);
        }
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(42, 6)]
    [InlineData(-1, 6)]
    [InlineData(42, 1)]
    [InlineData(42, 12)]
    public void ExpansionPreservesEmbeddingAndTerrainAtGates(int seed, int radius)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var original = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: 0));
        var expanded = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: radius));
        var again = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: radius));
        Assert.Equal(original.CampaignFingerprint, expanded.CampaignFingerprint);
        Assert.Equal(original.Locations, expanded.Locations);
        foreach (var route in original.Routes) Assert.Equal(route.Value, expanded.Routes[route.Key]);
        foreach (var layer in expanded.Layers)
            Assert.Equal(layer.Value.ToArray().Cast<bool>(), again.Layers[layer.Key].ToArray().Cast<bool>());
        foreach (string layer in new[] { OverworldLayers.Roads, OverworldLayers.Locks, OverworldLayers.AreaLocks,
            OverworldLayers.ObstacleLocks, OverworldLayers.InteractionLocks })
            Assert.Equal(original.Layers[layer].ToArray().Cast<bool>(), expanded.Layers[layer].ToArray().Cast<bool>());
        foreach (var gate in original.Locks)
        {
            Assert.Equal(gate.Cells, expanded.Locks.Single(g => g.RouteId == gate.RouteId).Cells);
            foreach (var cell in gate.Cells)
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            foreach (string layer in new[] { OverworldLayers.Ground, OverworldLayers.Trees, OverworldLayers.Mountains })
                Assert.Equal(original.Layers[layer][cell.X + dx, cell.Y + dy], expanded.Layers[layer][cell.X + dx, cell.Y + dy]);
        }
        var distances = new Dictionary<GridPoint, int>();
        var queue = new Queue<GridPoint>();
        for (int y = 0; y < original.Height; y++) for (int x = 0; x < original.Width; x++)
        {
            var cell = new GridPoint(x, y);
            // The legacy floor consists exactly of route paths and 3x3 location clearings.
            bool legacy = original.Layers[OverworldLayers.Roads][x, y] || original.Locations.Values.Any(p => Math.Abs(p.X - x) <= 1 && Math.Abs(p.Y - y) <= 1);
            Assert.Equal(legacy, original.IsGround(cell));
            if (legacy)
            {
                Assert.True(expanded.IsGround(cell));
                if (original.LockAt(cell) == null) { distances[cell] = 0; queue.Enqueue(cell); }
            }
        }
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var next in OverworldMovement.Neighbors(at).Where(p => p.X == at.X || p.Y == at.Y))
                if (expanded.IsGround(next) && expanded.LockAt(next) == null && !distances.ContainsKey(next))
                { distances[next] = distances[at] + 1; queue.Enqueue(next); }
        }
        for (int y = 0; y < expanded.Height; y++) for (int x = 0; x < expanded.Width; x++)
        {
            var cell = new GridPoint(x, y);
            Assert.Equal(expanded.IsGround(cell) ? 1 : 0, campaign.Regions.Count(r => expanded.Layers[OverworldLayers.Region(r.Id)][x, y]));
            if (expanded.IsGround(cell) && !original.IsGround(cell))
            {
                Assert.True(distances.TryGetValue(cell, out int distance));
                Assert.InRange(distance, 1, radius);
                Assert.Equal(!expanded.RequiresBoat(cell), expanded.IsWalkable(cell, CapabilitySet.Empty));
                Assert.False(expanded.Layers[OverworldLayers.Roads][x, y]);
            }
        }
    }

    [Fact]
    public void StartingLocationsStayTogetherAndExpansionRemainsNearOriginalTerrain()
    {
        var campaign = CampaignGenerator.Generate(42);
        var grid = OverworldGridGenerator.Generate(campaign);
        var original = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: 0));
        string startingRegion = campaign.Locations.Single(l => l.Id == campaign.StartLocationId).RegionId;
        var startLocations = campaign.Locations.Where(l => l.RegionId == startingRegion)
            .Select(l => grid.Locations[l.Id]).ToArray();
        Assert.True(startLocations.Max(p => p.Y) - startLocations.Min(p => p.Y) <= 60,
            "Starting locations must not straddle the full campaign height.");
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
        {
            var point = new GridPoint(x, y);
            if (!grid.IsGround(point) || original.IsGround(point)) continue;
            string region = campaign.Regions.Single(r => grid.Layers[OverworldLayers.Region(r.Id)][x, y]).Id;
            int radius = region == startingRegion ? 3 : 6;
            Assert.Contains(Enumerable.Range(-radius, 2 * radius + 1).SelectMany(dy => Enumerable.Range(-radius, 2 * radius + 1).Select(dx => new GridPoint(x + dx, y + dy))),
                p => original.IsGround(p) && Math.Abs(p.X - x) + Math.Abs(p.Y - y) <= radius);
        }
    }

    [Fact]
    public void DefaultExpansionCreatesBroadOpenSpaces()
    {
        var campaign = CampaignGenerator.Generate(42);
        var original = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: 0));
        var expanded = OverworldGridGenerator.Generate(campaign);
        Assert.True(expanded.Layers[OverworldLayers.Walkable].ToArray().Cast<bool>().Count(v => v) >=
            2 * original.Layers[OverworldLayers.Walkable].ToArray().Cast<bool>().Count(v => v));
        int patches = 0;
        var used = new HashSet<GridPoint>();
        for (int y = 0; y < expanded.Height - 4; y++) for (int x = 0; x < expanded.Width - 4; x++)
        {
            var patch = Enumerable.Range(0, 25).Select(i => new GridPoint(x + i % 5, y + i / 5)).ToArray();
            if (patch.Any(p => used.Contains(p) || !expanded.IsWalkable(p, CapabilitySet.Empty))) continue;
            foreach (var p in patch) used.Add(p);
            patches++;
        }
        Assert.True(patches >= 2, $"Only {patches} disjoint 5x5 spaces.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    public void InvalidExpansionRadiusIsRejected(int radius) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new OverworldGridOptions(areaExpansionRadius: radius));

    [Fact]
    public void ValidatorRejectsCarvedBypassAroundARealizedGate()
    {
        var campaign = CampaignGenerator.Generate(42);
        var grid = OverworldGridGenerator.Generate(campaign);
        var gate = grid.Locks[0];
        var ground = grid.Layers[OverworldLayers.Ground].ToArray();
        for (int y = gate.Cells.Min(c => c.Y) - 1; y <= gate.Cells.Max(c => c.Y) + 1; y++)
        for (int x = gate.Cells.Min(c => c.X) - 1; x <= gate.Cells.Max(c => c.X) + 1; x++) ground[x, y] = true;
        var layers = grid.Layers.ToDictionary(p => p.Key, p => p.Value);
        layers[OverworldLayers.Ground] = new GridLayer(ground);
        var broken = new OverworldGrid(campaign, layers, grid.Locations.ToDictionary(p => p.Key, p => p.Value),
            grid.Routes.ToDictionary(p => p.Key, p => p.Value), grid.Locks);
        var result = OverworldGridValidator.Validate(campaign, broken);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("bypass:", StringComparison.Ordinal));
    }
}
