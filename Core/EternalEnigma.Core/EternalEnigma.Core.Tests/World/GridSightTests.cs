using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.World;

public sealed class GridSightTests
{
    private static GridLayer Open(int size)
    {
        var map = new bool[size, size];
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++) map[x, y] = true;
        return new GridLayer(map);
    }

    [Fact]
    public void WallsAreVisibleButBlockTilesBehindThem()
    {
        var map = Open(7);
        var mapArray = map.ToArray();
        for (int y = 0; y < 7; y++) mapArray[3, y] = false;
        var mapWithWall = new GridLayer(mapArray);

        var tiles = GridSight.VisibleTiles(mapWithWall, new GridPoint(1, 3), 8);
        Assert.Contains(new GridPoint(3, 3), tiles);
        Assert.DoesNotContain(new GridPoint(4, 3), tiles);
        Assert.DoesNotContain(new GridPoint(5, 5), tiles);

        var mapArray2 = mapWithWall.ToArray();
        mapArray2[3, 3] = true;
        var mapWithOpening = new GridLayer(mapArray2);
        Assert.True(GridSight.HasLineOfSight(mapWithOpening, new GridPoint(1, 3), new GridPoint(5, 3)));
    }

    [Fact]
    public void CorridorSightHasSymmetricEdgesAndCannotCutCorners()
    {
        var map = Open(7);
        var center = new GridPoint(3, 3);
        Assert.Equal(9, GridSight.VisibleTiles(map, center, 1).Count);
        Assert.DoesNotContain(new GridPoint(5, 3), GridSight.VisibleTiles(map, center, 1));

        var mapArray = map.ToArray();
        mapArray[4, 3] = false;
        var mapWithBlockade = new GridLayer(mapArray);
        Assert.False(GridSight.HasLineOfSight(mapWithBlockade, center, new GridPoint(4, 4)));
        Assert.False(GridSight.HasLineOfSight(mapWithBlockade, new GridPoint(4, 4), center));
    }

    [Fact]
    public void RoomClassificationAndMapEdgesAreExplicit()
    {
        var corridor = new bool[7, 7];
        for (int x = 0; x < 7; x++) corridor[x, 3] = true;
        var corridorLayer = new GridLayer(corridor);
        Assert.False(GridSight.IsRoom(corridorLayer, new GridPoint(3, 3)));

        var corridorArray = corridor;
        corridorArray[3, 4] = corridorArray[4, 4] = true;
        var roomLayer = new GridLayer(corridorArray);
        Assert.True(GridSight.IsRoom(roomLayer, new GridPoint(3, 3)));

        Assert.Equal(49, GridSight.VisibleTiles(Open(7), new GridPoint(0, 0), 8).Count);
        Assert.Empty(GridSight.VisibleTiles(corridorLayer, new GridPoint(-1, 0), 8));
    }
}
