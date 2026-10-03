using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Native TWC rule selection, combined directly into chunks instead of temporary tile GameObjects.</summary>
[Serializable, ActionName(Name = "Biome smart tiles (batched)")]
public sealed class EnvironmentSmartTileLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public TileWorldCreator4TilesPreset QuarterTiles;
    public TileWorldCreator6TilesPreset WallTiles;
    public bool Road;
    public bool Buildings;
    [Tooltip("Height above ground, in cells. XY worlds raise geometry toward negative Z.")]
    public float Elevation;
    public float HeightScale = 1;
    public bool PerimeterOnly;
    public bool SkipMapBoundary;
    public Material SurfaceMaterial;
    public override TWCBuildLayer Clone() => new EnvironmentSmartTileLayer { guid = guid, layerName = layerName,
        assignedGenerationLayerGuid = assignedGenerationLayerGuid, active = active, Kit = Kit,
        QuarterTiles = QuarterTiles, WallTiles = WallTiles, Road = Road, Buildings = Buildings, Elevation = Elevation, HeightScale = HeightScale,
        PerimeterOnly=PerimeterOnly,SkipMapBoundary=SkipMapBoundary,SurfaceMaterial=SurfaceMaterial };

    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            var map = creator.GetGeneratedBlueprintMap(assignedGenerationLayerGuid + (WallTiles != null ? "_UNSUBD" : ""));
            if (map == null || Kit == null) return;
            var root = creator.AddLayerObject(layerName, guid);
            root.transform.SetParent(creator.worldObject.transform, false); root.transform.localRotation = Quaternion.identity;
            foreach (var child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
            foreach (var old in root.GetComponents<EnvironmentMeshOwner>()) Release(old);
            var batch = new EnvironmentBatch(root.transform);
            var decorationFaces=root.GetComponent<BiomeDecorationSurfaceSet>() ?? root.AddComponent<BiomeDecorationSurfaceSet>();
            decorationFaces.Faces.Clear();
            var grid = creator.GetComponent<CampaignOverworld>()?.CurrentGrid;
            CoreLayoutCache.TryGetTown(creator, out var town);
            var biome = creator.GetComponent<TownBiomeStyle>()?.Current ?? OverworldBiome.Grassland;
            float size = creator.twcAsset.cellSize;
            foreach (var tile in map.clusters.Values.SelectMany(c => c.Values))
            {
                if(PerimeterOnly && tile.position.x!=0 && tile.position.z!=0 && tile.position.x!=creator.twcAsset.mapWidth*2-1 && tile.position.z!=creator.twcAsset.mapHeight*2-1) continue;
                if(SkipMapBoundary && (tile.position.x==0 || tile.position.z==0 || tile.position.x==creator.twcAsset.mapWidth*2-1 || tile.position.z==creator.twcAsset.mapHeight*2-1)) continue;
                bool wall = WallTiles != null;
                float unit = wall ? size : size * .5f;
                int x = (int)tile.position.x / (wall ? 1 : 2), y = (int)tile.position.z / (wall ? 1 : 2);
                var palette = grid == null ? biome : OverworldCosmetics.Biome(grid, x, y);
                GameObject prefab; float angle;
                if (wall) (prefab, angle) = SelectWall(WallTiles, tile.neighboursLocation);
                else
                {
                    prefab = tile.tileType switch { TileData.TileType.edge => QuarterTiles.edgeTile,
                        TileData.TileType.exteriorCorner => QuarterTiles.exteriorCornerTile,
                        TileData.TileType.interiorCorner => QuarterTiles.interiorCornerTile, _ => QuarterTiles.fillTile };
                    angle = -tile.yRotation;
                }
                if (prefab == null) continue; // Edge-only presets deliberately omit their interior fill.
                // Authored preset child rotates the XY mesh into TWC's canonical XZ plane.
                var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                bool alley = Road && town != null && town.Layers.TryGetValue(TownLayers.Alleys, out var alleys) && alleys[x,y];
                if(!Road && (wall || tile.tileType==TileData.TileType.edge) && (Buildings || wall || mesh.bounds.size.z*HeightScale>.25f))
                    decorationFaces.Add(mesh,Matrix4x4.TRS(new Vector3((tile.position.x+.5f)*unit,(tile.position.z+.5f)*unit,-Elevation*size),Quaternion.Euler(0,0,angle),new Vector3(unit,unit,unit*HeightScale)),palette,
                        Buildings?DecorationSurface.Facade:wall?DecorationSurface.BuiltWall:DecorationSurface.NaturalWall,size,Buildings?(town?.Footprints.FirstOrDefault(f=>f.Cells.Contains(new GridPoint(x,y)))?.Door.ToString()??x+","+y):null);
                batch.Add(mesh, SurfaceMaterial != null ? SurfaceMaterial : Road ? (alley ? Kit.Paving : Kit.Road) : Buildings ? Kit.BuildingMaterial(palette) : Kit.Material(palette),
                    new Vector3((tile.position.x + .5f) * unit, (tile.position.z + .5f) * unit, -Elevation * size),
                    new Vector3(unit, unit, unit * HeightScale), angle,
                    Road ? SilhouetteRole.Receiver : Buildings || wall ? SilhouetteRole.Caster : (SilhouetteRole?)null);
            }
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount += 1; }
    }

    // Same canonical orientations as TWC Instantiate6Tiles: end=N, corner=N+W, tee=N+S+W.
    public static (GameObject prefab, float angle) SelectWall(TileWorldCreator6TilesPreset set, NeighboursLocation n)
    {
        int mask = (n.north ? 1 : 0) | (n.east ? 2 : 0) | (n.south ? 4 : 0) | (n.west ? 8 : 0);
        if (mask == 0) return (set.singleTile, 0);
        if (mask == 15) return (set.fourWayTile, 0);
        int[] canonical = { 1, 5, 9, 13 };
        GameObject[] pieces = { set.deadEndTile, set.straightTile, set.cornerTile, set.threeWayTile };
        for (int i = 0; i < canonical.Length; i++)
        for (int turns = 0; turns < 4; turns++)
        {
            int rotated = ((canonical[i] << turns) | (canonical[i] >> (4 - turns))) & 15;
            if (rotated == mask) return (pieces[i], -90 * turns);
        }
        throw new InvalidOperationException("Unmatched wall neighborhood.");
    }
    private static void Release(UnityEngine.Object obj)
    {
        if (obj is GameObject go) go.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)
    {
        layerName = EditorGUILayout.TextField("Layer name", layerName);
        var layers = asset.mapBlueprintLayers;
        int selected = layers.FindIndex(l => l.guid == assignedGenerationLayerGuid);
        int next = EditorGUILayout.Popup("Blueprint", Math.Max(0, selected), layers.Select(l => l.layerName).ToArray());
        if (layers.Count > 0) assignedGenerationLayerGuid = layers[next].guid;
        Kit = (EnvironmentKit)EditorGUILayout.ObjectField("Biome kit", Kit, typeof(EnvironmentKit), false);
        QuarterTiles = (TileWorldCreator4TilesPreset)EditorGUILayout.ObjectField("4-tile preset", QuarterTiles, typeof(TileWorldCreator4TilesPreset), false);
        WallTiles = (TileWorldCreator6TilesPreset)EditorGUILayout.ObjectField("6-tile wall preset", WallTiles, typeof(TileWorldCreator6TilesPreset), false);
        Road = EditorGUILayout.Toggle("Paving material", Road);
        Buildings = EditorGUILayout.Toggle("Detailed building material", Buildings);
        Elevation = EditorGUILayout.FloatField("Elevation (cells)", Elevation);
        HeightScale = EditorGUILayout.FloatField("Height scale", HeightScale);
        PerimeterOnly=EditorGUILayout.Toggle("Outer map boundary only",PerimeterOnly);
        SkipMapBoundary=EditorGUILayout.Toggle("Open sea at map boundary",SkipMapBoundary);
        SurfaceMaterial=(Material)EditorGUILayout.ObjectField("Surface override",SurfaceMaterial,typeof(Material),false);
    }
#endif
}
