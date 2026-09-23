using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TWC;
using TWC.Actions;

/// <summary>Carves a small walkable room behind each shop's door marker, so a shop is a real interior instead of a single interact tile.</summary>
[Serializable]
public sealed class ShopInteriorCarver : TWCBlueprintAction, ITWCAction
{
    public enum Role { Floor, Wall, Clear }

    public Role RoomLayer;
    private readonly List<bool> shopFlags;

    public ShopInteriorCarver() { }
    public ShopInteriorCarver(List<bool> shopFlags) { this.shopFlags = shopFlags; }

    public override bool ShowFoldout => false;
    public float GetGUIHeight() => 0;
    public ITWCAction Clone() => new ShopInteriorCarver(shopFlags) { RoomLayer = RoomLayer };

    public bool[,] Execute(bool[,] map, TileWorldCreator creator)
    {
        var doors = creator.GetMapOutputFromBlueprintLayer("Buildings");
        var allies = creator.GetMapOutputFromBlueprintLayer("Allies");
        foreach (var room in ComputeRooms(doors, allies, shopFlags, map.GetLength(0), map.GetLength(1)))
        {
            IEnumerable<Vector3Int> cells = RoomLayer switch
            {
                Role.Floor => room.Floor,
                Role.Wall => room.Wall,
                _ => room.Floor.Concat(room.Wall)
            };
            foreach (var cell in cells) map[cell.x, cell.y] = RoomLayer != Role.Clear;
        }
        return map;
    }

    public readonly struct Room
    {
        public readonly Vector3Int Door;
        public readonly List<Vector3Int> Floor;
        public readonly List<Vector3Int> Wall;
        public Room(Vector3Int door, List<Vector3Int> floor, List<Vector3Int> wall) { Door = door; Floor = floor; Wall = wall; }
    }

    /// <summary>
    /// One shop room per true "Buildings" marker whose raster index maps to a shop in shopFlags -
    /// same x-outer/y-inner raster order as Town.GetPositions, so index i lines up with configuration.Buildings[i].
    /// The room opens north from the door: a one-tile threshold, a one-tile interior, and the vendor's
    /// tile against the back wall, each flanked by a one-tile-thick wall ring.
    /// </summary>
    public static List<Room> ComputeRooms(bool[,] doors, bool[,] allies, IReadOnlyList<bool> shopFlags, int width, int height)
    {
        var rooms = new List<Room>();
        if (doors == null || shopFlags == null) return rooms;
        var claimed = new HashSet<Vector3Int>();
        int shopIndex = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!doors[x, y]) continue;
                bool isShop = shopIndex < shopFlags.Count && shopFlags[shopIndex];
                shopIndex++;
                if (!isShop) continue;

                var floor = new List<Vector3Int> { new(x, y + 1, 0), new(x, y + 2, 0), new(x, y + 3, 0) };
                var wall = new List<Vector3Int>();
                for (int dy = 1; dy <= 4; dy++)
                    foreach (int dx in dy == 4 ? new[] { -1, 0, 1 } : new[] { -1, 1 })
                        wall.Add(new Vector3Int(x + dx, y + dy, 0));

                bool fits = floor.Concat(wall).All(c => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height &&
                    !claimed.Contains(c) && (allies == null || !allies[c.x, c.y]));
                if (!fits) continue;

                foreach (var c in floor) claimed.Add(c);
                foreach (var c in wall) claimed.Add(c);
                rooms.Add(new Room(new Vector3Int(x, y, 0), floor, wall));
            }
        return rooms;
    }

    /// <summary>The floor cell against the room's back wall, where the vendor stands facing the door.</summary>
    public static Vector3Int InteriorAnchor(Vector3Int door) => new(door.x, door.y + 3, 0);

    /// <summary>False when no room was carved for this door this generation (e.g. campaign placement moved the building) - callers should fall back to the legacy single-tile behavior.</summary>
    public static bool TryGetInteriorAnchor(bool[,] floorMap, Vector3Int door, out Vector3Int anchor)
    {
        anchor = InteriorAnchor(door);
        return floorMap != null && anchor.x >= 0 && anchor.y >= 0 &&
            anchor.x < floorMap.GetLength(0) && anchor.y < floorMap.GetLength(1) && floorMap[anchor.x, anchor.y];
    }

    /// <summary>
    /// Clones the TWC asset (never mutates the shared project asset), moves "Buildings" ahead of
    /// "Houses"/"Trees" so door positions are known when those layers carve themselves clear, appends
    /// the clearing action to their stacks, and adds two new layers ("ShopFloor"/"ShopWalls") that carve
    /// the rooms themselves.
    /// </summary>
    public static void Configure(TileWorldCreator creator, TownConfiguration configuration)
    {
        creator.twcAsset = UnityEngine.Object.Instantiate(creator.twcAsset);
        creator.twcAsset.hideFlags = HideFlags.DontSave;
        var shopFlags = configuration.Buildings.Select(b => b.ShopCatalog.Count > 0).ToList();
        var layers = creator.twcAsset.mapBlueprintLayers;

        var buildings = layers.FirstOrDefault(l => l.layerName == "Buildings");
        var houses = layers.FirstOrDefault(l => l.layerName == "Houses");
        if (buildings != null && houses != null && layers.IndexOf(buildings) > layers.IndexOf(houses))
        {
            layers.Remove(buildings);
            layers.Insert(layers.IndexOf(houses), buildings);
        }

        foreach (var layer in layers.Where(l => l.layerName == "Houses" || l.layerName == "Trees"))
            layer.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack(
                "Clear shop interiors", new ShopInteriorCarver(shopFlags) { RoomLayer = Role.Clear }));

        var floorLayer = new TileWorldCreatorAsset.BlueprintLayerData("ShopFloor", true);
        floorLayer.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack(
            "Carve shop floors", new ShopInteriorCarver(shopFlags) { RoomLayer = Role.Floor }));
        layers.Add(floorLayer);

        var wallLayer = new TileWorldCreatorAsset.BlueprintLayerData("ShopWalls", true);
        wallLayer.stack.Add(new TileWorldCreatorAsset.BlueprintLayerData.ActionStack(
            "Carve shop walls", new ShopInteriorCarver(shopFlags) { RoomLayer = Role.Wall }));
        layers.Add(wallLayer);
    }
}
