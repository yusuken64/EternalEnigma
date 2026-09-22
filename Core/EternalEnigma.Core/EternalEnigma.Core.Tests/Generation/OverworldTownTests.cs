using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class OverworldTownTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void WalledTownsHaveOneDryEntranceAndRoadsAvoidTheirInterior(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var grid = OverworldGridGenerator.Generate(campaign);
        var all = CapabilitySet.From(campaign.Manifest.Select(c => c.Id));
        var resolved = campaign.Routes.Select(r => r.Id).ToHashSet();
        Assert.Equal(campaign.Locations.Count(l => l.Kind == LocationKind.Town), grid.TownFootprints.Count);
        var occupied = new HashSet<GridPoint>();
        foreach (var town in grid.TownFootprints)
        {
            Assert.Equal(25, town.Cells.Count);
            Assert.Equal(15, town.Walls.Count);
            Assert.Equal(9, town.Interior.Count);
            Assert.Equal(grid.Locations[town.LocationId], town.Entrance);
            Assert.Equal(new[] { town.Approach }, OverworldMovement.Neighbors(town.Entrance).Where(p => grid.CanStep(town.Entrance, p, all, resolved)));
            Assert.True(grid.CanStep(town.Approach, town.Entrance, all, resolved));
            Assert.False(grid.RequiresBoat(town.Entrance));
            Assert.False(grid.RequiresBoat(town.Approach));
            foreach (var cell in town.Cells)
            {
                Assert.True(occupied.Add(cell));
                Assert.True(grid.Layers[OverworldLayers.Reserved][cell.X, cell.Y]);
                Assert.False(grid.Layers[OverworldLayers.Trees][cell.X, cell.Y]);
                Assert.False(grid.Layers[OverworldLayers.Mountains][cell.X, cell.Y]);
                Assert.Null(grid.LockAt(cell));
                if (cell.Equals(town.Entrance)) continue;
                Assert.False(grid.IsWalkable(cell, all, resolved));
                Assert.False(grid.Layers[OverworldLayers.Roads][cell.X, cell.Y]);
                Assert.DoesNotContain(cell, grid.Locations.Values);
                Assert.All(grid.Routes.Values, path => Assert.DoesNotContain(cell, path));
                Assert.All(OverworldMovement.Neighbors(cell), from => Assert.False(grid.CanStep(from, cell, all, resolved)));
            }
        }
        Assert.True(OverworldGridValidator.Validate(campaign, grid).IsValid);
    }

    [Fact]
    public void StartingGatewayKeepsTheTownDungeonDepartureLock()
    {
        var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Sandbox, 42));
        var town = context.Grid.TownFootprints.Single(t => t.LocationId == context.Campaign.StartLocationId);
        Assert.Equal(town.Entrance, context.Position);
        Assert.Equal(town.LocationId, context.Location!.Id);
        Assert.False(context.Grid.CanStep(town.Entrance, town.Approach, context.Held, context.Resolved));
        var unlocked = context.Campaign.Routes.Select(r => r.Id).ToHashSet();
        var all = CapabilitySet.From(context.Campaign.Manifest.Select(c => c.Id));
        Assert.True(context.Grid.CanStep(town.Entrance, town.Approach, all, unlocked));
        context.Position = town.Approach;
        Assert.Null(context.Location);
        context.Position = town.Entrance;
        Assert.Equal(town.LocationId, context.Location!.Id);
    }
}
