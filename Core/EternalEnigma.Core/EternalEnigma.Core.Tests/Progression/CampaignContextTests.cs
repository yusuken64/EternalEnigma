using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;
public sealed class CampaignContextTests
{
    [Fact]
    public void TownAndInteriorRunsDoNotGenerateTheOverworldUntilItIsRequested()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        Assert.False(context.IsGridGenerated);
        Assert.Equal("town-0", context.Location!.Id);
        Assert.True(context.SetParty());
        Assert.True(context.BeginTownDungeon("story-0"));
        Assert.False(context.IsGridGenerated);
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.False(restored.IsGridGenerated);
        Assert.True(restored.RecoverInterruptedRun());
        Assert.False(restored.IsGridGenerated);
        Assert.True(restored.BeginTownDungeon("story-0"));
        Assert.True(restored.CompleteDungeon(true));
        Assert.True(restored.CanLeaveTown("town-0"));
        Assert.False(restored.IsGridGenerated);
        var grid = restored.Grid;
        Assert.True(restored.IsGridGenerated);
        Assert.Same(grid, restored.Grid);
        Assert.Equal(grid.PlayerStart, restored.Position);
        Assert.Same(restored.Gates, restored.Gates);
        Assert.Contains("town-exit", restored.Resolved);
    }

    [Fact]
    public void InteriorDungeonRequiresItsTownAndSurvivesSaveWithoutBecomingAnOverworldEntrance()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        Assert.False(context.BeginTownDungeon("story-1"));
        Assert.False(context.BeginTownDungeon("repeatable-0"));
        Assert.False(context.BeginTownDungeon("missing"));
        context.State.Scene = "Overworld";
        Assert.False(context.BeginTownDungeon("story-0"));
        Assert.False(context.BeginDungeon());
        context.State.Scene = "Town";
        Assert.True(context.BeginTownDungeon("story-0"));
        Assert.Equal("story-0", context.Location!.Id);
        Assert.False(context.SetParty());
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.Equal("story-0", restored.Location!.Id);
        Assert.True(restored.CompleteDungeon(true));
        Assert.Equal("town-0", restored.Location!.Id);
        Assert.Equal("Town", restored.State.Scene);
        Assert.Contains("story-0", restored.Completed);
        Assert.Contains("Town gate key", restored.Keys);
        Assert.True(restored.BeginTownDungeon("story-0"));
        Assert.True(restored.CompleteDungeon(true));
        Assert.Single(restored.Completed);
        Assert.Single(restored.Keys);
    }

    [Theory]
    [InlineData(0, OverworldLaunchMode.Campaign)]
    [InlineData(42, OverworldLaunchMode.Sandbox)]
    [InlineData(-1, OverworldLaunchMode.Campaign)]
    public void StarterEnclosureRequiresVictoryAndPhysicalUseEvenWithEveryCapability(int seed, OverworldLaunchMode mode)
    {
        var context = new CampaignContext(new(mode, seed));
        var gate = context.Campaign.Routes.Single(r => r.Id == "starter-exit");
        var all = CapabilitySet.From(context.Campaign.Manifest.Select(c => c.Id));
        HashSet<GridPoint> Reachable()
        {
            var result = new HashSet<GridPoint> { context.Grid.PlayerStart };
            var queue = new Queue<GridPoint>(result);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in OverworldMovement.Neighbors(at).Where(p => context.Grid.CanStep(at, p, all, context.Resolved) && OverworldMovement.CanStep(at, p, cell => context.Gates.IsWalkable(cell, all)))
                    .Concat(context.Grid.WarpDestinations(at, all, context.Resolved)))
                    if (result.Add(next)) queue.Enqueue(next);
            }
            return result;
        }
        Assert.Equal(new[] { "story-0", "town-0" }, context.Grid.Locations.Where(l => Reachable().Contains(l.Value)).Select(l => l.Key).OrderBy(x => x));
        Assert.False(context.CanLeaveTown("town-0"));
        context.Position = context.Grid.Locations["story-0"];
        Assert.False(context.Gates.CollectKey(gate, "story-0"));
        Assert.False(context.BeginDungeon());
        Assert.True(context.BeginTownDungeon("story-0")); Assert.False(context.BeginTownDungeon("story-0"));
        Assert.True(context.CompleteDungeon(true)); Assert.False(context.CompleteDungeon(true));
        Assert.True(context.CanLeaveTown("town-0"));
        Assert.Contains("town-exit", context.Resolved);
        Assert.DoesNotContain(gate.KeyId!, context.Keys);
        Assert.Equal(context.Campaign.StarterAreaLocations.OrderBy(x => x), context.Grid.Locations.Where(l => Reachable().Contains(l.Value)).Select(l => l.Key).OrderBy(x => x));
        context.Position = context.Grid.Locations["repeatable-0"];
        Assert.False(context.Gates.CollectKey(gate, "repeatable-0"));
        Assert.True(context.BeginDungeon()); Assert.True(context.CompleteDungeon(false));
        Assert.DoesNotContain(gate.KeyId!, context.Keys);
        context.Position = context.Grid.Locations["repeatable-0"];
        Assert.True(context.BeginDungeon());
        var pending = new CampaignContext(new(mode), context.Capture());
        Assert.True(pending.RecoverInterruptedRun());
        Assert.DoesNotContain(gate.KeyId!, pending.Keys);
        Assert.Equal("Overworld", pending.State.Scene);
        // Capture returns the state object, so use the restored context from here.
        context = pending;
        Assert.True(context.BeginDungeon()); Assert.True(context.CompleteDungeon(true));
        Assert.Contains(gate.KeyId!, context.Keys); Assert.DoesNotContain(gate.Id, context.Resolved);
        Assert.DoesNotContain(context.Grid.Locations["checkpoint-0"], Reachable());
        Assert.False(context.Gates.TryOpen(gate.Id, context.Position, all));
        var cells = Reachable();
        var approach = cells.First(p => context.Gates.Nearby(p).Any(r => r.Id == gate.Id));
        Assert.True(context.Gates.TryOpen(gate.Id, approach, all));
        Assert.Contains(context.Grid.Locations["checkpoint-0"], Reachable());
        Assert.Contains(gate.KeyId!, context.Keys);
        var restored = new CampaignContext(new(mode), context.Capture());
        Assert.Contains(gate.KeyId!, restored.Keys); Assert.Contains(gate.Id, restored.Resolved);
        Assert.All(context.Grid.Locks.Single(g => g.RouteId == gate.Id).Cells, p => Assert.True(restored.Gates.IsWalkable(p, all)));
    }
    [Fact]
    public void DefeatAndInterruptionDoNotAwardCompletionAndStableParametersSurviveRestore()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.LastTownId = "town-0";
        context.Position = context.Grid.Locations["story-0"];
        Assert.True(context.BeginTownDungeon("story-0")); Assert.True(context.CompleteDungeon(false));
        Assert.Equal("Town", context.State.Scene); Assert.Equal(context.Grid.Locations["town-0"], context.Position);
        Assert.Empty(context.Keys); Assert.Empty(context.Completed);
        context.Position = context.Grid.Locations["town-0"]; context.BeginTownDungeon("story-0");
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.True(restored.RecoverInterruptedRun()); Assert.False(restored.RecoverInterruptedRun());
        Assert.Equal("Town", restored.State.Scene); Assert.Equal(context.Position, restored.Position);
        Assert.Empty(restored.Keys);
        Assert.Equal(context.LocationSeed("story-0", 2), restored.LocationSeed("story-0", 2));
        Assert.NotEqual(context.LocationSeed("story-0", 1), context.LocationSeed("story-0", 2));
        Assert.NotEqual(context.LocationSeed("town-0"), context.LocationSeed("town-1"));
        Assert.Equal(new[] { (1, 5), (5, 10), (10, 20), (20, 30), (30, 40) }, Enumerable.Range(0, 5).Select(CampaignContext.Floors));
        restored.Position = restored.Grid.Locations[restored.Campaign.FinalLocationId];
        restored.BeginDungeon(); Assert.True(restored.CompleteDungeon(true)); Assert.True(restored.State.Finished);
        Assert.False(restored.CompleteDungeon(true));
    }
    [Fact]
    public void PartyIsTownOnlyAndBenchingDoesNotRemoveRosterMembers()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Sandbox));
        context.Roster.UnionWith(new[] { "ordinary-1", "ordinary-2", "ordinary-3", "ordinary-4" });
        Assert.True(context.SetParty("ordinary-1", "ordinary-2", "ordinary-3"));
        Assert.Equal(CapabilitySet.Empty, context.Held);
        Assert.False(context.SetParty(context.Roster.ToArray()));
        Assert.True(context.SetParty("ordinary-2")); Assert.Equal(4, context.Roster.Count);
        context.Position = context.Grid.Locations["repeatable-0"];
        Assert.False(context.SetParty("ordinary-1"));
    }
}
