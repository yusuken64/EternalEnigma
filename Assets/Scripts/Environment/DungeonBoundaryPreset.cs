using TWC;
using UnityEngine;

/// <summary>XY/-Z whole-cell wall kit. Canonical directions are N, NS, NE, NES, NESW.</summary>
[CreateAssetMenu(menuName = "Game/Environment/Dungeon Smart Boundary")]
public sealed class DungeonBoundaryPreset : ScriptableObject
{
    public TileWorldCreator6TilesPreset Tiles;
    public GameObject[] StraightVariants = System.Array.Empty<GameObject>();
    public GameObject SolidFill, QuadrantFill, ConcaveCorner;
    [Tooltip("Optional dungeon wall material. Keeps shared kit meshes while using the theme's texture, tint and UV projection.")]
    public Material MaterialOverride;
    public float AuthoredCellSize = 2;
    public float AuthoredHeight = 4.234727f;
}
