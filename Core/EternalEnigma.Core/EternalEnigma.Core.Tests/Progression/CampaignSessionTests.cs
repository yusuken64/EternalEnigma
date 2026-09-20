using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class CampaignSessionTests
{
    internal static Campaign Fixture(LockForm firstGate = LockForm.Area, bool secondTown = false)
    {
        return new Campaign(0, 1, "town", "finish",
            new[] { new ActivatedCapability(Capability.HazardWard, CapabilityRole.Critical, 0), new ActivatedCapability(Capability.DeepDive, CapabilityRole.Critical, 0) },
            new[] { new CampaignRegion("region", "Marsh", 0) },
            new[] { new CampaignLocation("town", "region", 0, LocationKind.Town),
                new CampaignLocation("middle", "region", 0, secondTown ? LocationKind.Town : LocationKind.Checkpoint),
                new CampaignLocation("finish", "region", 0, LocationKind.FinalDungeon) },
            new[] { new CampaignRoute("hazard", "town", "middle", new Requirement(CapabilitySet.Of(Capability.HazardWard)), firstGate),
                new CampaignRoute("dive", "middle", "finish", new Requirement(CapabilitySet.Of(Capability.DeepDive)), LockForm.Area) },
            new[] { new CapabilitySource("ward", "town", Capability.HazardWard, "a"), new CapabilitySource("diver", "town", Capability.DeepDive, "b") },
            new[] { new CampaignCompanion("a", Capability.HazardWard), new CampaignCompanion("b", Capability.DeepDive) });
    }

    [Fact]
    public void RecruitmentDoesNotAutomaticallyEquipPersonalCapabilities()
    {
        var session = new CampaignSession(Fixture());
        Assert.True(session.TryClaimSource("ward"));
        Assert.False(session.TryClaimSource("ward"));
        Assert.False(session.TryMove("hazard"));
        Assert.False(session.TrySetParty("not-recruited"));
        Assert.False(session.TrySetParty("a", "a"));
        Assert.True(session.TrySetParty("a"));
        Assert.True(session.TryMove("hazard"));
        Assert.False(session.TrySetParty());
        Assert.Empty(session.ResolvedLocks);
        Assert.False(session.TryFastTravel("middle"));
        session.ReturnAfterDefeat();
        Assert.Equal("town", session.LocationId);
        Assert.Contains("a", session.RecruitedCompanions);
        Assert.True(session.TrySetParty());
        Assert.False(session.TryMove("hazard"));
    }

    [Fact]
    public void ResolvedObstacleRemainsOpenAfterDismissalAndDefeat()
    {
        var session = new CampaignSession(Fixture(LockForm.Obstacle));
        Assert.True(session.TryClaimSource("ward"));
        Assert.True(session.TrySetParty("a"));
        Assert.True(session.TryMove("hazard"));
        Assert.Contains("hazard", session.ResolvedLocks);
        session.ReturnAfterDefeat();
        Assert.True(session.TrySetParty());
        Assert.True(session.TryMove("hazard"));
    }

    [Fact]
    public void RouteWidthCountsTheWholeExcursionNotEachGateSeparately()
    {
        Assert.DoesNotContain("finish", CampaignExplorer.Explore(Fixture(), maxPersonalCapabilities: 1).ReachableLocations);
        Assert.Contains("finish", CampaignExplorer.Explore(Fixture(), maxPersonalCapabilities: 2).ReachableLocations);
        Assert.Contains("finish", CampaignExplorer.Explore(Fixture(secondTown: true), maxPersonalCapabilities: 1).ReachableLocations);
    }

    [Fact]
    public void ExplorerDoesNotTreatNewlyRecruitedCompanionAsActiveInTheField()
    {
        var original = Fixture();
        var campaign = new Campaign(0, 1, "town", "finish", original.Manifest, original.Regions, original.Locations, original.Routes,
            new[] { original.Sources[0], new CapabilitySource("diver", "middle", Capability.DeepDive, "b") }, original.Companions);
        Assert.DoesNotContain("finish", CampaignExplorer.Explore(campaign, maxPersonalCapabilities: 1).ReachableLocations);
        Assert.Contains("finish", CampaignExplorer.Explore(campaign, maxPersonalCapabilities: 2).ReachableLocations);
    }

    [Fact]
    public void ConverterRequiresEngineeringAndVehiclePersistsAcrossDefeat()
    {
        var original = Fixture();
        var campaign = new Campaign(0, 1, "town", "finish", original.Manifest, original.Regions, original.Locations, original.Routes,
            new[] { new CapabilitySource("engineering", "town", Capability.Engineering),
                new CapabilitySource("boat", "town", Capability.Boat, prerequisites: new Requirement(CapabilitySet.Of(Capability.Engineering))) }, original.Companions);
        var session = new CampaignSession(campaign);
        Assert.False(session.TryClaimSource("boat"));
        Assert.True(session.TryClaimSource("engineering"));
        Assert.True(session.TryClaimSource("boat"));
        session.ReturnAfterDefeat();
        Assert.True(session.TrySetParty());
        Assert.True(session.HeldCapabilities.ContainsAll(CapabilitySet.Of(Capability.Engineering, Capability.Boat)));
    }
}
