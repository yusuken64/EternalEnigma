using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Validation;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class DungeonFloorGeneratorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GeneratesDeterministicValidFloor(int seed)
    {
        // Generate twice with the same seed
        var options = new DungeonFloorOptions(seed);
        var floor1 = DungeonFloorGenerator.Generate(options);
        var floor2 = DungeonFloorGenerator.Generate(options);

        // Verify dimensions
        Assert.Equal(32, floor1.Width);
        Assert.Equal(32, floor1.Height);
        Assert.Equal(32, floor2.Width);
        Assert.Equal(32, floor2.Height);

        // Verify layers are identical cell-by-cell
        foreach (var layer in DungeonLayers.All)
        {
            if (!floor1.Layers.ContainsKey(layer)) continue;
            var array1 = floor1.Layers[layer].ToArray();
            var array2 = floor2.Layers[layer].ToArray();
            for (int x = 0; x < floor1.Width; x++)
            {
                for (int y = 0; y < floor1.Height; y++)
                {
                    Assert.Equal(array1[x, y], array2[x, y]);
                }
            }
        }

        // Verify Start and Stairs are equal
        Assert.Equal(floor1.Start, floor2.Start);
        Assert.Equal(floor1.Stairs, floor2.Stairs);

        // Verify placements are equal
        Assert.Equal(floor1.Enemies.Count, floor2.Enemies.Count);
        for (int i = 0; i < floor1.Enemies.Count; i++)
            Assert.Equal(floor1.Enemies[i], floor2.Enemies[i]);

        Assert.Equal(floor1.Gold.Count, floor2.Gold.Count);
        for (int i = 0; i < floor1.Gold.Count; i++)
            Assert.Equal(floor1.Gold[i], floor2.Gold[i]);

        Assert.Equal(floor1.Items.Count, floor2.Items.Count);
        for (int i = 0; i < floor1.Items.Count; i++)
            Assert.Equal(floor1.Items[i], floor2.Items[i]);

        Assert.Equal(floor1.Traps.Count, floor2.Traps.Count);
        for (int i = 0; i < floor1.Traps.Count; i++)
            Assert.Equal(floor1.Traps[i], floor2.Traps[i]);

        Assert.Equal(floor1.GatheringSites.Count, floor2.GatheringSites.Count);
        for (int i = 0; i < floor1.GatheringSites.Count; i++)
            Assert.Equal(floor1.GatheringSites[i], floor2.GatheringSites[i]);

        Assert.Equal(options.GatheringCount, floor1.GatheringSites.Count);

        // Verify validation
        var validation = DungeonFloorValidator.Validate(floor1, options);
        Assert.True(validation.IsValid, string.Join("\n", validation.Errors));

        // Verify placement counts
        Assert.Equal(10, floor1.Enemies.Count);
        Assert.Equal(5, floor1.Gold.Count);
        Assert.Equal(5, floor1.Items.Count);
        Assert.Equal(5, floor1.Traps.Count);

        // Verify Start is in a room
        Assert.True(floor1.IsRoom(floor1.Start));

        // Verify Start != Stairs
        Assert.NotEqual(floor1.Start, floor1.Stairs);
    }

    [Fact]
    public void ThroneFloorIsFixed()
    {
        var floor1 = DungeonFloorGenerator.Generate(DungeonFloorOptions.Throne(1));
        var floor2 = DungeonFloorGenerator.Generate(DungeonFloorOptions.Throne(2));

        // Verify layers are identical
        foreach (var layer in DungeonLayers.All)
        {
            if (!floor1.Layers.ContainsKey(layer)) continue;
            var array1 = floor1.Layers[layer].ToArray();
            var array2 = floor2.Layers[layer].ToArray();
            for (int x = 0; x < floor1.Width; x++)
            {
                for (int y = 0; y < floor1.Height; y++)
                {
                    Assert.Equal(array1[x, y], array2[x, y]);
                }
            }
        }

        // Verify dimensions
        Assert.Equal(12, floor1.Width);
        Assert.Equal(12, floor1.Height);
        Assert.Equal(12, floor2.Width);
        Assert.Equal(12, floor2.Height);

        // Verify Start and Stairs positions
        Assert.Equal(new GridPoint(6, 4), floor1.Start);
        Assert.Equal(new GridPoint(6, 9), floor1.Stairs);
        Assert.Equal(floor1.Start, floor2.Start);
        Assert.Equal(floor1.Stairs, floor2.Stairs);

        // Verify no placements
        Assert.Empty(floor1.Enemies);
        Assert.Empty(floor1.Gold);
        Assert.Empty(floor1.Items);
        Assert.Empty(floor1.Traps);
        Assert.Empty(floor1.GatheringSites);

        // Verify throne flag
        Assert.True(floor1.IsThroneFloor);
        Assert.True(floor2.IsThroneFloor);
    }

    [Fact]
    public void SeedSweepIsValid()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            var options = new DungeonFloorOptions(seed);
            var floor = DungeonFloorGenerator.Generate(options);
            var validation = DungeonFloorValidator.Validate(floor, options);
            Assert.True(validation.IsValid, $"Seed {seed}: {string.Join("\n", validation.Errors)}");

            var requiredPath = GatheringPlacement.RequiredPath(floor.Layers[DungeonLayers.Floor], floor.Start, floor.Stairs);
            foreach (var site in floor.GatheringSites)
            {
                Assert.DoesNotContain(site.Cell, requiredPath);
            }
        }
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        var floor1 = DungeonFloorGenerator.Generate(new DungeonFloorOptions(1));
        var floor2 = DungeonFloorGenerator.Generate(new DungeonFloorOptions(2));

        // Verify at least one layer differs
        var floorLayer1 = floor1.Layers[DungeonLayers.Floor].ToArray();
        var floorLayer2 = floor2.Layers[DungeonLayers.Floor].ToArray();

        bool identical = true;
        for (int x = 0; x < floor1.Width; x++)
        {
            for (int y = 0; y < floor1.Height; y++)
            {
                if (floorLayer1[x, y] != floorLayer2[x, y])
                {
                    identical = false;
                    break;
                }
            }
            if (!identical) break;
        }

        Assert.False(identical, "Different seeds should produce different floors");
    }

    [Fact]
    public void OptionsGuardRanges()
    {
        // Test invalid width (too small)
        Assert.Throws<ArgumentOutOfRangeException>(() => new DungeonFloorOptions(1, width: 8));
    }

    [Fact]
    public void GatheringCountZeroGivesNoSites()
    {
        var options = new DungeonFloorOptions(9, gatheringCount: 0);
        var floor = DungeonFloorGenerator.Generate(options);
        Assert.Empty(floor.GatheringSites);

        var validation = DungeonFloorValidator.Validate(floor, options);
        Assert.True(validation.IsValid, string.Join("\n", validation.Errors));
    }

    [Fact]
    public void GatheringDoesNotChangeExistingPlacements()
    {
        var optionsNoGathering = new DungeonFloorOptions(42, gatheringCount: 0);
        var optionsWithGathering = new DungeonFloorOptions(42);

        var floorNoGathering = DungeonFloorGenerator.Generate(optionsNoGathering);
        var floorWithGathering = DungeonFloorGenerator.Generate(optionsWithGathering);

        Assert.Equal(floorNoGathering.Start, floorWithGathering.Start);
        Assert.Equal(floorNoGathering.Stairs, floorWithGathering.Stairs);

        Assert.Equal(floorNoGathering.Enemies.Count, floorWithGathering.Enemies.Count);
        for (int i = 0; i < floorNoGathering.Enemies.Count; i++)
            Assert.Equal(floorNoGathering.Enemies[i], floorWithGathering.Enemies[i]);

        Assert.Equal(floorNoGathering.Gold.Count, floorWithGathering.Gold.Count);
        for (int i = 0; i < floorNoGathering.Gold.Count; i++)
            Assert.Equal(floorNoGathering.Gold[i], floorWithGathering.Gold[i]);

        Assert.Equal(floorNoGathering.Items.Count, floorWithGathering.Items.Count);
        for (int i = 0; i < floorNoGathering.Items.Count; i++)
            Assert.Equal(floorNoGathering.Items[i], floorWithGathering.Items[i]);

        Assert.Equal(floorNoGathering.Traps.Count, floorWithGathering.Traps.Count);
        for (int i = 0; i < floorNoGathering.Traps.Count; i++)
            Assert.Equal(floorNoGathering.Traps[i], floorWithGathering.Traps[i]);

        // Verify Floor layer cells are identical
        var arrayNoGathering = floorNoGathering.Layers[DungeonLayers.Floor].ToArray();
        var arrayWithGathering = floorWithGathering.Layers[DungeonLayers.Floor].ToArray();
        for (int x = 0; x < floorNoGathering.Width; x++)
        {
            for (int y = 0; y < floorNoGathering.Height; y++)
            {
                Assert.Equal(arrayNoGathering[x, y], arrayWithGathering[x, y]);
            }
        }
    }

    [Fact]
    public void GenerationVersionIsTwo()
    {
        Assert.Equal(2, DungeonFloor.GenerationVersion);
    }

    [Fact]
    public void OptionsRejectGatheringOnThrone()
    {
        // Verify that throne options reject gathering
        Assert.Throws<ArgumentException>(() => new DungeonFloorOptions(1, 12, 12, true, 0, 0, 0, 0, 1));

        // Verify that throne options have gathering count of 0
        Assert.Equal(0, DungeonFloorOptions.Throne(1).GatheringCount);
    }
}
