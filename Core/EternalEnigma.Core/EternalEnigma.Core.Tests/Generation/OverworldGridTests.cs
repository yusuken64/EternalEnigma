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
    [InlineData(int.MaxValue)]
    public void GeneratedMapIsDeterministicAndEveryLocationHasAPosition(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var again = OverworldGridGenerator.Generate(campaign);
        Assert.Equal(256, grid.Width);
        Assert.Equal(256, grid.Height);
        var ground = Enumerable.Range(0, grid.Width * grid.Height).Select(i => new GridPoint(i % grid.Width, i / grid.Width)).Where(grid.IsGround).ToArray();
        // 250 = width/height minus the 3-tile border reserved as ocean on every edge; the warped
        // coastline can legitimately stretch a peninsula close to that bound.
        Assert.InRange(ground.Max(p => p.X) - ground.Min(p => p.X) + 1, 1, 250);
        Assert.InRange(ground.Max(p => p.Y) - ground.Min(p => p.Y) + 1, 1, 250);
        Assert.Equal(campaign.Locations.Count, grid.Locations.Count);
        Assert.Equal(campaign.Routes.Count(r => r.HasGate && !r.IsWarp && !r.IsTownExit), grid.Locks.Count);
        Assert.True(OverworldGridValidator.Validate(campaign, grid).IsValid);
        foreach (var interior in campaign.Locations.Where(l => l.ParentTownId != null))
        {
            var at = grid.Locations[interior.Id];
            Assert.Equal(grid.Locations[interior.ParentTownId!], at);
            Assert.False(grid.Layers[OverworldLayers.StoryDungeons][at.X, at.Y]);
            Assert.False(grid.Layers[OverworldLayers.RepeatableDungeons][at.X, at.Y]);
        }
        foreach (var layer in grid.Layers)
            Assert.Equal(layer.Value.ToArray().Cast<bool>(), again.Layers[layer.Key].ToArray().Cast<bool>());
        Assert.Equal(grid.Locations.OrderBy(p => p.Key), again.Locations.OrderBy(p => p.Key));
        Assert.True(grid.Layers[OverworldLayers.PlayerStart][grid.PlayerStart.X, grid.PlayerStart.Y]);
        foreach (var route in campaign.Routes.Where(r => !r.IsWarp))
        {
            var path = grid.Routes[route.Id];
            for (int i=1;i<path.Count;i++)
                Assert.Equal(1,Math.Abs(path[i].X-path[i-1].X)+Math.Abs(path[i].Y-path[i-1].Y));
        }
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
        var open = campaign.Routes.First(r => !r.HasGate && campaign.Locations.Single(l => l.Id == r.To).ParentTownId == null);
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
    [InlineData(0, 0)]
    [InlineData(42, 6)]
    [InlineData(-1, 12)]
    [InlineData(int.MinValue, 6)]
    [InlineData(int.MaxValue, 6)]
    public void TerritoriesHaveBroadInteriorsSeparatedDestinationsAndShortPasses(int seed, int variation)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(areaExpansionRadius: variation));
        Assert.Equal(11, OverworldGrid.GenerationVersion);
        Assert.Equal(CampaignFingerprint.Compute(campaign), grid.CampaignFingerprint);
        foreach (var a in grid.Locations.Where(p => campaign.Locations.Single(l => l.Id == p.Key).ParentTownId == null))
        foreach (var b in grid.Locations.Where(b => campaign.Locations.Single(l => l.Id == b.Key).ParentTownId == null && StringComparer.Ordinal.Compare(a.Key,b.Key)<0))
            Assert.True(Math.Pow(a.Value.X-b.Value.X,2)+Math.Pow(a.Value.Y-b.Value.Y,2) >= (campaign.StarterAreaLocations.Contains(a.Key) && campaign.StarterAreaLocations.Contains(b.Key) ? 36 : 121));
        foreach (var gate in grid.Locks)
        {
            var gateRoute = campaign.Routes.Single(r => r.Id == gate.RouteId);
            if (gateRoute.Form == LockForm.Area && gateRoute.Requirement.Alternatives.All(alt => alt.Contains(Capability.Boat)))
            {
                // Boat-only Area gates are carved into a wide sea crossing rather than a short pass.
                Assert.True(gate.Cells.Count >= 7, "Sea crossing should carve a wide body of water, not a narrow strip.");
                continue;
            }
            Assert.InRange(gate.Cells.Count,2,4);
            var first = gate.Cells[0]; var last = gate.Cells[gate.Cells.Count-1];
            int dx = Math.Sign(last.X-first.X), dy = Math.Sign(last.Y-first.Y);
            foreach (var center in new[] { new GridPoint(first.X-dx*3,first.Y-dy*3), new GridPoint(last.X+dx*3,last.Y+dy*3) })
            for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++)
                Assert.True(grid.IsGround(new GridPoint(center.X+x,center.Y+y)), "Pass must meet broad ground within two approach tiles.");
        }
        foreach (var region in campaign.Regions)
        {
            if (grid.RegionBiomes[region.Id] == OverworldBiome.Water) continue;
            int count=0, broad=0;
            for(int y=2;y<grid.Height-2;y++) for(int x=2;x<grid.Width-2;x++)
            {
                if(!grid.Layers[OverworldLayers.Region(region.Id)][x,y]) continue;
                count++;
                bool open=true;
                for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++)
                    open &= grid.IsGround(new GridPoint(x+dx,y+dy)) && grid.LockAt(new GridPoint(x+dx,y+dy))==null;
                if(open) broad++;
            }
            Assert.True(broad >= count*.3, $"{region.Id}: {broad}/{count} broad cells");
        }
        for(int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++)
        {
            int count=Enum.GetValues<OverworldBiome>().Count(b=>grid.Layers[OverworldLayers.Landscape(b)][x,y]);
            Assert.InRange(count,0,1);
            if(grid.IsGround(new GridPoint(x,y))) Assert.Equal(1,count);
        }
        foreach(var objective in campaign.ReturnObjectives)
        foreach(var id in objective.DestinationIds)
        {
            var p=grid.Locations[id]; Assert.True(grid.Layers[OverworldLayers.Region("region-0")][p.X,p.Y]);
        }
        foreach(var route in campaign.Routes.Where(r=>r.IsProgressionBoundary))
        {
            var gate=grid.Locks.Single(g=>g.RouteId==route.Id);
            Assert.True(gate.Cells.Count<=6, "Consecutive stages must share a short pass.");
        }
    }

    [Theory]
    [InlineData(248,248)]
    [InlineData(320,288)]
    [InlineData(1024,248)]
    [InlineData(248,512)]
    public void SupportedMapSizesKeepOpenTerritories(int width,int height)
    {
        var campaign=CampaignGenerator.Generate(42);
        var grid=OverworldGridGenerator.Generate(campaign,new OverworldGridOptions(width,height));
        Assert.Equal(width,grid.Width); Assert.Equal(height,grid.Height);
        Assert.True(OverworldGridValidator.Validate(campaign,grid).IsValid);
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
            grid.Routes.ToDictionary(p => p.Key, p => p.Value), grid.Locks,
            grid.RegionBiomes.ToDictionary(p => p.Key, p => p.Value), grid.TownFootprints);
        var result = OverworldGridValidator.Validate(campaign, broken);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("bypass:", StringComparison.Ordinal));
    }
}
