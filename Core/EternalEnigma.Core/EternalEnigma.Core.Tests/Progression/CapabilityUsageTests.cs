using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class CapabilityUsageTests
{
    [Fact]
    public void LastRemoteLockRetainsAnOptionAndResettingItRestoresTheOption()
    {
        var gallery = new LockTestWorld(); var context = gallery.CreateContext();
        var routes = context.Campaign.Routes.Where(r => r.Requirement.Alternatives.Any(a => a.Contains(Capability.Remedy))).ToArray();
        Assert.True(routes.Length > 1);
        foreach (var route in routes.Skip(1)) context.Resolved.Add(route.Id);
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Remedy));
        context.Resolved.Add(routes[0].Id);
        Assert.False(CapabilityUsage.HasRemainingUse(context, Capability.Remedy));
        Assert.True(context.Permanent.Contains(Capability.Remedy));
        context.Resolved.Remove(routes[0].Id);
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Remedy));
    }

    [Theory]
    [InlineData(LockForm.Obstacle, ShortcutKind.None, false)]
    [InlineData(LockForm.Interaction, ShortcutKind.None, false)]
    [InlineData(LockForm.Area, ShortcutKind.None, true)]
    [InlineData(LockForm.Obstacle, ShortcutKind.Capability, true)]
    public void CompletedRoutesKeepOnlyOngoingTraversalUses(LockForm form, ShortcutKind shortcut, bool useful)
    {
        var route = new CampaignRoute("gate", "a", "b", new Requirement(CapabilitySet.Of(Capability.Climb)), form, shortcutKind: shortcut);
        var context = Context(route);
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Climb));
        context.Opened.Add(route.Id); context.Resolved.Add(route.Id);
        Assert.Equal(useful, CapabilityUsage.HasRemainingUse(context, Capability.Climb));
    }

    [Fact]
    public void UnclaimedRewardsAndCompletedEncountersRetainTheirPrerequisites()
    {
        var source = new CapabilitySource("boat", "a", Capability.Boat, prerequisites: new Requirement(CapabilitySet.Of(Capability.Engineering)));
        var context = Context(source: source);
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Engineering));
        context.Claimed.Add(source.Id);
        Assert.False(CapabilityUsage.HasRemainingUse(context, Capability.Engineering));
        context = Context(encounter: new EncounterDefinition(EncounterKind.BoulderCave, 0, source.Prerequisites));
        context.Completed.Add("b");
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Engineering));
    }

    [Fact]
    public void KeysRemainUntilEveryMatchingLockOpensAndOwnershipIsPreserved()
    {
        var gallery = new LockTestWorld(); var context = gallery.CreateContext();
        var key = context.Campaign.Routes.First(r => r.KeyId != null && !r.IsWarp).KeyId!;
        var routes = context.Campaign.Routes.Where(r => r.KeyId == key).ToArray();
        context.Resolved.Add(routes[0].Id);
        Assert.True(CapabilityUsage.HasRemainingKeyUse(context, key));
        context.Resolved.Add(routes[1].Id);
        Assert.False(CapabilityUsage.HasRemainingKeyUse(context, key));
        Assert.Contains(key, context.Keys);
        context.Keys.Add(CampaignContext.OpeningDayKey);
        Assert.False(CapabilityUsage.HasRemainingKeyUse(context, CampaignContext.OpeningDayKey));
        Assert.Contains(CampaignContext.OpeningDayKey, context.Capture().Keys);
    }

    [Fact]
    public void EveryAlternativeStaysVisibleAndBoatAlwaysRetainsItsPassiveUse()
    {
        var context = Context(new CampaignRoute("gate", "a", "b", new Requirement(
            CapabilitySet.Of(Capability.Climb), CapabilitySet.Of(Capability.Grapple)), LockForm.Obstacle));
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Climb));
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Grapple));
        context.Resolved.Add("gate");
        Assert.False(CapabilityUsage.HasRemainingUse(context, Capability.Climb));
        Assert.False(CapabilityUsage.HasRemainingUse(context, Capability.Grapple));
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.Boat));
    }

    [Fact]
    public void HomeKeyRetiresFromTheMenuWithoutReopeningItsChest()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.RequiresOpeningPreparation = true; context.State.HomeLessonVersion = 1;
        context.State.Scene = "Town"; context.State.LocationId = "town-0";
        Assert.True(context.ClaimHomeKey());
        Assert.True(CapabilityUsage.HasRemainingUse(context, Capability.HomeKey));
        Assert.True(context.UnlockHomeDoor());
        Assert.False(CapabilityUsage.HasRemainingUse(context, Capability.HomeKey));
        Assert.True(context.HasHomeKey);
        Assert.False(context.ClaimHomeKey());
        Assert.Contains(Capability.HomeKey, context.Capture().Permanent);
    }

    private static CampaignContext Context(CampaignRoute? route = null, CapabilitySource? source = null, EncounterDefinition? encounter = null)
    {
        var campaign = new Campaign(42, CampaignGenerator.Version, "a", "b",
            CapabilityCatalog.All.Select(c => new ActivatedCapability(c, CapabilityRole.Exploratory, 0)),
            new[] { new CampaignRegion("r", LockSkinCatalog.Highlands, 0) },
            new[] { new CampaignLocation("a", "r", 0, LocationKind.Landmark), new CampaignLocation("b", "r", 0, LocationKind.Landmark, encounter: encounter) },
            route == null ? Array.Empty<CampaignRoute>() : new[] { route },
            source == null ? Array.Empty<CapabilitySource>() : new[] { source }, Array.Empty<CampaignCompanion>());
        var grid = new OverworldGrid(campaign, new Dictionary<string, GridLayer> {
                [OverworldLayers.Ground] = new(new bool[,] { { true }, { true }, { true } }) },
            new Dictionary<string, GridPoint> { ["a"] = new(0, 0), ["b"] = new(2, 0) },
            new Dictionary<string, IReadOnlyList<GridPoint>>(), Array.Empty<GridLock>());
        return new CampaignContext(campaign, grid, CapabilitySet.From(CapabilityCatalog.All), Array.Empty<string>());
    }
}
