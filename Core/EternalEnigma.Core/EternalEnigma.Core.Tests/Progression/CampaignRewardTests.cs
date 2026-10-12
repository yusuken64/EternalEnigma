using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

public sealed class CampaignRewardTests
{
    [Theory]
    [InlineData(Capability.MillwrightTraining)]
    [InlineData(Capability.ForestersWrit)]
    [InlineData(Capability.FerrymansFavor)]
    [InlineData(Capability.GravediggersCharge)]
    [InlineData(Capability.SmugglersMark)]
    [InlineData(Capability.BellkeepersOffice)]
    [InlineData(Capability.FuneraryRites)]
    [InlineData(Capability.RoyalSurveyorsMaps)]
    public void NewKeysAreDistinctPermanentAbilitiesThatOnlySolveTheirOwnGate(Capability capability)
    {
        Assert.Equal(CapabilityKind.Utility, capability.Kind());
        var held = CapabilitySet.Of(capability);
        Assert.Equal(1, held.Count);
        Assert.Equal(capability, Assert.Single(held.Values));
        var route = new CampaignRoute("gate", "a", "b", new Requirement(held), LockForm.Interaction);
        var evaluator = new LockSolutionEvaluator(route);
        foreach (var other in CapabilityCatalog.All.Where(c => c != capability))
            Assert.Equal(LockOutcome.NoEffect, evaluator.Apply(new CapabilityLockAction(other)));
        Assert.Equal(LockOutcome.Opened, evaluator.Apply(new CapabilityLockAction(capability)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    public void NamesBelongToTheIssuingTownAndStayIdenticalAfterReload(int seed)
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, seed));
        var names = context.Campaign.Manifest.ToDictionary(e => e.Id, e => CampaignRewardNames.Name(context, e.Id));
        Assert.Equal(names.Count, names.Values.Distinct().Count());
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        foreach (var source in context.Campaign.Sources)
        {
            var site = context.Campaign.Locations.Single(l => l.Id == source.LocationId);
            Assert.Contains(context.Campaign.Locations.Where(l => l.Kind == LocationKind.Town && l.RegionId == site.RegionId),
                town => names[source.Capability].StartsWith(context.GetTownDisplayName(town.Id) + " "));
            Assert.Equal(names[source.Capability], CampaignRewardNames.Name(restored, source.Capability));
            Assert.NotEqual(source.Capability.DisplayName(), names[source.Capability]);
        }
        // Names use the campaign's stored settlement identity, not another generated placeholder.
        foreach (var town in restored.State.TownNames) town.Name = "Renamed " + town.Name;
        foreach (var entry in names)
            Assert.Equal("Renamed " + entry.Value, CampaignRewardNames.Name(restored, entry.Key));
    }

    [Fact]
    public void EngineeringDescriptorVariesWithCampaignFiction()
    {
        var names = Enumerable.Range(0, 32).Select(seed => new CampaignContext(new(OverworldLaunchMode.Campaign, seed)))
            .Select(context => CampaignRewardNames.Name(context, Capability.Engineering)).ToArray();
        Assert.Contains(names, name => name.EndsWith("Bridge Engineering"));
        Assert.Contains(names, name => name.EndsWith("Hydraulic Engineering"));
        Assert.Contains(names, name => name.EndsWith("Mechanical Engineering"));
    }

    [Fact]
    public void ANewPermanentKeyCanOnlyBeClaimedOnceIncludingAfterReload()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var source = context.Campaign.Sources.First(s => s.Capability >= Capability.MillwrightTraining);
        // Select the site directly; travel and encounter completion have their own integration tests.
        context.State.PendingDungeon = source.LocationId;
        context.Completed.Add(source.LocationId);
        Assert.True(context.Claim(source.Id));
        Assert.False(context.Claim(source.Id));
        Assert.True(context.Held.Contains(source.Capability));
        Assert.Single(CampaignGuidance.Acquired(context), c => c == source.Capability);
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), context.Capture());
        Assert.False(restored.Claim(source.Id));
        Assert.True(restored.Held.Contains(source.Capability));
    }
}
