#if UNITY_EDITOR
using System;
using System.Linq;
using TWC;
using UnityEditor;
using UnityEngine;

/// <summary>Authors the six canonical pieces and connects the campaign TWC build layers.</summary>
public static class OverworldSixTerrainAuthoring
{
    private const string Folder = "Assets/Art/EnvironmentKit/SmartTiles/SixTerrain";
    private static readonly string[] Names = { "Single", "End", "Straight", "Corner", "Tee", "Cross" };
    private static readonly int[] Masks = { 0, 1, 5, 9, 13, 15 };

    [MenuItem("Tools/Eternal Enigma/Author Overworld Six Terrain")]
    public static void Author()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art/EnvironmentKit/SmartTiles", "SixTerrain");
        var kit = EnvironmentKit.Load();
        string materialPath = Folder + "/MountainSurface.mat";
        var stone = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (stone == null)
        {
            stone = new Material(kit.Ground(EternalEnigma.Core.World.OverworldBiome.Mountain));
            AssetDatabase.CreateAsset(stone, materialPath);
        }
        stone.SetOverrideTag("EnvironmentProjection", "Box");
        EditorUtility.SetDirty(stone);
        var mountain = Preset("Mountain", false, stone);
        var shore = Preset("Water", true, kit.Shore);
        var template = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        foreach (var layer in template.mapBuildLayers.OfType<EnvironmentSmartTileLayer>())
        {
            if (layer.layerName.StartsWith("Smart/Mountains", StringComparison.Ordinal) || layer.layerName == SmartEnvironmentMasks.Summits)
            {
                layer.WallTiles = mountain;
                layer.QuarterTiles = null;
                layer.SurfaceMaterial = stone;
            }
            else if (layer.layerName == SmartEnvironmentMasks.Coast)
            {
                layer.WallTiles = shore;
                layer.QuarterTiles = null;
                layer.SurfaceMaterial = kit.Shore;
            }
        }
        EditorUtility.SetDirty(template);
        AssetDatabase.SaveAssets();
    }

    private static TileWorldCreator6TilesPreset Preset(string name, bool coast, Material material)
    {
        var pieces = new GameObject[6];
        for (int i = 0; i < pieces.Length; i++)
        {
            string basePath = $"{Folder}/{name}{Names[i]}";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(basePath + ".asset");
            var generated = EnvironmentSmartTileLayer.SixTerrainMesh(Masks[i], 15, coast, .42f);
            generated.hideFlags = HideFlags.None;
            generated.name = name + Names[i];
            if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, basePath + ".asset"); }
            else { EditorUtility.CopySerialized(generated, mesh); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            var go = new GameObject(name + Names[i]);
            try
            {
                var visual = new GameObject("Terrain mesh");
                visual.transform.SetParent(go.transform, false);
                // TWC presets are authored on XZ; our batched renderer consumes
                // the child mesh in its original XY coordinates.
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pieces[i] = PrefabUtility.SaveAsPrefabAsset(go, basePath + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        string path = $"{Folder}/{name}SixTerrain.asset";
        var preset = AssetDatabase.LoadAssetAtPath<TileWorldCreator6TilesPreset>(path);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<TileWorldCreator6TilesPreset>();
            AssetDatabase.CreateAsset(preset, path);
        }
        preset.singleTile = pieces[0];
        preset.deadEndTile = pieces[1];
        preset.straightTile = pieces[2];
        preset.cornerTile = pieces[3];
        preset.threeWayTile = pieces[4];
        preset.fourWayTile = pieces[5];
        EditorUtility.SetDirty(preset);
        return preset;
    }
}
#endif
