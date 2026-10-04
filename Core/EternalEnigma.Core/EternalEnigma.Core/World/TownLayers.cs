namespace EternalEnigma.Core.World;

/// <summary>Walkable == !(Houses | Trees | ShopWalls)</summary>
public static class TownLayers
{
    public const string Roads = "Roads";
    public const string Houses = "Houses";
    public const string Trees = "Trees";
    public const string Parks = "Parks";
    public const string Roofs = "Roofs";
    public const string Buildings = "Buildings";
    public const string Allies = "Allies";
    public const string Dungeon = "Dungeon";
    public const string ShopFloor = "ShopFloor";
    public const string ShopWalls = "ShopWalls";
    public const string Walkable = "Walkable";
    public const string Furniture = "Furniture", Carpet = "Carpet", Counters = "Counters";
    public static readonly IReadOnlyList<string> InteriorLayers = Array.AsReadOnly(new[] { Furniture, Carpet, Counters });
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { Roads, Houses, Trees, Parks, Roofs, Buildings, Allies, Dungeon, ShopFloor, ShopWalls, Walkable });

    // Layers of detailed towns. They refine the layers above and are empty otherwise, so they are not in All.
    /// <summary>Wide main roads (subset of Roads).</summary>
    public const string MainRoads = "MainRoads";
    /// <summary>Single-cell back alleys behind buildings, and the links that join them to the network (subset of Roads).</summary>
    public const string Alleys = "Alleys";
    /// <summary>Non-blocking decoration cells for the town biome; never on roads, buildings, doors or the entrance corridor.</summary>
    public const string Props = "Props";
    public static readonly IReadOnlyList<string> Detail = Array.AsReadOnly(new[] { MainRoads, Alleys, Props });
}
