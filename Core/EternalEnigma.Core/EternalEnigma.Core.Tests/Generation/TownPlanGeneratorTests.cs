using System;
using System.Diagnostics;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownPlanGeneratorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GeneratesDeterministicValidPlan(int seed)
    {
        // Generate twice with the same seed
        var options = new TownPlanOptions(seed);
        var plan1 = TownPlanGenerator.Generate(options);
        var plan2 = TownPlanGenerator.Generate(options);

        // Verify dimensions
        Assert.Equal(15, plan1.Width);
        Assert.Equal(15, plan1.Height);
        Assert.Equal(plan1.Width, plan2.Width);
        Assert.Equal(plan1.Height, plan2.Height);

        // Verify all layers are equal
        foreach (var layerName in TownLayers.All)
        {
            var layer1 = plan1.Layers[layerName].ToArray();
            var layer2 = plan2.Layers[layerName].ToArray();

            for (int x = 0; x < plan1.Width; x++)
            {
                for (int y = 0; y < plan1.Height; y++)
                {
                    Assert.True(layer1[x, y] == layer2[x, y],
                        $"Layer {layerName} differs at ({x},{y})");
                }
            }
        }

        // Verify building slots are equal
        Assert.Equal(plan1.BuildingSlots.Count, plan2.BuildingSlots.Count);
        for (int i = 0; i < plan1.BuildingSlots.Count; i++)
        {
            Assert.Equal(plan1.BuildingSlots[i], plan2.BuildingSlots[i]);
        }

        // Verify ally slots are equal
        Assert.Equal(plan1.AllySlots.Count, plan2.AllySlots.Count);
        for (int i = 0; i < plan1.AllySlots.Count; i++)
        {
            Assert.Equal(plan1.AllySlots[i].Cell, plan2.AllySlots[i].Cell);
            Assert.Equal(plan1.AllySlots[i].Roll, plan2.AllySlots[i].Roll);
        }

        // Verify shop rooms are equal
        Assert.Equal(plan1.ShopRooms.Count, plan2.ShopRooms.Count);
        for (int i = 0; i < plan1.ShopRooms.Count; i++)
        {
            Assert.Equal(plan1.ShopRooms[i].Door, plan2.ShopRooms[i].Door);
            Assert.Equal(plan1.ShopRooms[i].Floor.Count, plan2.ShopRooms[i].Floor.Count);
            for (int j = 0; j < plan1.ShopRooms[i].Floor.Count; j++)
            {
                Assert.Equal(plan1.ShopRooms[i].Floor[j], plan2.ShopRooms[i].Floor[j]);
            }
            Assert.Equal(plan1.ShopRooms[i].Wall.Count, plan2.ShopRooms[i].Wall.Count);
            for (int j = 0; j < plan1.ShopRooms[i].Wall.Count; j++)
            {
                Assert.Equal(plan1.ShopRooms[i].Wall[j], plan2.ShopRooms[i].Wall[j]);
            }
        }

        // Verify validator passes
        var validation = Validation.TownPlanValidator.Validate(plan1, options);
        Assert.True(validation.IsValid,
            $"Validation failed: {string.Join("; ", validation.Errors)}");

        // Verify building slots count and properties
        Assert.Equal(4, plan1.BuildingSlots.Count);

        // Verify building slots are in raster order (y-inner, x-outer)
        for (int i = 0; i < plan1.BuildingSlots.Count - 1; i++)
        {
            var curr = plan1.BuildingSlots[i];
            var next = plan1.BuildingSlots[i + 1];
            bool inOrder = (curr.Y < next.Y) || (curr.Y == next.Y && curr.X < next.X);
            Assert.True(inOrder, $"Building slots not in raster order at index {i}");
        }

        // Verify exactly one shop room
        Assert.Single(plan1.ShopRooms);
        var shopRoom = plan1.ShopRooms[0];

        // Verify shop room door is at building slot 1
        Assert.Equal(plan1.BuildingSlots[1], shopRoom.Door);

        // Verify vendor anchor is at door + (0, 3)
        var expectedAnchor = new GridPoint(shopRoom.Door.X, shopRoom.Door.Y + 3);
        Assert.Equal(expectedAnchor, shopRoom.VendorAnchor);

        // Verify party spawn and exit are walkable
        Assert.True(plan1.IsWalkable(plan1.PartySpawn),
            $"PartySpawn at {plan1.PartySpawn} is not walkable");
        Assert.True(plan1.IsWalkable(plan1.Exit),
            $"Exit at {plan1.Exit} is not walkable");

        // Verify every reserved corridor cell is walkable
        for (int x = 0; x < plan1.Width; x++)
        {
            for (int y = 0; y < plan1.Height; y++)
            {
                var cell = new GridPoint(x, y);
                if (TownPlan.IsReservedCorridor(cell, plan1.Height))
                {
                    Assert.True(plan1.IsWalkable(cell),
                        $"Reserved corridor cell {cell} is not walkable");
                }
            }
        }

        // Verify ally slots count
        Assert.Equal(3, plan1.AllySlots.Count);
    }

    [Fact]
    public void BuildingSlotsIndependentOfShopFlags()
    {
        int seed = 42;
        var defaultOptions = new TownPlanOptions(seed);
        var allFalseOptions = new TownPlanOptions(seed, 15, 15,
            new[] { false, false, false, false });

        var plan1 = TownPlanGenerator.Generate(defaultOptions);
        var plan2 = TownPlanGenerator.Generate(allFalseOptions);

        // Both should have same building slots
        Assert.Equal(plan1.BuildingSlots.Count, plan2.BuildingSlots.Count);
        for (int i = 0; i < plan1.BuildingSlots.Count; i++)
        {
            Assert.Equal(plan1.BuildingSlots[i], plan2.BuildingSlots[i]);
        }
    }

    [Fact]
    public void CustomOptionsAreValid()
    {
        var options = new TownPlanOptions(7, 20, 20,
            new[] { true, false, true, false, false, false },
            allyCount: 6);

        var plan = TownPlanGenerator.Generate(options);

        var validation = Validation.TownPlanValidator.Validate(plan, options);
        Assert.True(validation.IsValid,
            $"Validation failed: {string.Join("; ", validation.Errors)}");

        Assert.Equal(6, plan.BuildingSlots.Count);
        Assert.Equal(6, plan.AllySlots.Count);
    }

    [Fact]
    public void SeedSweepIsValid()
    {
        var sw = Stopwatch.StartNew();

        for (int seed = 0; seed < 500; seed++)
        {
            var options = new TownPlanOptions(seed);
            var plan = TownPlanGenerator.Generate(options);

            var validation = Validation.TownPlanValidator.Validate(plan, options);
            Assert.True(validation.IsValid,
                $"Seed {seed} validation failed: {string.Join("; ", validation.Errors)}");
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 10000,
            $"500-seed sweep took {sw.ElapsedMilliseconds}ms, expected under 10000ms");
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        var plan1 = TownPlanGenerator.Generate(new TownPlanOptions(1));
        var plan2 = TownPlanGenerator.Generate(new TownPlanOptions(2));

        var houses1 = plan1.Layers[TownLayers.Houses].ToArray();
        var houses2 = plan2.Layers[TownLayers.Houses].ToArray();

        bool identical = true;
        for (int x = 0; x < plan1.Width; x++)
        {
            for (int y = 0; y < plan1.Height; y++)
            {
                if (houses1[x, y] != houses2[x, y])
                {
                    identical = false;
                    break;
                }
            }
            if (!identical) break;
        }

        Assert.False(identical, "Houses layers of different seeds should differ");
    }
}
