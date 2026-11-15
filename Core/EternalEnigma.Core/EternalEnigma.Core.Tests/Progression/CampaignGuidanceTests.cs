using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;
public sealed class CampaignGuidanceTests
{
    [Theory]
    [InlineData(1,0,"east")][InlineData(1,1,"northeast")]
    [InlineData(0,1,"north")][InlineData(-1,1,"northwest")]
    [InlineData(-1,0,"west")][InlineData(-1,-1,"southwest")]
    [InlineData(0,-1,"south")][InlineData(1,-1,"southeast")]
    [InlineData(10,2,"east")][InlineData(10,5,"northeast")]
    public void BearingsUsePositiveYNorthAndRoundToNearestSector(int x,int y,string expected)
        => Assert.Equal(expected,CampaignGuidance.Bearing(new(10,10),new(10+x,10+y)));

    [Fact]
    public void CurrentTownExitHasPriorityAndClearedLocksDisappear()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign,42));
        string town = "town-0";
        var exit = c.Campaign.Routes.Single(r=>r.IsTownExit && r.From==town);
        var hint = CampaignGuidance.TownHint(c,town);
        Assert.StartsWith("Clear ",hint);
        Assert.DoesNotContain(exit.LockText!,hint);
        Assert.Contains(c.Campaign.KeyLabel(exit.KeyId!),hint);
        Assert.Contains("here in town",hint);
        c.Resolved.UnionWith(c.Campaign.Routes.Select(r=>r.Id));
        Assert.Equal("",CampaignGuidance.TownHint(c,town));
    }

    [Fact]
    public void OpeningAdviceAdvancesOneErrandAtATime()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        c.State.RequiresOpeningPreparation = true;
        Assert.Equal("Collect your free bread at the bakery.", CampaignGuidance.TownHint(c, "town-0"));
        c.State.BoughtOpeningBread = true;
        Assert.Equal("Learn your first ability at the trainer.", CampaignGuidance.TownHint(c, "town-0"));
        c.State.LearnedOpeningSkill = true;
        Assert.Equal("Sleep and save in your bed at home.", CampaignGuidance.TownHint(c, "town-0"));
        Assert.False(c.IsGridGenerated);
    }

    [Fact]
    public void OwnedKeyPointsToItsGateInsteadOfRepeatingItsSource()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var route = c.Campaign.Routes.Single(r => r.Id == "starter-exit");
        c.Keys.Add(route.KeyId!);
        var hint = CampaignGuidance.DescribeLock(c, "town-0", route);
        Assert.StartsWith("Use " + c.Campaign.KeyLabel(route.KeyId!) + " at the gate toward ", hint);
        Assert.Contains(CampaignGuidance.LocationName(c, route.To), hint);
        Assert.Contains("from here", hint);
        Assert.DoesNotContain("dungeon", hint);
        Assert.DoesNotContain(route.LockText!, hint);
    }

    [Fact]
    public void OwnedAlternativeIsPreferredAndBenchedCompanionIsNamed()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var companions = c.Campaign.Companions.GroupBy(a => a.Capability).Select(g => g.First()).Take(3).ToArray();
        var route = new CampaignRoute("test", "town-0", "checkpoint-1",
            new Requirement(CapabilitySet.Of(companions[0].Capability, companions[1].Capability), CapabilitySet.Of(companions[2].Capability)), LockForm.Obstacle);
        c.Roster.Add(companions[2].Id);
        var hint = CampaignGuidance.DescribeLock(c, "town-0", route, _ => "Casey");
        Assert.Equal("Add Casey to your party here in town to use " + CampaignRewardNames.Name(c, companions[2].Capability) + ".", hint);
        c.Active.Add(companions[2].Id);
        hint = CampaignGuidance.DescribeLock(c, "town-0", route, _ => "Casey");
        Assert.StartsWith("Use " + CampaignRewardNames.Name(c, companions[2].Capability) + " at the gate", hint);
        Assert.DoesNotContain("alternatively", hint);
        Assert.DoesNotContain(CampaignRewardNames.Name(c, companions[0].Capability), hint);
    }

    [Fact]
    public void PairedRequirementGivesOnlyTheRemainingRecruitmentStep()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var companions = c.Campaign.Companions.GroupBy(a => a.Capability).Select(g => g.First()).Take(2).ToArray();
        var route = new CampaignRoute("test", "town-0", "checkpoint-1",
            new Requirement(CapabilitySet.Of(companions[0].Capability, companions[1].Capability)), LockForm.Obstacle);
        c.Roster.Add(companions[0].Id);
        c.Active.Add(companions[0].Id);
        var source = c.Campaign.Sources.First(s => s.CompanionId == companions[1].Id);
        var hint = CampaignGuidance.DescribeLock(c, "town-0", route, _ => "Riley");
        Assert.StartsWith("Recruit Riley at ", hint);
        Assert.Contains(CampaignGuidance.LocationName(c, source.LocationId), hint);
        Assert.Contains("from here", hint);
        Assert.DoesNotContain(CampaignRewardNames.Name(c, companions[0].Capability), hint);
    }

    [Fact]
    public void ReadyPairIsOneGateActionUsingBothCapabilities()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var companions = c.Campaign.Companions.GroupBy(a => a.Capability).Select(g => g.First()).Take(2).ToArray();
        var route = new CampaignRoute("test", "town-0", "checkpoint-1",
            new Requirement(CapabilitySet.Of(companions[0].Capability, companions[1].Capability)), LockForm.Obstacle);
        c.Roster.UnionWith(companions.Select(a => a.Id));
        c.Active.UnionWith(companions.Select(a => a.Id));
        var hint = CampaignGuidance.DescribeLock(c, "town-0", route);
        Assert.StartsWith("Use ", hint);
        foreach (var companion in companions) Assert.Contains(CampaignRewardNames.Name(c, companion.Capability), hint);
        Assert.Contains(" and ", hint);
        Assert.Contains(" at the gate toward ", hint);
        Assert.DoesNotContain("alternatively", hint);
    }

    [Fact]
    public void ConverterAdviceFirstNamesTheMissingPrerequisite()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var route = new CampaignRoute("test", "town-0", "checkpoint-1", new Requirement(CapabilitySet.Of(Capability.Boat)), LockForm.Obstacle);
        var engineering = c.Campaign.Sources.First(s => s.Capability == Capability.Engineering);
        var hint = CampaignGuidance.DescribeLock(c, "town-0", route);
        Assert.StartsWith("Collect " + CampaignRewardNames.Name(c, Capability.Engineering) + " at ", hint);
        Assert.Contains(CampaignGuidance.LocationName(c, engineering.LocationId), hint);
        Assert.DoesNotContain(CampaignRewardNames.Name(c, Capability.Boat), hint);
        c.Position = c.Grid.Locations[engineering.LocationId];
        Assert.True(c.Claim(engineering.Id));
        var boat = c.Campaign.Sources.First(s => s.Capability == Capability.Boat);
        hint = CampaignGuidance.DescribeLock(c, "town-0", route);
        Assert.StartsWith("Collect " + CampaignRewardNames.Name(c, Capability.Boat) + " at ", hint);
        Assert.Contains(CampaignGuidance.LocationName(c, boat.LocationId), hint);
    }

    [Fact]
    public void BridgeAdviceHandlesEntryRequirementsBeforeNamingItsCompletionAction()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var route = c.Campaign.Routes.First(r => r.CompletionLocationId != null);
        var hint = CampaignGuidance.DescribeLock(c, "town-3", route, _ => "Rowan");
        Assert.StartsWith("Recruit Rowan at ", hint);
        var companion = c.Campaign.Companions.Single(a => a.Capability == Capability.Icewalk);
        c.Roster.Add(companion.Id);
        c.Active.Add(companion.Id);
        hint = CampaignGuidance.DescribeLock(c, "town-3", route);
        Assert.StartsWith("Reach the far end of ", hint);
        Assert.Contains(CampaignGuidance.LocationName(c, route.CompletionLocationId!), hint);
        Assert.Contains("from here", hint);
        Assert.DoesNotContain(route.LockText!, hint);
    }

    [Theory]
    [InlineData(0)] [InlineData(42)] [InlineData(12345)]
    public void GeneratedGateHintsHaveOneInstructionAndNoAlternativeLists(int seed)
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign, seed));
        foreach (var route in c.Campaign.Routes.Where(r => r.HasGate && (r.Required || r.IsTownExit)))
        {
            var hint = CampaignGuidance.DescribeLock(c, "town-0", route, _ => "Casey");
            Assert.NotEmpty(hint);
            Assert.EndsWith(".", hint);
            Assert.DoesNotContain(". ", hint);
            Assert.DoesNotContain(";", hint);
            Assert.DoesNotContain("alternatively", hint);
            Assert.DoesNotContain("The way leads", hint);
        }
    }

    [Fact]
    public void LocationUsesParentTownGeographyAndGeneratedBiome()
    {
        var c=new CampaignContext(new(OverworldLaunchMode.Campaign,42));
        var dungeon=c.Campaign.Locations.First(l=>l.ParentTownId!=null && l.ParentTownId!="town-0");
        string text=CampaignGuidance.Location(c,"town-0",dungeon.Id);
        Assert.Contains(c.GetTownDisplayName(dungeon.ParentTownId!),text);
        Assert.Contains(c.Grid.RegionBiomes[dungeon.RegionId].ToString(),text);
        Assert.Contains(CampaignGuidance.Bearing(c.Grid.Locations["town-0"],c.Grid.Locations[dungeon.ParentTownId!])+" from here",text);
        Assert.Contains("here in town",CampaignGuidance.Location(c,dungeon.ParentTownId!,dungeon.Id));
    }
}
