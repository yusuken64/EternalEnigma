using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Progression;

/// <summary>Counterexamples for a future retirement policy. These do not enable capability deletion.</summary>
public sealed class CapabilityRetirementAuditTests
{
    [Theory]
    [InlineData(LockForm.Obstacle, true)]
    [InlineData(LockForm.Interaction, true)]
    [InlineData(LockForm.Area, false)]
    public void OpeningAGateDoesNotAlwaysMakeItsCapabilityDisposable(LockForm form, bool canReturnWithoutIt)
    {
        var lab = new LockTestWorld();
        var context = lab.CreateContext();
        var station = lab.Stations.First(s => s.Route.Form == form && s.Route.ShortcutKind == ShortcutKind.None);
        var cell = lab.Grid.Locks.Single(g => g.RouteId == station.Route.Id).Cells[0];
        Assert.True(context.Gates.TryOpen(station.Route.Id, station.Approach, context.Held));
        Assert.False(context.Gates.NeedsOpening(station.Route));
        Assert.True(context.Gates.IsWalkable(cell, context.Held));
        Assert.Equal(canReturnWithoutIt, context.Gates.IsWalkable(cell, CapabilitySet.Empty));
    }

    [Theory]
    [InlineData(LockForm.Obstacle)]
    [InlineData(LockForm.Interaction)]
    public void CapabilityShortcutsKeepTheirRequirementEvenWithAnOpenedRecord(LockForm form)
    {
        var route = new CampaignRoute("shortcut", "a", "b", new Requirement(CapabilitySet.Of(Capability.Grapple)),
            form, shortcutKind: ShortcutKind.Capability);
        var recorded = new HashSet<string> { route.Id };
        Assert.True(route.CanTraverse(CapabilitySet.Of(Capability.Grapple), recorded));
        Assert.False(route.CanTraverse(CapabilitySet.Empty, recorded));
    }

    [Fact]
    public void OpenWaterStillNeedsBoatWhenNoGateOccupiesIt()
    {
        var lab = new LockTestWorld();
        var water = new bool[lab.Grid.Width, lab.Grid.Height];
        var cell = lab.Grid.PlayerStart;
        water[cell.X, cell.Y] = true;
        var layers = lab.Grid.Layers.ToDictionary(p => p.Key, p => p.Value);
        layers[OverworldLayers.NavigableWater] = new GridLayer(water);
        var grid = new OverworldGrid(lab.Campaign, layers, lab.Grid.Locations.ToDictionary(p => p.Key, p => p.Value),
            lab.Grid.Routes.ToDictionary(p => p.Key, p => p.Value), lab.Grid.Locks);
        Assert.Null(grid.LockAt(cell));
        Assert.True(grid.IsWalkable(cell, CapabilitySet.Of(Capability.Boat)));
        Assert.False(grid.IsWalkable(cell, CapabilitySet.Empty));
    }

    [Fact]
    public void SharedKeyCanBeRemovedOnlyAfterBothLocksAreResolved()
    {
        var lab = new LockTestWorld();
        var context = lab.CreateContext();
        var first = lab.Stations.First(s => s.Route.KeyId != null && !s.Route.IsWarp);
        var second = lab.Stations.Single(s => s.Route.KeyId == first.Route.KeyId && s.Route.Id != first.Route.Id);
        Assert.True(context.Gates.TryOpen(first.Route.Id, first.Approach, context.Held));
        context.Keys.Remove(first.Route.KeyId!);
        Assert.True(first.Route.CanTraverse(CapabilitySet.Empty, context.Resolved));
        Assert.False(context.Gates.TryOpen(second.Route.Id, second.Approach, context.Held));
        context.Keys.Add(first.Route.KeyId!);
        Assert.True(context.Gates.TryOpen(second.Route.Id, second.Approach, context.Held));
        context.Keys.Remove(first.Route.KeyId!);
        Assert.True(first.Route.CanTraverse(CapabilitySet.Empty, context.Resolved));
        Assert.True(second.Route.CanTraverse(CapabilitySet.Empty, context.Resolved));
    }

    [Fact]
    public void EngineeringIsNeededForUnclaimedVehicleRewardsOutsideLockChecks()
    {
        var campaign = CampaignGenerator.Generate(42);
        var vehicles = campaign.Sources.Where(s => s.Capability.Kind() == CapabilityKind.Vehicle).ToArray();
        var withoutEngineering = CapabilitySet.From(CapabilityCatalog.All.Where(c => c != Capability.Engineering));
        Assert.NotEmpty(vehicles);
        Assert.All(vehicles, source => {
            Assert.True(source.Prerequisites.IsSatisfiedBy(CapabilitySet.From(CapabilityCatalog.All)));
            Assert.False(source.Prerequisites.IsSatisfiedBy(withoutEngineering));
        });
    }

    [Fact]
    public void CompletedEncounterStillChecksCapabilitiesOnReentry()
    {
        var campaign = CampaignGenerator.Generate(42);
        var cave = campaign.Locations.Single(l => l.Encounter?.Kind == EncounterKind.BoulderCave);
        var completed = new HashSet<string> { cave.Id };
        Assert.True(cave.Encounter!.CanEnter(CapabilitySet.Of(Capability.Engineering), completed));
        Assert.False(cave.Encounter.CanEnter(CapabilitySet.Empty, completed));
    }

    [Fact]
    public void TwoIndividuallyRedundantAlternativesCannotBothBeDiscarded()
    {
        var route = new CampaignRoute("crossing", "a", "b", new Requirement(
            CapabilitySet.Of(Capability.Climb), CapabilitySet.Of(Capability.Grapple)), LockForm.Area);
        var resolved = new HashSet<string>();
        Assert.True(route.CanTraverse(CapabilitySet.Of(Capability.Climb), resolved));
        Assert.True(route.CanTraverse(CapabilitySet.Of(Capability.Grapple), resolved));
        Assert.False(route.CanTraverse(CapabilitySet.Empty, resolved));
    }

    [Fact]
    public void BenchingACompanionPreservesAcquisitionButLosesActiveTraversal()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var companion = context.Campaign.Companions.First();
        context.Roster.Add(companion.Id); context.Active.Add(companion.Id);
        Assert.True(context.Held.Contains(companion.Capability));
        context.Active.Clear();
        Assert.Contains(companion.Capability, CampaignGuidance.Acquired(context));
        Assert.False(context.Held.Contains(companion.Capability));
    }

    [Fact]
    public void DeletingASavedCapabilityDoesNotRestoreItsAlreadyClaimedReward()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        var source = context.Campaign.Sources.First(s => s.CompanionId == null && s.Capability == Capability.Engineering);
        var snapshot = context.Capture();
        snapshot.Permanent = Array.Empty<Capability>();
        snapshot.Claimed = new[] { source.Id };
        snapshot.Scene = "Town"; snapshot.LocationId = source.LocationId;
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), snapshot);
        Assert.Equal(source.LocationId, restored.Location!.Id);
        Assert.False(restored.Claim(source.Id));
        Assert.False(restored.Held.Contains(Capability.Engineering));
    }

    [Fact]
    public void AKeyWithNoMatchingRouteCanStillBeARequiredQuestFlag()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.RequiresOpeningPreparation = true;
        context.Keys.Add(CampaignContext.OpeningDayKey);
        Assert.DoesNotContain(context.Campaign.Routes, r => r.KeyId == CampaignContext.OpeningDayKey);
        Assert.True(context.CanEnterDungeon("story-0"));
        context.Keys.Remove(CampaignContext.OpeningDayKey);
        Assert.True(context.IsPreparingForOpening);
        Assert.False(context.CanEnterDungeon("story-0"));
    }

    [Fact]
    public void RemovingHomeKeyKeepsDoorOpenButMakesTheChestClaimableAgain()
    {
        var context = new CampaignContext(new(OverworldLaunchMode.Campaign, 42));
        context.State.RequiresOpeningPreparation = true; context.State.HomeLessonVersion = 1;
        context.State.Scene = "Town"; context.State.LocationId = "town-0";
        Assert.True(context.ClaimHomeKey());
        Assert.True(context.UnlockHomeDoor());
        var snapshot = context.Capture();
        snapshot.Permanent = Array.Empty<Capability>();
        var restored = new CampaignContext(new(OverworldLaunchMode.Campaign), snapshot);
        Assert.True(restored.State.HomeDoorUnlocked);
        Assert.True(restored.ClaimHomeKey());
    }
}
