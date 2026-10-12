using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class FinaleTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(17)] [InlineData(42)] [InlineData(99)] [InlineData(2026)]
    public void FiveMapsAreDeterministicAndReachableWithoutBoat(int seed)
    {
        var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
        var plan = new FinalePlan(context.Campaign, context.Grid, false, Array.Empty<string>());
        var again = new FinalePlan(context.Campaign, context.Grid, false, Array.Empty<string>());
        Assert.Equal(5, plan.Floors.Count); Assert.Same(context.Grid, plan.Floors[0].Grid);
        Assert.Equal(5, plan.Floors.Select(f => f.Seed).Distinct().Count());
        Assert.Equal(plan.Floors.Select(f => f.Fingerprint), again.Floors.Select(f => f.Fingerprint));
        Assert.Equal(plan.Floors.SelectMany(f => f.Enemies).Select(e => (e.Id, e.Position, e.Roll, e.Family)),
            again.Floors.SelectMany(f => f.Enemies).Select(e => (e.Id, e.Position, e.Roll, e.Family)));
        var roster = plan.Floors.SelectMany(f => f.Enemies).Where(e => e.IsMimic).Select(e => e.Family).ToHashSet();
        Assert.All(FinaleRoster.Required, f => Assert.Contains(f, roster));
        Assert.Equal("Castle Monster", plan.Floors[4].Enemies.Single(e => e.LocationId == plan.Floors[4].Campaign.StartLocationId).Family);
        foreach (var floor in plan.Floors)
        {
            floor.ValidateRoute();
            Assert.Equal(floor.Start, floor.CreateFloor().Start); Assert.Equal(floor.Stairs, floor.CreateFloor().Stairs);
            Assert.Equal(floor.Campaign.Regions.Count, floor.Regions.Count);
            Assert.Equal(floor.Regions.Count * 5, floor.Traps.Count);
            var trapped = floor.Traps.Select(t => t.Cell).ToHashSet();
            Assert.Equal(trapped.Count, floor.Traps.Count);
            var unlocked = new HashSet<string>();
            foreach (var region in floor.Regions)
            {
                unlocked.Add(region.RegionId);
                var safe = GridSearch.VisitOrder(floor.Start, p => OverworldMovement.Neighbors(p)
                    .Where(q => !trapped.Contains(q) && floor.CanStep(p, q, unlocked) &&
                        (p.X == q.X || p.Y == q.Y || !trapped.Contains(new GridPoint(p.X, q.Y)) && !trapped.Contains(new GridPoint(q.X, p.Y))))).ToHashSet();
                Assert.All(region.Enemies, e => Assert.Contains(e.Position, safe));
                Assert.Equal(DungeonLayoutProfile.Options(floor.Seed, region.Biome, 4).EnemyCount, region.Enemies.Count(e => !e.IsMimic));
                foreach (var enemy in region.Enemies)
                {
                    Assert.Equal(region.RegionId, floor.RegionAt(enemy.Position));
                    Assert.False(floor.Grid.RequiresBoat(enemy.Position));
                    if (!enemy.IsMimic) Assert.DoesNotContain(enemy.Position, floor.Reserved);
                    else Assert.Contains(enemy.Family, enemy.IsTown ? FinaleRoster.Towns : FinaleRoster.Dungeons);
                }
            }
        }
    }

    internal static void Clear(FinaleSession session)
    {
        while (!session.FinalExitCompleted)
        {
            foreach (var region in session.Floor.Regions)
            {
                foreach (var enemy in region.Enemies)
                {
                    if (enemy.IsMimic)
                    {
                        var from = GridSteps.EightWay.Select(d => new GridPoint(enemy.Position.X + d.X, enemy.Position.Y + d.Y))
                            .First(p => session.CanReveal(enemy.Id, p));
                        Assert.True(session.Reveal(enemy.Id, from));
                    }
                    Assert.True(session.RecordDeath(enemy.Id));
                }
                session.ReconcileClearance();
            }
            Assert.True(session.UseStairs(session.Floor.Stairs));
        }
    }

    [Fact]
    public void HiddenMimicsPackMembersAndSummonsMustActuallyDieAndStairsAreMandatory()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        c.State.PendingDungeon = c.Campaign.FinalLocationId;
        c.Resolved.UnionWith(c.Campaign.Routes.Select(r => r.Id));
        var s = c.BeginFinale();
        Assert.Single(s.Unlocked); Assert.Empty(s.Defeated); Assert.False(c.CompleteDungeon(true));
        var first = s.Floor.Regions[0];
        var source = first.Enemies.First(e => !e.IsMimic);
        string child = source.Id + "/member-1", summon = child + "/summon-1";
        Assert.True(s.RegisterChild(source.Id, child)); Assert.True(s.RegisterChild(child, summon));
        Assert.False(s.RegisterChild(source.Id, child));
        foreach (var e in first.Enemies.Where(e => !e.IsMimic)) Assert.True(s.RecordDeath(e.Id));
        Assert.Empty(s.ReconcileClearance()); Assert.Single(s.Unlocked);
        foreach (var e in first.Enemies.Where(e => e.IsMimic))
        {
            Assert.False(s.RecordDeath(e.Id));
            Assert.False(s.Reveal(e.Id, s.Floor.Stairs));
            var from = GridSteps.EightWay.Select(d => new GridPoint(e.Position.X + d.X, e.Position.Y + d.Y)).First(p => s.CanReveal(e.Id, p));
            Assert.True(s.Reveal(e.Id, from)); Assert.True(s.RecordDeath(e.Id));
        }
        Assert.True(s.RecordDeath(child)); Assert.Empty(s.ReconcileClearance());
        Assert.False(s.UseStairs(s.Floor.Stairs));
        Assert.True(s.RecordDeath(summon)); Assert.Single(s.ReconcileClearance()); Assert.Empty(s.ReconcileClearance());
        Assert.False(s.RecordDeath(summon)); Assert.False(c.CompleteDungeon(true));
        Assert.False(s.RegisterChild(source.Id, source.Id + "/late"));
        Assert.True(c.CompleteDungeon(false));
        c.State.PendingDungeon = c.Campaign.FinalLocationId;
        s = c.BeginFinale(); Assert.Single(s.Unlocked); Assert.Empty(s.Defeated);
        Clear(s); Assert.Equal(new[] { 1, 2, 3, 4, 5 }, s.StairTransitions);
        Assert.False(s.UseStairs(s.Floor.Stairs)); Assert.True(c.CompleteDungeon(true));
        Assert.Equal("Town", c.State.Scene); Assert.Equal(c.Campaign.StartLocationId, c.State.LocationId);
        Assert.Null(c.Finale); Assert.True(c.State.Finished);
        c.Position = c.Grid.Locations[c.Campaign.FinalLocationId]; Assert.False(c.BeginDungeon());
        c.Position = c.Grid.Locations["repeatable-0"]; Assert.True(c.BeginDungeon()); Assert.True(c.CompleteDungeon(true));
        Assert.True(c.State.Finished);
    }

    [Fact]
    public void LockedRegionsRejectStepsWarpsAndDeathsRegardlessOfCampaignKeys()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var s = new FinaleSession(new FinalePlan(c.Campaign, c.Grid));
        foreach (var region in s.Floor.Regions.Skip(1))
        foreach (var e in region.Enemies)
        { Assert.False(s.IsWalkable(e.Position)); Assert.False(s.RecordDeath(e.Id)); }
        foreach (var warp in s.Floor.Grid.Warps)
        foreach (var from in new[] { s.Floor.Grid.Locations[warp.From], s.Floor.Grid.Locations[warp.To] })
            Assert.All(s.WarpDestinations(from), w => Assert.Contains(s.Floor.RegionAt(w.Destination), s.Unlocked));
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), c.Capture());
        Assert.Null(restored.Finale);
    }
}
