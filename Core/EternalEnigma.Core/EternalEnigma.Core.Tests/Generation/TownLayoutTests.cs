using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownLayoutTests
{
    [Fact]
    public void EveryTownOffersAllServices()
    {
        foreach (int seed in new[] { 1, 42, 99 })
        {
            var campaign = CampaignGenerator.Generate(seed);
            foreach (var town in campaign.Locations.Where(l => l.Kind == LocationKind.Town))
                Assert.Equal(TownServiceCatalog.All.Select(s => s.Id), town.Services.Select(s => s.Id));
        }
        Assert.Equal(5, TownServiceCatalog.All.Count);
        Assert.Equal(1, TownServiceCatalog.All.Count(s => s.Kind == TownServiceKind.Trainer));
    }

    [Fact]
    public void OnlyTownsHaveServices()
    {
        var campaign = CampaignGenerator.Generate(42);
        Assert.All(campaign.Locations.Where(l => l.Kind != LocationKind.Town), l => Assert.Empty(l.Services));
    }

    [Fact]
    public void ValidatorRejectsATownMissingATrainer()
    {
        var campaign = CampaignGenerator.Generate(42);
        var locations = campaign.Locations.Select(l => l.Id == "town-0"
            ? new CampaignLocation(l.Id, l.RegionId, l.Tier, l.Kind, l.Required, l.Stage, l.ParentTownId, l.Services.Where(s => s.Kind != TownServiceKind.Trainer))
            : l).ToArray();
        var broken = new Campaign(campaign.Seed, campaign.GeneratorVersion, campaign.StartLocationId, campaign.FinalLocationId,
            campaign.Manifest, campaign.Regions, locations, campaign.Routes, campaign.Sources, campaign.Companions, campaign.ReturnObjectives);
        Assert.Contains(CampaignValidator.Validate(broken).Errors, e => e.StartsWith("towns.services:") && e.Contains("Trainer"));
    }

    [Fact]
    public void ServicesChangeTheFingerprint()
    {
        var campaign = CampaignGenerator.Generate(42);
        var locations = campaign.Locations.Select(l => l.Id == "town-0"
            ? new CampaignLocation(l.Id, l.RegionId, l.Tier, l.Kind, l.Required, l.Stage, l.ParentTownId, l.Services.Skip(1))
            : l).ToArray();
        var changed = new Campaign(campaign.Seed, campaign.GeneratorVersion, campaign.StartLocationId, campaign.FinalLocationId,
            campaign.Manifest, campaign.Regions, locations, campaign.Routes, campaign.Sources, campaign.Companions, campaign.ReturnObjectives);
        Assert.NotEqual(CampaignFingerprint.Compute(campaign), CampaignFingerprint.Compute(changed));
    }

    [Fact]
    public void TownsGrowWithTheirBuildingCount()
    {
        Assert.True(TownPlanOptions.SizeFor(4) >= 15);
        Assert.True(TownPlanOptions.SizeFor(15) >= 30);
        Assert.True(TownPlanOptions.SizeFor(TownPlanOptions.MaxBuildings) <= 64);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(-7)]
    [InlineData(int.MaxValue)]
    public void LayoutPlacesEveryServiceWithAnInteriorWhereRequired(int seed)
    {
        var layout = TownLayout.Create(seed, TownServiceCatalog.All, CampaignContext.AuthoredTownBuildings);
        var plan = TownPlanGenerator.Generate(layout.Options);

        Assert.True(TownPlanValidator.Validate(plan, layout.Options).IsValid);
        Assert.Equal(TownServiceCatalog.All.Count + CampaignContext.AuthoredTownBuildings, plan.BuildingSlots.Count);
        Assert.Equal(layout.Options.Width, plan.Width);
        Assert.Equal(TownServiceCatalog.All.Count, layout.SlotServices.Count(s => s != null));
        Assert.Equal(CampaignContext.AuthoredTownBuildings, layout.SlotServices.Count(s => s == null));
        Assert.Equal(layout.SlotServices.Count(s => s != null && s.HasInterior), plan.ShopRooms.Count);
        for (int i = 0; i < plan.BuildingSlots.Count; i++)
        {
            bool room = plan.ShopRoomAt(plan.BuildingSlots[i]) != null;
            Assert.Equal(layout.SlotServices[i]?.HasInterior ?? false, room);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-7)]
    public void EveryServiceIsUsedInsideItsBuilding(int seed)
    {
        var layout = TownLayout.Create(seed, TownServiceCatalog.All, CampaignContext.AuthoredTownBuildings);
        var plan = TownPlanGenerator.Generate(layout.Options);

        Assert.Equal(TownServiceCatalog.All.Count, plan.ShopRooms.Count);
        for (int i = 0; i < plan.BuildingSlots.Count; i++)
        {
            bool hasRoom = plan.TryGetVendorAnchor(plan.BuildingSlots[i], out _);
            Assert.Equal(layout.SlotServices[i] != null, hasRoom); // services have a room; the entrance does not
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(42)]
    public void ServiceTownsCentreTheEntranceCorridor(int seed)
    {
        var layout = TownLayout.Create(seed, TownServiceCatalog.All, CampaignContext.AuthoredTownBuildings);
        var plan = TownPlanGenerator.Generate(layout.Options);

        Assert.Equal(plan.Width / 2, plan.SpineX);
        Assert.Equal(new GridPoint(plan.SpineX, 2), plan.PartySpawn);
        Assert.Equal(new GridPoint(plan.SpineX, 0), plan.Exit);
        Assert.Equal(plan.SpineX, plan.DungeonEntrance.X);
        for (int y = 0; y < plan.Height; y++) Assert.True(plan.Layers[TownLayers.Roads][plan.SpineX, y]);
        for (int x = 0; x < plan.Width; x++)
            for (int y = 0; y < plan.Height; y++)
                if (plan.IsReserved(new GridPoint(x, y)))
                {
                    Assert.True(plan.IsWalkable(new GridPoint(x, y)));
                    Assert.False(plan.Layers[TownLayers.Buildings][x, y]);
                }
    }

    [Fact]
    public void LegacyTownsKeepTheOriginalSpine()
    {
        var plan = TownPlanGenerator.Generate(new TownPlanOptions(42));
        Assert.Equal(TownPlan.DefaultSpineX, plan.SpineX);
        Assert.Equal(new GridPoint(10, 2), plan.PartySpawn);
    }

    [Fact]
    public void LayoutIsDeterministicAndVariesBySeed()
    {
        var a = TownLayout.Create(5, TownServiceCatalog.All, 2).SlotServices.Select(s => s?.Id).ToArray();
        var b = TownLayout.Create(5, TownServiceCatalog.All, 2).SlotServices.Select(s => s?.Id).ToArray();
        var c = TownLayout.Create(6, TownServiceCatalog.All, 2).SlotServices.Select(s => s?.Id).ToArray();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void CampaignTownsGenerateAtTheirServiceSize()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        foreach (var town in context.Campaign.Locations.Where(l => l.Kind == LocationKind.Town))
        {
            var layout = context.TownLayout(town.Id);
            var plan = context.Town(town.Id);
            Assert.Equal(layout.Options.Width, plan.Width);
            Assert.Equal(TownServiceCatalog.All.Count + CampaignContext.AuthoredTownBuildings + CampaignContext.ResidentialTownBuildings, plan.BuildingSlots.Count);
        }
    }

    [Fact]
    public void SeedSweepOfServiceTownsIsValid()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var layout = TownLayout.Create(seed, TownServiceCatalog.All, CampaignContext.AuthoredTownBuildings);
            var plan = TownPlanGenerator.Generate(layout.Options);
            var result = TownPlanValidator.Validate(plan, layout.Options);
            Assert.True(result.IsValid, $"Seed {seed}: {string.Join("; ", result.Errors)}");
        }
    }
}
