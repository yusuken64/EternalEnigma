using System.Collections.ObjectModel;

namespace EternalEnigma.Core.World;

public sealed class TownPlan
{
    public const int GenerationVersion = 1;
    public const int CorridorMinX = 8, CorridorMaxX = 12;

    /// Port of Unity CampaignTownCorridor.IsReserved: the southern entrance corridor that must stay open.
    public static bool IsReservedCorridor(GridPoint cell, int height) =>
        cell.X >= CorridorMinX && cell.X <= CorridorMaxX && cell.Y >= 0 && cell.Y <= height / 2;

    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public IReadOnlyDictionary<string, GridLayer> Layers { get; }
    public IReadOnlyList<GridPoint> BuildingSlots { get; }
    public IReadOnlyList<Placement> AllySlots { get; }
    public IReadOnlyList<ShopRoom> ShopRooms { get; }
    public GridPoint PartySpawn { get; }
    public GridPoint Exit { get; }
    public GridPoint DungeonEntrance { get; }

    internal TownPlan(
        int seed,
        IDictionary<string, GridLayer> layers,
        IEnumerable<GridPoint> buildingSlots,
        IEnumerable<Placement> allySlots,
        IEnumerable<ShopRoom> shopRooms,
        GridPoint partySpawn,
        GridPoint exit,
        GridPoint dungeonEntrance)
    {
        // Validate that Walkable layer exists and get dimensions from it
        if (!layers.ContainsKey(TownLayers.Walkable))
            throw new ArgumentException("Walkable layer must be present in layers.", nameof(layers));

        var walkableLayer = layers[TownLayers.Walkable];
        Width = walkableLayer.Width;
        Height = walkableLayer.Height;

        // Validate that all layer names from TownLayers.All are present
        foreach (var layerName in TownLayers.All)
        {
            if (!layers.ContainsKey(layerName))
                throw new ArgumentException($"Layer '{layerName}' from TownLayers.All must be present in layers.", nameof(layers));
        }

        // Validate that all layers have the same dimensions
        foreach (var layer in layers.Values)
        {
            if (layer.Width != Width || layer.Height != Height)
                throw new ArgumentException("All grid layers must have the same dimensions.", nameof(layers));
        }

        // Validate Walkable == !(Houses || Trees || ShopWalls)
        var housesLayer = layers[TownLayers.Houses];
        var treesLayer = layers[TownLayers.Trees];
        var shopWallsLayer = layers[TownLayers.ShopWalls];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                bool cellWalkable = walkableLayer[x, y];
                bool cellBlocked = housesLayer[x, y] || treesLayer[x, y] || shopWallsLayer[x, y];
                bool expectedWalkable = !cellBlocked;

                if (cellWalkable != expectedWalkable)
                    throw new ArgumentException(
                        $"Walkable layer is not the derived mask of !(Houses | Trees | ShopWalls) at ({x}, {y}).",
                        nameof(layers));
            }
        }

        // Validate and convert building slots to list
        var buildingArray = buildingSlots.ToArray();
        var buildingsLayer = layers[TownLayers.Buildings];
        var expectedBuildingSlots = CollectRasterOrderCells(buildingsLayer);

        if (buildingArray.Length != expectedBuildingSlots.Length)
            throw new ArgumentException(
                $"BuildingSlots count ({buildingArray.Length}) does not match raster-order true cells in Buildings layer ({expectedBuildingSlots.Length}).",
                nameof(buildingSlots));

        for (int i = 0; i < buildingArray.Length; i++)
        {
            if (!buildingArray[i].Equals(expectedBuildingSlots[i]))
                throw new ArgumentException(
                    $"BuildingSlots is not in raster order (x-outer, y-inner). Mismatch at index {i}: expected {expectedBuildingSlots[i]}, got {buildingArray[i]}.",
                    nameof(buildingSlots));
        }

        // Validate and convert ally slots to list
        var allyArray = allySlots.ToArray();
        var alliesLayer = layers[TownLayers.Allies];
        var expectedAllySlots = CollectRasterOrderCells(alliesLayer);

        if (allyArray.Length != expectedAllySlots.Length)
            throw new ArgumentException(
                $"AllySlots count ({allyArray.Length}) does not match raster-order true cells in Allies layer ({expectedAllySlots.Length}).",
                nameof(allySlots));

        for (int i = 0; i < allyArray.Length; i++)
        {
            if (!allyArray[i].Cell.Equals(expectedAllySlots[i]))
                throw new ArgumentException(
                    $"AllySlots cells are not in raster order (x-outer, y-inner). Mismatch at index {i}: expected {expectedAllySlots[i]}, got {allyArray[i].Cell}.",
                    nameof(allySlots));
        }

        // Validate spawn and exit points are in bounds
        var walkableLayerForValidation = layers[TownLayers.Walkable];
        if (!walkableLayerForValidation.Contains(partySpawn))
            throw new ArgumentException("PartySpawn is out of bounds.", nameof(partySpawn));

        if (!walkableLayerForValidation.Contains(exit))
            throw new ArgumentException("Exit is out of bounds.", nameof(exit));

        if (!walkableLayerForValidation.Contains(dungeonEntrance))
            throw new ArgumentException("DungeonEntrance is out of bounds.", nameof(dungeonEntrance));

        // Store properties
        Seed = seed;
        Layers = new ReadOnlyDictionary<string, GridLayer>(new Dictionary<string, GridLayer>(layers, StringComparer.Ordinal));
        BuildingSlots = Array.AsReadOnly(buildingArray);
        AllySlots = Array.AsReadOnly(allyArray);
        ShopRooms = Array.AsReadOnly(shopRooms.ToArray());
        PartySpawn = partySpawn;
        Exit = exit;
        DungeonEntrance = dungeonEntrance;
    }

    public bool Contains(GridPoint cell) => Layers[TownLayers.Walkable].Contains(cell);

    public bool IsWalkable(GridPoint cell) => Layers[TownLayers.Walkable].At(cell);

    public bool CanStep(GridPoint from, GridPoint to) =>
        GridSteps.CanStep(from, to, IsWalkable, DiagonalRule.AllowCornerCutting);

    public IEnumerable<GridPoint> CardinalNeighbors(GridPoint from) =>
        GridSteps.CardinalNeighbors(from, IsWalkable);

    public int? BuildingIndexAt(GridPoint cell)
    {
        for (int i = 0; i < BuildingSlots.Count; i++)
        {
            if (BuildingSlots[i].Equals(cell))
                return i;
        }
        return null;
    }

    public ShopRoom? ShopRoomAt(GridPoint door)
    {
        foreach (var room in ShopRooms)
        {
            if (room.Door.Equals(door))
                return room;
        }
        return null;
    }

    /// True when a room exists for this door and its VendorAnchor is on ShopFloor (same meaning as Unity ShopInteriorCarver.TryGetInteriorAnchor).
    public bool TryGetVendorAnchor(GridPoint door, out GridPoint anchor)
    {
        anchor = default;
        var room = ShopRoomAt(door);
        if (room == null)
            return false;

        var vendorAnchor = room.VendorAnchor;
        var shopFloorLayer = Layers[TownLayers.ShopFloor];

        if (shopFloorLayer.At(vendorAnchor))
        {
            anchor = vendorAnchor;
            return true;
        }

        return false;
    }

    private GridPoint[] CollectRasterOrderCells(GridLayer layer)
    {
        var cells = new List<GridPoint>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (layer[x, y])
                    cells.Add(new GridPoint(x, y));
            }
        }
        return cells.ToArray();
    }
}
