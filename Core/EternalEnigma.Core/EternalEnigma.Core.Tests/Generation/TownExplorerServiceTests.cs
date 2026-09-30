using EternalEnigma.ConsoleExplorer;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class TownExplorerServiceTests
{
    private static TownVisit Visit(int seed = 42) =>
        TownVisit.Create(seed, CampaignGenerator.Generate(seed).Locations.First(l => l.Id == "town-0"));

    [Fact]
    public void EveryServiceDoorIsLabelledExactlyOnce()
    {
        var visit = Visit();
        var map = string.Concat(TownRenderer.Render(visit, visit.Plan.Width, visit.Plan.Height));
        foreach (var service in TownServiceCatalog.All)
            Assert.Equal(1, map.Count(c => c == TownServiceGlyphs.Glyph(service)));
        Assert.Equal(CampaignContext.AuthoredTownBuildings, map.Count(c => c == TownServiceGlyphs.OtherBuilding));
    }

    [Fact]
    public void ServiceGlyphsAreDistinctAndDoNotClashWithMapGlyphs()
    {
        var glyphs = TownServiceCatalog.All.Select(TownServiceGlyphs.Glyph).ToArray();
        Assert.Equal(glyphs.Length, glyphs.Distinct().Count());
        Assert.DoesNotContain(glyphs, g => "@XDvaHT_+:\".*?".Contains(g));
    }

    [Fact]
    public void LegendNamesEveryServiceAndReplacesSlotNumbers()
    {
        var legend = string.Join("\n", TownRenderer.LegendFor(Visit()));
        foreach (var service in TownServiceCatalog.All)
            Assert.Contains($"{TownServiceGlyphs.Glyph(service)} {TownServiceGlyphs.Name(service)}", legend);
        Assert.DoesNotContain("0-9 buildings", legend);
    }

    [Fact]
    public void LegacyTownsKeepNumberedDoors()
    {
        var visit = new TownVisit("town-x", TownPlanGenerator.Generate(new TownPlanOptions(42)));
        Assert.Null(visit.SlotServices);
        Assert.Single(TownRenderer.LegendFor(visit));
        Assert.Contains("0-9 buildings", TownRenderer.LegendFor(visit)[0]);
    }

    [Fact]
    public void InteriorViewMarksTheVendorAtTheBackOfEveryRoom()
    {
        var campaign = CampaignGenerator.Generate(42);
        var session = new ExplorerSession(campaign, OverworldGridGenerator.Generate(campaign));
        session.JumpTo("town-0");
        Assert.True(session.EnterLocation());
        var plan = session.Town!.Plan;
        var map = string.Concat(new MapRenderer(session).Render(plan.Width, plan.Height));
        Assert.Equal(TownServiceCatalog.All.Count, plan.ShopRooms.Count);
        Assert.Equal(plan.ShopRooms.Count, map.Count(c => c == 'v'));
        Assert.All(TownServiceCatalog.All, s => Assert.Equal(1, map.Count(c => c == TownServiceGlyphs.Glyph(s))));
    }

    [Fact]
    public void EnteringATownInTheSessionCarriesItsServices()
    {
        var campaign = CampaignGenerator.Generate(42);
        var session = new ExplorerSession(campaign, OverworldGridGenerator.Generate(campaign));
        session.JumpTo("town-0");
        Assert.True(session.EnterLocation());
        Assert.NotNull(session.Town!.SlotServices);
        Assert.Equal(TownServiceCatalog.All.Count + CampaignContext.AuthoredTownBuildings, session.Town.SlotServices!.Count);
    }
}
