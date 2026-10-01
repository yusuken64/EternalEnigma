using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class LockFictionTests
{
    private static readonly string[] Themes =
    {
        LockSkinCatalog.Highlands, LockSkinCatalog.Marsh, LockSkinCatalog.Coast, LockSkinCatalog.Forest,
        LockSkinCatalog.Desert, LockSkinCatalog.Tundra, LockSkinCatalog.Ruins, LockSkinCatalog.Volcanic,
    };

    // Generation validates every campaign, so build the sweep once for the whole class.
    private static readonly Lazy<Campaign[]> Sweep = new(() => Enumerable.Range(0, 128).Select(CampaignGenerator.Generate).ToArray());

    private static bool IsGate(CampaignRoute r) =>
        r.ShortcutKind == ShortcutKind.Keyed || (r.ShortcutKind == ShortcutKind.None && !r.Requirement.IsOpen);

    private static Campaign With(Campaign c, IEnumerable<CampaignRoute> routes) =>
        new(c.Seed, c.GeneratorVersion, c.StartLocationId, c.FinalLocationId, c.Manifest, c.Regions, c.Locations, routes, c.Sources, c.Companions, c.ReturnObjectives);

    [Fact]
    public void EveryCapabilityHasSkinsForItsNaturalFormAndAsASealedDoorInEveryTheme()
    {
        foreach (var capability in CapabilityCatalog.All)
            foreach (var form in new[] { CampaignGenerator.Form(capability), LockForm.Interaction })
                foreach (var theme in Themes)
                    Assert.True(LockSkinCatalog.For(capability, form, theme).Count >= (form == LockForm.Interaction && CampaignGenerator.Form(capability) != LockForm.Interaction ? 2 : 3),
                        $"{capability}/{form} has too few skins for {theme}.");
    }

    [Fact]
    public void SkinsAreWellFormedAndNeverNameTheirCapability()
    {
        Assert.Equal(LockSkinCatalog.Skins.Count, LockSkinCatalog.Skins.Select(s => s.Id).Distinct().Count());
        Assert.Equal(LockSkinCatalog.Skins.Count, LockSkinCatalog.Skins.Select(s => s.Text).Distinct().Count());
        foreach (var skin in LockSkinCatalog.Skins)
        {
            Assert.Equal(1, skin.Text.Split("{place}").Length - 1);
            Assert.False(skin.Text.StartsWith("{place}"), $"{skin.Id} starts with a place, which may begin with a lowercase article.");
            Assert.EndsWith(".", skin.Text);
            Assert.Null(LockSkinCatalog.LeakedCapability(skin.Text));
            Assert.All(skin.Themes, theme => Assert.Contains(theme, Themes));
        }
    }

    [Fact]
    public void PlaceAndKeyPoolsAreDeepAndDistinct()
    {
        var places = Themes.SelectMany(LockSkinCatalog.Places).ToArray();
        Assert.All(Themes, theme => Assert.True(LockSkinCatalog.Places(theme).Count >= 10, theme));
        Assert.Equal(places.Length, places.Distinct().Count());
        Assert.Equal(Themes.Length, Themes.Select(LockSkinCatalog.WaystoneKeyName).Distinct().Count());
        Assert.Equal(LockSkinCatalog.TownGateKeyNames.Count, LockSkinCatalog.TownGateKeyNames.Distinct().Count());
        Assert.Empty(LockSkinCatalog.TownGateKeyNames.Intersect(LockSkinCatalog.TownAreaKeyNames));
    }

    [Fact]
    public void EveryGeneratedGateIsFictionNeverARawIdentifier()
    {
        foreach (var campaign in Sweep.Value)
        {
            int seed = campaign.Seed;
            foreach (var route in campaign.Routes)
            {
                if (!IsGate(route)) { Assert.Null(route.LockText); continue; }
                Assert.False(string.IsNullOrWhiteSpace(route.LockText), $"seed {seed} {route.Id}");
                Assert.True(LockSkinCatalog.IsKnown(route.SkinId!), $"seed {seed} {route.Id}");
                Assert.DoesNotContain("{", route.LockText);
                Assert.Null(LockSkinCatalog.LeakedCapability(route.LockText!));
                Assert.DoesNotContain("route-", route.LockText);
                Assert.Equal(route.LockText, route.GateHint);
                if (route.ShortcutKind == ShortcutKind.Keyed) Assert.Contains(route.KeyName!, route.LockText);
            }
        }
    }

    [Fact]
    public void KeyIdentityIsStableWhileKeyNamesAreFiction()
    {
        var campaign = CampaignGenerator.Generate(42);
        Assert.Equal("Town gate key", campaign.Routes.Single(r => r.IsTownExit).KeyId);
        Assert.Equal("Town area key", campaign.Routes.Single(r => r.Id == "starter-exit").KeyId);
        foreach (var key in campaign.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed))
        {
            Assert.NotEqual(key.KeyId, key.KeyName);
            Assert.Equal(key.KeyName, key.KeyLabel);
        }
    }

    [Fact]
    public void FictionIsReproduciblePerSeedAndVariesAcrossSeeds()
    {
        string Texts(Campaign c) => string.Join("|", c.Routes.Select(r => r.LockText));
        Assert.Equal(Texts(Sweep.Value[7]), Texts(CampaignGenerator.Generate(7)));
        Assert.Equal(128, Sweep.Value.Select(Texts).Distinct().Count());
        var allTexts = Sweep.Value.SelectMany(c => c.Routes).Where(IsGate).Select(r => r.LockText).Distinct().Count();
        Assert.True(allTexts > 800, $"Only {allTexts} distinct lock texts across 128 seeds.");
    }

    [Fact]
    public void NoTwoLocksInOneCampaignReadTheSame()
    {
        foreach (var campaign in Sweep.Value)
        {
            var texts = campaign.Routes.Where(IsGate).Select(r => r.LockText!).ToArray();
            Assert.True(texts.Length == texts.Distinct().Count(), $"seed {campaign.Seed} repeats: " +
                string.Join("; ", texts.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key)));
        }
    }

    [Fact]
    public void FictionDoesNotChangeTopologyOrProgression()
    {
        // Stage 9 must only dress routes: the logical shape is identical when every fiction field is stripped.
        var campaign = CampaignGenerator.Generate(42);
        var stripped = With(campaign, campaign.Routes.Select(r => new CampaignRoute(r.Id, r.From, r.To, r.Requirement, r.Form, r.Required,
            r.IsProgressionBoundary, r.ShortcutKind, r.UnlockingEndpoint, r.KeyId, r.KeyLocationId, r.IsWarp, r.KeyCondition, r.IsTownExit)));
        Assert.Equal(campaign.Routes.Select(r => (r.Id, r.From, r.To, r.Requirement.ToString())),
            stripped.Routes.Select(r => (r.Id, r.From, r.To, r.Requirement.ToString())));
        Assert.NotEqual(CampaignFingerprint.Compute(campaign), CampaignFingerprint.Compute(stripped));
    }

    [Fact]
    public void ValidatorRejectsLeakedMissingAndDuplicateFiction()
    {
        var campaign = CampaignGenerator.Generate(42);
        var gate = campaign.Routes.First(r => r.ShortcutKind == ShortcutKind.None && !r.Requirement.IsOpen);

        var leaked = gate.WithFiction(gate.SkinId!, "A wall bars the way. Requires: Breach.");
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == gate ? leaked : r))).Errors, e => e.StartsWith("lock.leak:"));

        var template = gate.WithFiction(gate.SkinId!, "A wall bars the way at {place}.");
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == gate ? template : r))).Errors, e => e.StartsWith("lock.template:"));

        var unknown = gate.WithFiction("made.up.1", "A wall bars the way.");
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == gate ? unknown : r))).Errors, e => e.StartsWith("lock.skin:"));

        var bare = new CampaignRoute(gate.Id, gate.From, gate.To, gate.Requirement, gate.Form, gate.Required, gate.IsProgressionBoundary);
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == gate ? bare : r))).Errors, e => e.StartsWith("lock.skin:"));

        var keys = campaign.Routes.Where(r => r.ShortcutKind == ShortcutKind.Keyed).Take(2).ToArray();
        var clone = keys[1].WithFiction(keys[1].SkinId!, keys[1].LockText!, keys[0].KeyName);
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == keys[1] ? clone : r))).Errors, e => e.StartsWith("lock.keyNames:"));

        var open = campaign.Routes.First(r => !r.HasGate);
        var dressed = open.WithFiction("climb.area.1", "Nothing blocks the way.");
        Assert.Contains(CampaignValidator.Validate(With(campaign, campaign.Routes.Select(r => r == open ? dressed : r))).Errors, e => e.StartsWith("lock.fiction:"));
    }

    [Fact]
    public void EveryRegionHasADistinctNameInsteadOfALetter()
    {
        var pools = Themes.SelectMany(LockSkinCatalog.RegionNames).ToArray();
        Assert.All(Themes, theme => Assert.True(LockSkinCatalog.RegionNames(theme).Count >= 3, theme));
        Assert.Equal(pools.Length, pools.Distinct().Count());
        foreach (var campaign in Sweep.Value)
        {
            Assert.All(campaign.Regions, r =>
            {
                Assert.Contains(r.Name, LockSkinCatalog.RegionNames(r.Theme));
                Assert.Equal(r.Name, r.DisplayName);
                Assert.DoesNotContain("Biome", r.DisplayName);
            });
            Assert.Equal(campaign.Regions.Count, campaign.Regions.Select(r => r.Name).Distinct().Count());
        }
    }

    [Fact]
    public void WaystoneTextNamesTheRegionHoldingTheKey()
    {
        foreach (var campaign in Sweep.Value)
            foreach (var warp in campaign.Routes.Where(r => r.IsWarp))
            {
                var destination = campaign.Regions.Single(r => r.Id == campaign.Locations.Single(l => l.Id == warp.To).RegionId);
                Assert.Contains(destination.Name!, warp.LockText);
            }
    }

    [Fact]
    public void UnnamedRegionsFallBackToTheirLetter()
    {
        Assert.Equal("Biome C", new CampaignRegion("region-2", "Marsh", 0, 2).DisplayName);
        Assert.Equal("the Sallowmere", new CampaignRegion("region-2", "Marsh", 0, 2, "the Sallowmere").DisplayName);
        Assert.Equal("C", new CampaignRegion("region-2", "Marsh", 0, 2, "the Sallowmere").Label);
    }

    [Fact]
    public void ValidatorRejectsMissingOrDuplicateRegionNames()
    {
        var campaign = CampaignGenerator.Generate(42);
        CampaignRegion Rename(CampaignRegion r, string? name) => new(r.Id, r.Theme, r.Tier, r.ProgressionOrder, name);
        var duplicate = campaign.Regions.Select((r, i) => i < 2 ? Rename(r, "the Same Land") : r).ToArray();
        var missing = campaign.Regions.Select((r, i) => i == 0 ? Rename(r, null) : r).ToArray();
        foreach (var regions in new[] { duplicate, missing })
            Assert.Contains(CampaignValidator.Validate(new Campaign(campaign.Seed, campaign.GeneratorVersion, campaign.StartLocationId,
                campaign.FinalLocationId, campaign.Manifest, regions, campaign.Locations, campaign.Routes, campaign.Sources, campaign.Companions,
                campaign.ReturnObjectives)).Errors, e => e.StartsWith("region.names:"));
    }

    [Fact]
    public void GateWithoutFictionFallsBackToTheLegacyHint()
    {
        var route = new CampaignRoute("r", "a", "b", new Requirement(CapabilitySet.Of(Capability.Breach)), LockForm.Obstacle);
        Assert.Equal("Requires: " + route.Requirement, route.GateHint);
        Assert.Equal("The way is shut.", route.WithFiction("breach.obstacle.1", "The way is shut.").GateHint);
    }
}
