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
    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[] { Roads, Houses, Trees, Parks, Roofs, Buildings, Allies, Dungeon, ShopFloor, ShopWalls, Walkable });
}
