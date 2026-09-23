using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class ShopInteriorCarverTests
    {
        private static bool[,] Doors(int width, int height, params Vector3Int[] markers)
        {
            var map = new bool[width, height];
            foreach (var m in markers) map[m.x, m.y] = true;
            return map;
        }

        [Test]
        public void ShopMarkerCarvesFloorAndWallRing()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0));
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);

            Assert.That(rooms.Count, Is.EqualTo(1));
            var room = rooms[0];
            Assert.That(room.Door, Is.EqualTo(new Vector3Int(10, 2, 0)));
            Assert.That(room.Floor, Is.EquivalentTo(new[]
            {
                new Vector3Int(10, 3, 0), new Vector3Int(10, 4, 0), new Vector3Int(10, 5, 0)
            }));
            Assert.That(room.Wall, Is.EquivalentTo(new[]
            {
                new Vector3Int(9, 3, 0), new Vector3Int(11, 3, 0),
                new Vector3Int(9, 4, 0), new Vector3Int(11, 4, 0),
                new Vector3Int(9, 5, 0), new Vector3Int(11, 5, 0),
                new Vector3Int(9, 6, 0), new Vector3Int(10, 6, 0), new Vector3Int(11, 6, 0)
            }));
            Assert.That(room.Floor.Intersect(room.Wall), Is.Empty);
        }

        [Test]
        public void NonShopMarkerGetsNoRoom()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0));
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { false }, 20, 20);
            Assert.That(rooms, Is.Empty);
        }

        [Test]
        public void RaisterOrderMatchesShopFlagsIndex()
        {
            // Town.GetPositions raster-scans x-outer/y-inner, so the marker at the lower x comes first.
            var doors = Doors(20, 20, new Vector3Int(5, 2, 0), new Vector3Int(10, 2, 0));
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { false, true }, 20, 20);
            Assert.That(rooms.Count, Is.EqualTo(1));
            Assert.That(rooms[0].Door, Is.EqualTo(new Vector3Int(10, 2, 0)));
        }

        [Test]
        public void OutOfBoundsFootprintIsSkipped()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 18, 0)); // room would extend past height 20
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);
            Assert.That(rooms, Is.Empty);
        }

        [Test]
        public void OverlappingSecondRoomIsSkippedButFirstSucceeds()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0), new Vector3Int(10, 3, 0));
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { true, true }, 20, 20);
            Assert.That(rooms.Count, Is.EqualTo(1));
            Assert.That(rooms[0].Door, Is.EqualTo(new Vector3Int(10, 2, 0)));
        }

        [Test]
        public void AllyOccupiedCellBlocksRoom()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0));
            var allies = new bool[20, 20];
            allies[10, 5] = true; // sits inside the interior floor
            var rooms = ShopInteriorCarver.ComputeRooms(doors, allies, new List<bool> { true }, 20, 20);
            Assert.That(rooms, Is.Empty);
        }

        [Test]
        public void ComputeRoomsIsDeterministic()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0), new Vector3Int(3, 3, 0));
            var flags = new List<bool> { true, true };
            var first = ShopInteriorCarver.ComputeRooms(doors, null, flags, 20, 20);
            var second = ShopInteriorCarver.ComputeRooms(doors, null, flags, 20, 20);
            Assert.That(first.Select(r => r.Door), Is.EqualTo(second.Select(r => r.Door)));
        }

        [Test]
        public void TryGetInteriorAnchorSucceedsOnlyWhenFloorCarved()
        {
            var doors = Doors(20, 20, new Vector3Int(10, 2, 0));
            var rooms = ShopInteriorCarver.ComputeRooms(doors, null, new List<bool> { true }, 20, 20);
            var floorMap = new bool[20, 20];
            foreach (var cell in rooms[0].Floor) floorMap[cell.x, cell.y] = true;

            Assert.That(ShopInteriorCarver.TryGetInteriorAnchor(floorMap, new Vector3Int(10, 2, 0), out var anchor), Is.True);
            Assert.That(anchor, Is.EqualTo(new Vector3Int(10, 5, 0)));

            // A door whose room was never carved (e.g. campaign placement moved the marker) must fail gracefully.
            Assert.That(ShopInteriorCarver.TryGetInteriorAnchor(floorMap, new Vector3Int(2, 2, 0), out _), Is.False);
        }
    }
}
