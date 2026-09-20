using EternalEnigma.ConsoleExplorer;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;
using CampaignDefinition = EternalEnigma.Core.Progression.Campaign;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class ConsoleExplorerTests
{
    [Theory]
    [InlineData(LockForm.Obstacle, true)]
    [InlineData(LockForm.Interaction, true)]
    [InlineData(LockForm.Area, false)]
    public void PhysicalCrossingLatchesOnlyPermanentLocks(LockForm form, bool remainsOpen)
    {
        var session = Create(form);
        Assert.False(session.FastTravel("end"));
        Assert.False(session.Move(-1, 0));
        session.ClaimRewards();
        Assert.False(session.Held.Contains(Capability.Climb));
        Assert.True(session.ToggleCompanion("climber"));
        for (int i = 0; i < 14; i++) Assert.True(session.Move(1, 0));
        Assert.Equal("end", session.Location?.Id);
        Assert.True(session.ToggleCompanion("climber"));
        Assert.True(session.FastTravel("start"));
        for (int i = 0; i < 6; i++) Assert.True(session.Move(1, 0));
        Assert.Equal(remainsOpen, session.Move(1, 0));
        Assert.False(session.ToggleCompanion("climber"));
    }

    [Fact]
    public void RewardsRequireLocationAndCameraFollowsMovement()
    {
        var session = Create(LockForm.Obstacle);
        var renderer = new MapRenderer(session);
        Assert.Equal("@::::", renderer.Render(5, 1)[0]);
        Assert.True(session.Move(1, 0));
        session.ClaimRewards();
        Assert.Empty(session.Roster);
        Assert.False(session.Move(2, 0));
        Assert.True(session.Move(-1, 0));
        session.ClaimRewards();
        session.ClaimRewards();
        Assert.Single(session.Roster);
        session.ToggleCompanion("climber");
        for (int i = 0; i < 10; i++) Assert.True(session.Move(1, 0));
        Assert.Equal("::@::", renderer.Render(5, 1)[0]);
        for (int i = 0; i < 4; i++) Assert.True(session.Move(1, 0));
        Assert.Equal("::::@", renderer.Render(5, 1)[0]);
    }

    [Fact]
    public void WarpMenuRequiresLaterKeyAndTeleportsBothWays()
    {
        var session = Create(LockForm.None, warp: true);
        Assert.Contains("Requires: Later key", session.WarpLabel(Assert.Single(session.WarpsHere)));
        Assert.False(session.Warp("gate"));
        Assert.Equal("start", session.Location!.Id);
        for (int i = 0; i < 14; i++) Assert.True(session.Move(1, 0));
        Assert.False(session.Warp("gate"));
        session.ClaimRewards();
        Assert.Equal(new[] { "Later key" }, session.CollectedKeys);
        Assert.True(session.Warp("gate"));
        Assert.Equal("start", session.Location!.Id);
        Assert.True(session.Warp("gate"));
        Assert.Equal("end", session.Location!.Id);
        Assert.True(session.Move(-1, 0));
        Assert.Empty(session.WarpsHere);
        Assert.False(session.Warp("gate"));
    }

    private static ExplorerSession Create(LockForm form, bool warp = false)
    {
        var campaign = new CampaignDefinition(1, 1, "start", "end", Array.Empty<ActivatedCapability>(),
            new[] { new CampaignRegion("region", "test", 0) },
            new[] { new CampaignLocation("start", "region", 0, LocationKind.Town), new CampaignLocation("end", "region", 0, LocationKind.Town) },
            new[] { warp ? new CampaignRoute("gate", "start", "end", Requirement.Open, LockForm.None,
                shortcutKind: ShortcutKind.Keyed, keyId: "Later key", keyLocationId: "end", isWarp: true) :
                new CampaignRoute("gate", "start", "end", new Requirement(CapabilitySet.Of(Capability.Climb)), form) },
            new[] { new CapabilitySource("reward", "start", Capability.Climb, "climber") },
            new[] { new CampaignCompanion("climber", Capability.Climb) });
        var ground = new bool[15, 1];
        for (int x = 0; x < 15; x++) ground[x, 0] = true;
        var layers = new Dictionary<string, GridLayer>
        {
            [OverworldLayers.Ground] = new(ground), [OverworldLayers.Roads] = new(ground),
            [OverworldLayers.Water] = new(new bool[15, 1]), [OverworldLayers.Trees] = new(new bool[15, 1])
        };
        var grid = new OverworldGrid(campaign, layers,
            new Dictionary<string, GridPoint> { ["start"] = new(0, 0), ["end"] = new(14, 0) },
            new Dictionary<string, IReadOnlyList<GridPoint>>(), warp ? Array.Empty<GridLock>() : new[] { new GridLock("gate", new[] { new GridPoint(7, 0) }) });
        return new ExplorerSession(campaign, grid);
    }
}
