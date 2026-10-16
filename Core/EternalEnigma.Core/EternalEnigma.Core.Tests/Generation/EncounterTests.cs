using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class EncounterTests
{
    [Fact]
    public void EveryCampaignCoversAllArchetypesAndCompletionGatesKeys()
    {
        for (int seed = 0; seed < 32; seed++)
        {
            var campaign = CampaignGenerator.Generate(seed);
            Assert.Equal(Enum.GetValues<EncounterKind>().OrderBy(k => k), campaign.Locations.Where(l => l.Encounter != null).Select(l => l.Encounter!.Kind).OrderBy(k => k));
            Assert.All(campaign.Routes.Where(r => r.KeyId != null), r => Assert.Equal(KeyAcquisition.DungeonCompletion, r.KeyCondition));
            Assert.Single(campaign.Routes, r => r.IsStarterExit && !r.IsTownExit);
            var cave = campaign.Locations.Single(l => l.Encounter?.Kind == EncounterKind.BoulderCave);
            Assert.True(cave.Stage > 0); Assert.True(cave.Encounter!.EntryRequirement.IsSatisfiedBy(CapabilitySet.Of(Capability.Engineering)));
            var bridge = campaign.Locations.Single(l => l.Encounter?.Kind == EncounterKind.Bridge);
            var route = campaign.Routes.Single(r => r.Id == bridge.Encounter!.BridgeRouteId);
            var all = CapabilitySet.From(campaign.Manifest.Select(c => c.Id));
            Assert.False(route.CanTraverse(all, new HashSet<string>()));
            Assert.True(route.CanTraverse(all, new HashSet<string> { route.Id }));
            Assert.Equal(route.Requirement.ToString(), bridge.Encounter!.EntryRequirement.ToString());
        }
    }

    [Fact]
    public void FloorsArePlayableDeterministicConnectedAndEveryPuzzleWitnessReplays()
    {
        foreach (var kind in Enum.GetValues<EncounterKind>())
        for (int tier = 0; tier <= 4; tier++)
        for (int seed = 0; seed < 4; seed++)
        {
            var location = new CampaignLocation("test-" + kind, "region-0", tier, LocationKind.Landmark, encounter: new EncounterDefinition(kind, tier));
            var plan = new DungeonRunPlan(seed, location);
            Assert.Equal(location.Encounter!.FloorCount, plan.Floors.Count);
            foreach (var step in plan.Floors)
            {
                var floor = DungeonFloorGenerator.Generate(step.Options);
                var again = DungeonFloorGenerator.Generate(step.Options);
                Assert.False(floor.IsThroneFloor);
                var validation = DungeonFloorValidator.Validate(floor, step.Options);
                Assert.True(validation.IsValid, $"{kind}/{tier}/{seed}/{step.Number}: {string.Join(";", validation.Errors)}");
                Assert.Equal(DungeonFloorFingerprint.Compute(floor), DungeonFloorFingerprint.Compute(again));
                if (kind == EncounterKind.LostWoods) Assert.Equal(49 + 4 * tier, floor.Width);
                if (kind == EncounterKind.Sokoban) Assert.Equal(2 + tier / 2, floor.PuzzleObjects.Count(p => p.Kind == PuzzleObjectKind.Boulder));
                if (floor.Objective.AllowsReset) { Assert.False(new PuzzleState(floor).Complete); Assert.True(PuzzleState.ValidateWitness(floor)); }
            }
        }
    }

    [Fact]
    public void RequiredIdentitiesAndReinforcementsCannotCompleteByDespawnOrDuplicateCallbacks()
    {
        var location = new CampaignLocation("ambush", "region-0", 0, LocationKind.Landmark, encounter: new EncounterDefinition(EncounterKind.Ambush, 0));
        var floor = DungeonFloorGenerator.Generate(new DungeonRunPlan(42, location).Floors[0].Options);
        var progress = new DungeonObjectiveProgress(floor);
        Assert.False(progress.CanExit); Assert.False(progress.Defeat("unrelated"));
        Assert.True(progress.RegisterReinforcement("reinforcement"));
        foreach (var enemy in floor.ObjectiveEnemies) Assert.True(progress.Defeat(enemy.Id));
        Assert.False(progress.CanExit); Assert.True(progress.Defeat("reinforcement")); Assert.True(progress.CanExit);
        Assert.False(progress.Defeat("reinforcement")); Assert.False(progress.RegisterReinforcement("after-victory"));
    }

    [Fact]
    public void FootprintsPickupsAndCollectionArePersistentAndNonblocking()
    {
        var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
        var grid = context.Grid;
        Assert.Equal(18, grid.Pickups.Count);
        foreach (var region in context.Campaign.Regions)
        {
            var pickups = grid.Pickups.Where(p => p.RegionId == region.Id).ToArray();
            Assert.Single(pickups, p => p.Kind == OverworldPickupKind.Weapon); Assert.Equal(2, pickups.Count(p => p.Kind == OverworldPickupKind.SkillScroll));
            Assert.All(pickups, p => Assert.True(grid.IsGround(p.Position) && grid.LockAt(p.Position) == null && !grid.RequiresBoat(p.Position)));
        }
        var used = new HashSet<GridPoint>();
        foreach (var footprint in grid.LocationFootprints)
        {
            Assert.Equal(footprint.EncounterKind == EncounterKind.LostWoods ? 49 : 25, footprint.Cells.Count);
            Assert.All(footprint.Cells, p => Assert.True(used.Add(p) && grid.LockAt(p) == null && !grid.TownFootprints.Any(t => t.Cells.Contains(p))));
        }
        var pickup = grid.Pickups[0]; context.Position = pickup.Position;
        Assert.False(context.TryCollectPickup(pickup.Id, _ => false)); Assert.DoesNotContain(pickup.Id, context.CollectedPickups);
        Assert.True(context.TryCollectPickup(pickup.Id, _ => true)); Assert.False(context.TryCollectPickup(pickup.Id, _ => throw new Exception("Duplicate item grant")));
        var restored = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), context.Capture()); Assert.Contains(pickup.Id, restored.CollectedPickups);
    }

    [Fact]
    public void FollowersCannotOperateAndResetReplaysIdenticalPuzzleState()
    {
        foreach (var kind in new[] { EncounterKind.Sokoban, EncounterKind.LightPattern, EncounterKind.Lure, EncounterKind.Herding })
        {
            var location = new CampaignLocation("puzzle", "region-0", 2, LocationKind.Secret, encounter: new EncounterDefinition(kind, 2));
            var floor = DungeonFloorGenerator.Generate(new DungeonRunPlan(14, location).Floors[0].Options);
            var state = new PuzzleState(floor); Assert.False(state.TryMove(floor.SolutionWitness[0], controlledCharacter: false));
            foreach (var move in floor.SolutionWitness) Assert.True(state.TryMove(move));
            Assert.True(state.Complete); var reset = new PuzzleState(floor); Assert.False(reset.Complete);
            foreach (var move in floor.SolutionWitness) Assert.True(reset.TryMove(move)); Assert.True(reset.Complete);
        }
    }

    [Fact]
    public void BoulderPushesRejectChainsDiagonalsWallsAndUncontrolledMoves()
    {
        var mask = new bool[12, 12]; var inverse = new bool[12, 12];
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) { mask[x, y] = x >= 2 && y >= 2 && x < 10 && y < 10; inverse[x, y] = !mask[x, y]; }
        var objects = new[] { new PuzzleObjectPlacement("a", PuzzleObjectKind.Boulder, new GridPoint(4, 4)),
            new PuzzleObjectPlacement("b", PuzzleObjectKind.Boulder, new GridPoint(5, 4)),
            new PuzzleObjectPlacement("wall", PuzzleObjectKind.Boulder, new GridPoint(2, 6)),
            new PuzzleObjectPlacement("goal", PuzzleObjectKind.Goal, new GridPoint(7, 7)) };
        var floor = new DungeonFloor(0, false, new Dictionary<string, GridLayer> { [DungeonLayers.Floor] = new(mask), [DungeonLayers.Dungeon] = new(inverse) },
            Array.Empty<GridRect>(), new GridPoint(3, 4), new GridPoint(9, 9), Array.Empty<Placement>(), Array.Empty<Placement>(), Array.Empty<Placement>(), Array.Empty<Placement>(),
            objective: new DungeonObjectiveDefinition(DungeonObjectiveKind.PositionObjects), puzzleObjects: objects);
        var state = new PuzzleState(floor);
        Assert.False(state.TryPush(new GridPoint(4, 4))); // No pushing chains.
        state.SetPlayerPosition(new GridPoint(3, 3)); Assert.False(state.TryPush(new GridPoint(4, 4))); // No diagonal pushes.
        state.SetPlayerPosition(new GridPoint(3, 6)); Assert.False(state.TryPush(new GridPoint(2, 6))); // Wall.
        state.SetPlayerPosition(new GridPoint(4, 3)); Assert.False(state.TryMove(new GridPoint(4, 4), controlledCharacter: false));
        Assert.Equal(new GridPoint(4, 4), state.Boulders["a"]);
        Assert.True(state.TryPush(new GridPoint(4, 4))); Assert.Equal(new GridPoint(4, 5), state.Boulders["a"]);
    }

    [Fact]
    public void FinalDungeonUsesReverseWorldAndUsesTwoFloorsAndEndsWithStartingTownCastleMimic()
    {
        var campaign = CampaignGenerator.Generate(42); var location = campaign.Locations.Single(l => l.Id == campaign.FinalLocationId);
        var plan = new DungeonRunPlan(campaign.Seed, location);
        Assert.Equal(DungeonVariant.Overworld, plan.Variant); Assert.Single(plan.Floors);
        var finale = new FinalePlan(campaign, OverworldGridGenerator.Generate(campaign));
        Assert.Equal(2, finale.Floors.Count);
        var last = finale.Floors.Last();
        Assert.Equal("Castle Monster", last.Enemies.Single(e => e.LocationId == last.Campaign.StartLocationId).Family);
        Assert.False(last.CreateFloor().IsThroneFloor);
        Assert.Equal(32, BossCatalog.All.Count); Assert.Equal(32, BossCatalog.All.Select(p => p.Family).Distinct().Count());
    }
}
