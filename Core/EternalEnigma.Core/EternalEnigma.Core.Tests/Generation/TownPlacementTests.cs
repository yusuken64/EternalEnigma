using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownPlacementTests
{
    private static bool IsReserved(GridPoint p, int height) =>
        p.X >= 8 && p.X <= 12 && p.Y >= 0 && p.Y <= height / 2;

    [Fact]
    public void CorridorShortfallGetsDeterministicReachableUnoccupiedReplacement()
    {
        var floorMap = new bool[20, 20];
        var alliesMap = new bool[20, 20];
        for (int x = 0; x < 20; x++)
        {
            for (int y = 0; y < 20; y++)
            {
                floorMap[x, y] = x != 3;
            }
        }
        alliesMap[7, 2] = true;

        var floor = new GridLayer(floorMap);
        var allies = new GridLayer(alliesMap);
        var existing = new[] { new GridPoint(6, 7), new GridPoint(15, 7), new GridPoint(15, 13) };
        var spawn = new GridPoint(10, 2);

        var result = TownPlacement.CompleteBuildingPositions(floor, allies, existing, spawn, 4);

        Assert.Equal(4, result.Count);
        Assert.Equal(existing, result.Take(3));
        Assert.True(result.All(p => p.X > 3 && floorMap[p.X, p.Y] && !alliesMap[p.X, p.Y] &&
            !IsReserved(p, 20)));
        Assert.Equal(4, result.Distinct().Count());
        Assert.Equal(result, TownPlacement.CompleteBuildingPositions(floor, allies, existing, spawn, 4));
    }
}
