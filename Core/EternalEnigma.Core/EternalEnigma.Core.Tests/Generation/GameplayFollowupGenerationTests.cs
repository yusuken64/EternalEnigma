using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class GameplayFollowupGenerationTests
{
    [Fact]
    public void TrapsCoverEveryCombatVariantWithoutBlockingTheSafeRoute()
    {
        foreach (var biome in Enum.GetValues<OverworldBiome>())
        foreach (var variant in new[] { DungeonVariant.Standard, DungeonVariant.Town })
        foreach (int tier in new[] { 0, 4 })
        for (int seed = 0; seed < 6; seed++)
        {
            var options = DungeonLayoutProfile.Options(seed, biome, tier, variant: variant);
            var floor = DungeonFloorGenerator.Generate(options);
            Assert.Equal(options.TrapCount, floor.Traps.Count);
            var reserved = DungeonTrapPlacement.ProtectedCells(floor);
            Assert.All(floor.Traps, p => { Assert.True(floor.IsWalkable(p.Cell)); Assert.DoesNotContain(p.Cell, reserved); });
            var blocked = floor.Traps.Select(p => p.Cell).Concat(floor.Scenery.Select(p => p.Cell)).Concat(floor.Locks.Select(p => p.Door)).ToHashSet();
            Assert.Contains(floor.Stairs, GridSearch.VisitOrder(floor.Start, p => PuzzleState.CardinalNeighbors(p).Where(q => floor.IsWalkable(q) && !blocked.Contains(q))));
            Assert.True(DungeonFloorValidator.Validate(floor, options).IsValid);
        }
    }

    [Fact]
    public void EveryEncounterKeepsTrapsOffItsObjectiveAndSolution()
    {
        foreach (var kind in Enum.GetValues<EncounterKind>())
        for (int seed = 0; seed < 8; seed++)
        {
            var location = new CampaignLocation("test-" + kind, "region-0", seed % 5, LocationKind.Landmark, encounter: new EncounterDefinition(kind, seed % 5));
            foreach (var step in new DungeonRunPlan(seed, location).Floors)
            {
                var floor = DungeonFloorGenerator.Generate(step.Options);
                Assert.Equal(step.Options.TrapCount, floor.Traps.Count);
                Assert.True(PuzzleState.ValidateWitness(floor));
                var protectedCells = DungeonTrapPlacement.ProtectedCells(floor);
                Assert.All(floor.Traps, t => Assert.DoesNotContain(t.Cell, protectedCells));
                Assert.Equal(DungeonFloorFingerprint.Compute(floor), DungeonFloorFingerprint.Compute(DungeonFloorGenerator.Generate(step.Options)));
            }
        }
    }

    [Fact]
    public void ForestMazeStaysWithinTheNormalForestFootprintAndReachesEveryRoom()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var options = new DungeonFloorOptions(seed, 49, 49, encounter: EncounterKind.LostWoods);
            var footprint = ForestMazeGenerator.Footprint(options);
            var floor = DungeonFloorGenerator.Generate(options);
            int reflection = new SeedStream(seed, 1705).Range(4);
            GridPoint Original(GridPoint p) => new((reflection & 1) == 0 ? p.X : floor.Width - 1 - p.X,
                (reflection & 2) == 0 ? p.Y : floor.Height - 1 - p.Y);
            var cells = GridSearch.VisitOrder(floor.Start, floor.Neighbors).Select(Original).ToHashSet();
            Assert.All(cells, p => Assert.True(footprint.IsWalkable(p)));
            Assert.All(footprint.Rooms, room => Assert.Contains(room.Cells(), cells.Contains));
            Assert.True(cells.Count < footprint.Layers[DungeonLayers.Floor].ToArray().Cast<bool>().Count(v => v));
            Assert.True(cells.Count > 150);
        }
    }

    [Fact]
    public void DistrictGatePairsVaryAndRemainSeparateWithPartyClearance()
    {
        var arrivals = new HashSet<GridPoint>(); var exits = new HashSet<GridPoint>();
        for (int seed = 0; seed < 16; seed++)
        {
            var floor = DungeonFloorGenerator.Generate(DungeonLayoutProfile.Options(seed, OverworldBiome.Forest, 0, variant: DungeonVariant.Town));
            arrivals.Add(floor.Start); exits.Add(floor.Stairs);
            Assert.Equal(floor.Start, floor.TownPlan!.PartySpawn); Assert.Equal(floor.Start, floor.TownPlan.Exit);
            Assert.True(floor.Distance(floor.Start, floor.Stairs) >= 8);
            Assert.All(new GridRect(floor.Start.X - 1, floor.Start.Y - 1, 3, 3).Cells(), p => Assert.True(floor.IsWalkable(p)));
        }
        Assert.True(arrivals.Count >= 4); Assert.True(exits.Count >= 4);
    }
}
