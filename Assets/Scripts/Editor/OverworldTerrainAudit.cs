#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OverworldTerrainAudit
{
    private const string Output = "Temp/OverworldTerrainAudit";

    [MenuItem("Tools/Eternal Enigma/Art/Audit TWC Terrain Samples")]
    public static void Audit()
    {
        Directory.CreateDirectory(Output);
        var text = new StringBuilder();
        var previous = SceneManager.GetActiveScene();
        const string path = "Assets/TileWorldCreator/Demo/06_CliffIsland/06_CliffIsland.unity";
        if(!File.Exists(path)){Debug.Log("TWC demo assets were removed after migration. The retained audit is Docs/Art/TWCSampleAudit.md.");return;}
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            text.AppendLine("Sample scene: " + path);
            foreach (var creator in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TileWorldCreator>(true)))
                text.AppendLine($"Scene creator {creator.name}: {AssetDatabase.GetAssetPath(creator.twcAsset)}");
            foreach (string assetPath in new[] {
                "Assets/TileWorldCreator/Demo/06_CliffIsland/CliffIslandAsset.asset",
                "Assets/TileWorldCreator/Demo/11_MixTilesets/MixTilesetAsset.asset",
                "Assets/Overworld/CampaignTerrain.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(assetPath);
                text.AppendLine($"\n{assetPath}: orientation={asset.mapOrientation}, cellSize={asset.cellSize}");
                foreach (var layer in asset.mapBlueprintLayers)
                {
                    text.AppendLine($"Blueprint {layer.layerName} [{layer.guid}] active={layer.active}");
                    foreach (var action in layer.stack)
                    {
                        text.Append("  " + action.action.GetType().Name);
                        foreach (var field in action.action.GetType().GetFields())
                            if (field.FieldType.IsPrimitive || field.FieldType == typeof(Guid) || field.FieldType == typeof(string))
                                text.Append($" {field.Name}={field.GetValue(action.action)}");
                        text.AppendLine();
                    }
                }
                foreach (var layer in asset.mapBuildLayers)
                {
                    text.AppendLine($"Build {layer.layerName}: {layer.GetType().Name}, active={layer.active}, source={asset.GetBlueprintLayerData(layer.assignedGenerationLayerGuid)?.layerName}");
                    if (layer is InstantiateTiles four)
                        text.AppendLine($"  Four tiles: {string.Join(",", four.tiles.Select(t => t.preset.name))}; offset={four.globalPositionOffset}");
                    if (layer is Instantiate6Tiles six)
                        text.AppendLine($"  Six tiles: {string.Join(",", six.tiles.Select(t => t.preset.name))}; offset={six.globalPositionOffset}");
                    if (layer is EnvironmentSmartTileLayer smart)
                        text.AppendLine($"  Six={smart.WallTiles?.name}; four={smart.QuarterTiles?.name}; elevation={smart.Elevation}; material={smart.SurfaceMaterial?.name}");
                }
            }
            File.WriteAllText(Output + "/TWC-sample-audit.txt", text.ToString());
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Art/Capture Fixed Overworld Terrain")]
    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        var previous = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/EnvironmentPlayground.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var p = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnvironmentPlayground>(true)).Single();
            p.ShowOverworld();
            p.Rebuild();
            var creator = p.Overworld.GetComponent<TileWorldCreator>();
            var grid = p.Overworld.CurrentGrid;
            var root = new GameObject("Terrain capture group");
            var world = creator.worldObject.transform;
            var floor = p.Overworld.GetComponent<OverworldBiomeRenderer>().RenderedSurfaces.transform;
            var worldParent = world.parent; var floorParent = floor.parent;
            try
            {
                world.SetParent(root.transform, true); floor.SetParent(root.transform, true);
                foreach (string kind in new[] { SmartEnvironmentMasks.Mountains, SmartEnvironmentMasks.Coast })
                {
                    var mask = SmartEnvironmentMasks.World(grid, kind);
                    var candidates = Enumerable.Range(0, grid.Width * grid.Height)
                        .Select(i => new Vector2Int(i % grid.Width, i / grid.Width))
                        .Where(c => c.x > 8 && c.y > 8 && c.x < grid.Width - 8 && c.y < grid.Height - 8 && mask[c.x,c.y] &&
                            (!mask[c.x-1,c.y] || !mask[c.x+1,c.y] || !mask[c.x,c.y-1] || !mask[c.x,c.y+1]));
                    if (kind == SmartEnvironmentMasks.Coast)
                        candidates = candidates.Where(c => Enumerable.Range(-3,7).Sum(dy =>
                            Enumerable.Range(-3,7).Count(dx => mask[c.x+dx,c.y+dy])) >= 20);
                    var cell = candidates.OrderBy(c => (c - new Vector2Int(grid.PlayerStart.X, grid.PlayerStart.Y)).sqrMagnitude).First();
                    var center = new Vector3(cell.x + .5f, cell.y + .5f, 0) * creator.twcAsset.cellSize;
                    TownInteriorPreview.Capture(root, center, 13, Output + (kind == SmartEnvironmentMasks.Mountains ? "/Mountains.png" : "/Water.png"));
                }
            }
            finally
            {
                world.SetParent(worldParent, true); floor.SetParent(floorParent, true);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        Debug.Log("Terrain captures written to " + Output);
    }
}
#endif
