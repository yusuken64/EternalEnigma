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
        Assert.Equal(campaign.Routes.Count(r => !r.Requirement.IsOpen), grid.Locks.Count);
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

    [Fact]
    public void GridReachabilityMatchesCampaignForSampledCapabilityAndResolvedLockStates()
    {
        var campaign = CampaignGenerator.Generate(42);
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
            Assert.True(grid.IsWalkable(cell, route.Requirement.Alternatives[0]));
            Assert.Equal(route.Latches, grid.IsWalkable(cell, CapabilitySet.Empty, new HashSet<string> { gate.RouteId }));
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
    public void SmallMapsAndUnsupportedCyclesFailExplicitly()
    {
        var campaign = CampaignGenerator.Generate(42);
        Assert.Throws<ArgumentException>(() => OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(16, 16)));
        var open = campaign.Routes.First(r => r.Requirement.IsOpen);
        var parallel = new CampaignRoute("extra-route", open.From, open.To, Requirement.Open, LockForm.None);
        var cyclic = CampaignGeneratorTests.With(campaign, routes: campaign.Routes.Concat(new[] { parallel }));
        Assert.Throws<NotSupportedException>(() => OverworldGridGenerator.Generate(cyclic));
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

    [Fact]
    public void ValidatorRejectsCarvedBypassAroundARealizedGate()
    {
        var campaign = CampaignGenerator.Generate(42);
        var grid = OverworldGridGenerator.Generate(campaign);
        var gate = grid.Locks[0];
        var ground = grid.Layers[OverworldLayers.Ground].ToArray();
        int left = gate.Cells.Min(c => c.X) - 1, right = gate.Cells.Max(c => c.X) + 1, y = gate.Cells[0].Y;
        for (int x = left; x <= right; x++) ground[x, y + 1] = true;
        var layers = grid.Layers.ToDictionary(p => p.Key, p => p.Value);
        layers[OverworldLayers.Ground] = new GridLayer(ground);
        var broken = new OverworldGrid(campaign, layers, grid.Locations.ToDictionary(p => p.Key, p => p.Value),
            grid.Routes.ToDictionary(p => p.Key, p => p.Value), grid.Locks);
        var result = OverworldGridValidator.Validate(campaign, broken);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("bypass:", StringComparison.Ordinal));
    }
}
