using System.Globalization;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class CampaignGeneratorTests
{
    [Fact]
    public void EveryTownHasAnExplicitInteriorAndInvalidParentsOrExtraEntrancesAreRejected()
    {
        var campaign = CampaignGenerator.Generate(42);
        foreach (var town in campaign.Locations.Where(l => l.Kind == LocationKind.Town))
        {
            var dungeon = Assert.Single(campaign.Locations, l => l.ParentTownId == town.Id);
            Assert.Equal(town.Id, Assert.Single(campaign.Routes, r => r.Other(dungeon.Id) != null).Other(dungeon.Id));
        }
        var original = campaign.Locations.Single(l => l.Id == "story-0");
        var invalid = new CampaignLocation(original.Id, original.RegionId, original.Tier, original.Kind, original.Required, original.Stage, "missing-town");
        Assert.Contains(CampaignValidator.Validate(With(campaign, locations: campaign.Locations.Select(l => l.Id == original.Id ? invalid : l))).Errors,
            error => error.StartsWith("interior.parent:"));
        Assert.Contains(CampaignValidator.Validate(With(campaign, routes: campaign.Routes.Append(
            new CampaignRoute("extra-interior-entry", "checkpoint-0", original.Id, Requirement.Open, LockForm.None)))).Errors,
            error => error.StartsWith("interior.entrance:"));
    }

    [Fact]
    public void VersionSevenSeed42HasStableGoldenFingerprint()
    {
        Assert.Equal("e509265e3acab6e0684c79da680ca483667fad439f7406e72a3e4fff153da77b",
            CampaignFingerprint.Compute(CampaignGenerator.Generate(42)));
    }

    [Fact]
    public void InteriorVictoryCannotAlsoUnlockTheOuterAreaGate()
    {
        var campaign = CampaignGenerator.Generate(42);
        var townExit = campaign.Routes.Single(r => r.IsTownExit);
        var areaExit = campaign.Routes.Single(r => r.Id == "starter-exit");
        var sharedKey = new CampaignRoute(areaExit.Id, areaExit.From, areaExit.To, areaExit.Requirement, areaExit.Form,
            required: true, shortcutKind: ShortcutKind.Keyed, keyId: townExit.KeyId, keyLocationId: areaExit.KeyLocationId,
            keyCondition: KeyAcquisition.DungeonCompletion);
        Assert.Contains(CampaignValidator.Validate(With(campaign, routes: campaign.Routes.Select(r => r == areaExit ? sharedKey : r))).Errors,
            error => error.StartsWith("starter.keys:"));
    }

    [Fact]
    public void ValidatorRejectsABiomeWithoutATown()
    {
        var campaign = CampaignGenerator.Generate(42);
        var removed = new HashSet<string> { "town-5", "repeatable-5" };
        var withoutTown = With(campaign, locations: campaign.Locations.Where(l => !removed.Contains(l.Id)),
            routes: campaign.Routes.Where(r => !removed.Contains(r.From) && !removed.Contains(r.To)));
        Assert.Contains(CampaignValidator.Validate(withoutTown).Errors, error => error.StartsWith("towns.biome:"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void RuntimeSessionCanVisitAllRequiredContentUsingOnlyGuaranteedCriticalRewards(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var session = new CampaignSession(campaign);
        var critical = campaign.Manifest.Where(c => c.Role == CapabilityRole.Critical).Select(c => c.Id).ToHashSet();
        var claimed = new HashSet<string>();
        var visited = new HashSet<string>();
        bool changed;
        do
        {
            changed = false;
            var parties = new[] { Array.Empty<string>() }.Concat(session.RecruitedCompanions.Select(id => new[] { id })).ToArray();
            foreach (var town in session.VisitedTowns.ToArray())
            foreach (var party in parties)
            {
                Assert.True(session.TryFastTravel(town));
                Assert.True(session.TrySetParty(party));
                var excursion = new HashSet<string>();
                void Walk()
                {
                    string current = session.LocationId;
                    excursion.Add(current);
                    session.CompleteLocation();
                    foreach (var gate in campaign.Routes) if (session.TryCollectShortcutKey(gate.Id)) changed = true;
                    if (visited.Add(current)) changed = true;
                    foreach (var source in campaign.Sources.Where(s => s.LocationId == current && s.Guaranteed && critical.Contains(s.Capability)))
                        if (!claimed.Contains(source.Id) && session.TryClaimSource(source.Id))
                        { claimed.Add(source.Id); changed = true; }
                    foreach (var route in campaign.Routes.Where(r => r.Other(current) != null))
                    {
                        if (excursion.Contains(route.Other(current)!) || !session.TryMove(route.Id)) continue;
                        Walk();
                        Assert.True(session.TryMove(route.Id));
                        Assert.Equal(current, session.LocationId);
                    }
                }
                Walk();
            }
        } while (changed);
        Assert.All(campaign.Locations.Where(l => l.Required), l => Assert.Contains(l.Id, visited));
        Assert.Contains(campaign.FinalLocationId, visited);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void SeedIsReproducibleAndAllRequiredContentIsReachable(int seed)
    {
        var campaign = CampaignGenerator.Generate(seed);
        Assert.Equal(CampaignFingerprint.Compute(campaign), CampaignFingerprint.Compute(CampaignGenerator.Generate(seed)));
        var validation = CampaignValidator.Validate(campaign);
        Assert.True(validation.IsValid, string.Join("\n", validation.Errors));
        Assert.Contains(campaign.FinalLocationId, validation.GuaranteedCriticalPath!.ReachableLocations);
        Assert.All(campaign.Regions, r => Assert.Contains(campaign.Locations, l => l.Kind == LocationKind.Town && l.RegionId == r.Id));
        Assert.InRange(campaign.Locations.Count(l => l.Kind == LocationKind.Town), 6, 7);
        Assert.Equal(campaign.Locations.Count(l => l.Kind == LocationKind.Town), campaign.Locations.Count(l => l.Kind == LocationKind.RepeatableDungeon));
        Assert.Equal(4, campaign.Locations.Count(l => l.Kind == LocationKind.StoryDungeon));
    }

    [Fact]
    public void SeedSweepVariesActivationAndProgressionRatherThanOnlyNames()
    {
        var manifests = new HashSet<string>();
        var spines = new HashSet<string>();
        for (int seed = 0; seed < 128; seed++)
        {
            var campaign = CampaignGenerator.Generate(seed);
            manifests.Add(string.Join(",", campaign.Manifest.Select(c => c.Id)));
            spines.Add(string.Join(",", campaign.Routes.Where(r => r.Required && !r.Requirement.IsOpen).Select(r => r.Requirement.ToString())));
        }
        Assert.True(manifests.Count > 100);
        Assert.True(spines.Count > 100);
    }

    [Fact]
    public void FingerprintDoesNotDependOnInputEnumerationOrderOrCulture()
    {
        var campaign = CampaignGenerator.Generate(42);
        var reordered = new Campaign(campaign.Seed, campaign.GeneratorVersion, campaign.StartLocationId, campaign.FinalLocationId,
            campaign.Manifest.Reverse(), campaign.Regions.Reverse(), campaign.Locations.Reverse(), campaign.Routes.Reverse(), campaign.Sources.Reverse(), campaign.Companions.Reverse(), campaign.ReturnObjectives.Reverse());
        string expected = CampaignFingerprint.Compute(campaign);
        Assert.Equal(expected, CampaignFingerprint.Compute(reordered));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, CampaignFingerprint.Compute(CampaignGenerator.Generate(42)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    internal static Campaign With(Campaign c, IEnumerable<CapabilitySource>? sources = null, IEnumerable<CampaignRoute>? routes = null,
        IEnumerable<CampaignLocation>? locations = null) => new(c.Seed, c.GeneratorVersion, c.StartLocationId, c.FinalLocationId,
            c.Manifest, c.Regions, locations ?? c.Locations, routes ?? c.Routes, sources ?? c.Sources, c.Companions, c.ReturnObjectives);

    [Fact]
    public void ValidatorRejectsARequiredCapabilityLockedBehindItself()
    {
        var campaign = CampaignGenerator.Generate(42);
        var sources = campaign.Sources.Select(s => s.Capability == Capability.Engineering
            ? new CapabilitySource(s.Id, s.LocationId, s.Capability, prerequisites: new Requirement(CapabilitySet.Of(Capability.Engineering))) : s);
        var validation = CampaignValidator.Validate(With(campaign, sources));
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.StartsWith("acquisition:", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRejectsProgressionRewardsInRepeatableDungeons()
    {
        var campaign = CampaignGenerator.Generate(42);
        var source = campaign.Sources[0];
        string repeatable = campaign.Locations.First(l => l.Kind == LocationKind.RepeatableDungeon && l.Tier == campaign.Manifest.First(c => c.Id == source.Capability).Tier).Id;
        var sources = campaign.Sources.Select(s => s.Id == source.Id ? new CapabilitySource(s.Id, repeatable, s.Capability, s.CompanionId, prerequisites: s.Prerequisites) : s);
        Assert.Contains(CampaignValidator.Validate(With(campaign, sources)).Errors, e => e.StartsWith("repeatable.source:", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorReportsInvalidReferencesInsteadOfCrashing()
    {
        var campaign = CampaignGenerator.Generate(42);
        var broken = campaign.Routes.Concat(new[] { new CampaignRoute("broken", campaign.StartLocationId, "missing", Requirement.Open, LockForm.None) });
        Assert.Contains(CampaignValidator.Validate(With(campaign, routes: broken)).Errors, e => e.StartsWith("route.endpoint:", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingFinalConnectionIsRejected()
    {
        var campaign = CampaignGenerator.Generate(42);
        Assert.Contains(CampaignValidator.Validate(With(campaign, routes: campaign.Routes.Where(r => r.To != campaign.FinalLocationId))).Errors,
            e => e.StartsWith("reachability.required:", StringComparison.Ordinal));
    }

    [Fact]
    public void UngatedShortcutIsRejectedEvenThoughItMakesTheWorldCompletable()
    {
        var campaign = CampaignGenerator.Generate(42);
        var shortcut = new CampaignRoute("bypass", campaign.StartLocationId, campaign.FinalLocationId, Requirement.Open, LockForm.None);
        var broken = With(campaign, routes: campaign.Routes.Concat(new[] { shortcut }));
        Assert.Contains(broken.FinalLocationId, CampaignExplorer.Explore(broken).ReachableLocations);
        Assert.Contains(CampaignValidator.Validate(broken).Errors, e => e.StartsWith("boundary.bypass:", StringComparison.Ordinal));
    }
}
