using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class ShopInteriorsTests
{
    private static GridLayer Doors(int width, int height, params GridPoint[] markers)
    {
        var map = new bool[width, height];
        foreach (var m in markers) map[m.X, m.Y] = true;
        return new GridLayer(map);
    }

    [Fact]
    public void ShopMarkerCarvesFloorAndWallRing()
    {
        var doors = Doors(20, 20, new GridPoint(10, 2));
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);

        Assert.Single(rooms);
        var room = rooms[0];
        Assert.Equal(new GridPoint(10, 2), room.Door);
        Assert.Equal(new HashSet<GridPoint>
        {
            new GridPoint(10, 3), new GridPoint(10, 4), new GridPoint(10, 5)
        }, new HashSet<GridPoint>(room.Floor));
        Assert.Equal(new HashSet<GridPoint>
        {
            new GridPoint(9, 3), new GridPoint(11, 3),
            new GridPoint(9, 4), new GridPoint(11, 4),
            new GridPoint(9, 5), new GridPoint(11, 5),
            new GridPoint(9, 6), new GridPoint(10, 6), new GridPoint(11, 6)
        }, new HashSet<GridPoint>(room.Wall));
        Assert.Empty(room.Floor.Intersect(room.Wall));
    }

    [Fact]
    public void NonShopMarkerGetsNoRoom()
    {
        var doors = Doors(20, 20, new GridPoint(10, 2));
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { false }, 20, 20);
        Assert.Empty(rooms);
    }

    [Fact]
    public void RaisterOrderMatchesShopFlagsIndex()
    {
        // Town.GetPositions raster-scans x-outer/y-inner, so the marker at the lower x comes first.
        var doors = Doors(20, 20, new GridPoint(5, 2), new GridPoint(10, 2));
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { false, true }, 20, 20);
        Assert.Single(rooms);
        Assert.Equal(new GridPoint(10, 2), rooms[0].Door);
    }

    [Fact]
    public void OutOfBoundsFootprintIsSkipped()
    {
        var doors = Doors(20, 20, new GridPoint(10, 18)); // room would extend past height 20
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);
        Assert.Empty(rooms);
    }

    [Fact]
    public void OverlappingSecondRoomIsSkippedButFirstSucceeds()
    {
        var doors = Doors(20, 20, new GridPoint(10, 2), new GridPoint(10, 3));
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { true, true }, 20, 20);
        Assert.Single(rooms);
        Assert.Equal(new GridPoint(10, 2), rooms[0].Door);
    }

    [Fact]
    public void AllyOccupiedCellBlocksRoom()
    {
        var doorsMap = new bool[20, 20];
        doorsMap[10, 2] = true;
        var doors = new GridLayer(doorsMap);

        var alliesMap = new bool[20, 20];
        alliesMap[10, 5] = true; // sits inside the interior floor
        var allies = new GridLayer(alliesMap);

        var rooms = ShopInteriors.ComputeRooms(doors, allies, new List<bool> { true }, 20, 20);
        Assert.Empty(rooms);
    }

    [Fact]
    public void ComputeRoomsIsDeterministic()
    {
        var doors = Doors(20, 20, new GridPoint(10, 2), new GridPoint(3, 3));
        var flags = new List<bool> { true, true };
        var first = ShopInteriors.ComputeRooms(doors, null, flags, 20, 20);
        var second = ShopInteriors.ComputeRooms(doors, null, flags, 20, 20);
        Assert.Equal(first.Select(r => r.Door), second.Select(r => r.Door));
    }

    [Fact]
    public void TryGetInteriorAnchorSucceedsOnlyWhenFloorCarved()
    {
        var doors = Doors(20, 20, new GridPoint(10, 2));
        var rooms = ShopInteriors.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);

        var floorMap = new bool[20, 20];
        foreach (var cell in rooms[0].Floor) floorMap[cell.X, cell.Y] = true;
        var floor = new GridLayer(floorMap);

        Assert.True(ShopInteriors.TryGetInteriorAnchor(floor, new GridPoint(10, 2), out var anchor));
        Assert.Equal(new GridPoint(10, 5), anchor);

        // A door whose room was never carved (e.g. campaign placement moved the marker) must fail gracefully.
        Assert.False(ShopInteriors.TryGetInteriorAnchor(floor, new GridPoint(2, 2), out _));
    }
}
