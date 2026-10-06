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
        Assert.Contains(exit.LockText!,hint);
        Assert.Contains(c.Campaign.KeyLabel(exit.KeyId!),hint);
        Assert.Contains("here in town",hint);
        c.Resolved.UnionWith(c.Campaign.Routes.Select(r=>r.Id));
        Assert.Equal("",CampaignGuidance.TownHint(c,town));
    }

    [Fact]
    public void AlternativesPairsSourcesAndAcquiredGuidanceAreExplicit()
    {
        var c = new CampaignContext(new(OverworldLaunchMode.Campaign,42));
        var capabilities=c.Campaign.Companions.Select(a=>a.Capability).Distinct().Take(3).ToArray();
        var route=new CampaignRoute("test","town-0","checkpoint-1",new Requirement(CapabilitySet.Of(capabilities[0],capabilities[1]),CapabilitySet.Of(capabilities[2])),LockForm.Obstacle,true,lockText:"An ancient barrier.");
        c.Resolved.UnionWith(c.Campaign.Routes.Where(r=>r!=route).Select(r=>r.Id));
        var hint=CampaignGuidance.DescribeLock(c,"town-0",route);
        Assert.Contains("alternatively",hint);Assert.Contains("both",hint);
        foreach(var capability in route.Requirement.Alternatives.SelectMany(a=>a.Values).Distinct())
        {
            Assert.Contains(capability.DisplayName(),hint);
            foreach(var source in c.Campaign.Sources.Where(s=>s.Capability==capability))
                Assert.Contains(CampaignGuidance.Location(c,"town-0",source.LocationId),hint);
        }
        c.Roster.UnionWith(c.Campaign.Companions.Select(a=>a.Id));
        hint=CampaignGuidance.DescribeLock(c,"town-0",route);
        Assert.Contains("travelling party",hint);
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
