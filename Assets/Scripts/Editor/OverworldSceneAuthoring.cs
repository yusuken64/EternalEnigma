using System;
using System.IO;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EternalEnigma.Core.World;

public static class OverworldSceneAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Launch Overworld Sandbox")]
    public static void LaunchSandbox()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Overworld.unity");
        var map = UnityEngine.Object.FindFirstObjectByType<CampaignOverworld>();
        SessionState.SetInt("EternalEnigma.SandboxSeed", map.Seed);
        EditorApplication.EnterPlaymode();
    }

    private const string Folder = "Assets/Overworld";

    [MenuItem("Tools/Eternal Enigma/Overworld/Apply Biome Floors")]
    public static void ApplyBiomeFloors()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Overworld.unity") throw new InvalidOperationException("Open Overworld first.");
        var map = UnityEngine.Object.FindFirstObjectByType<CampaignOverworld>();
        if (map == null) throw new InvalidOperationException("Set up the Overworld scene first.");
        var renderer = map.GetComponent<OverworldBiomeRenderer>() ?? Undo.AddComponent<OverworldBiomeRenderer>(map.gameObject);
        Color[] colors = { new(.38f, .65f, .28f), new(.85f, .68f, .35f), new(.14f, .46f, .75f), new(.5f, .53f, .58f),
            new(.22f, .43f, .28f), new(.75f, .86f, .9f), new(.4f, .5f, .32f), new(.45f, .28f, .24f) };
        renderer.Biomes = Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().Select(b => new OverworldBiomeMaterial
        { Biome = b, Material = FloorMaterial(b.ToString(), colors[(int)b], b == OverworldBiome.Water) }).ToArray();
        renderer.RoadMaterial = FloorMaterial("Road", new Color(.68f, .58f, .43f), false);
        renderer.BridgeMaterial = FloorMaterial("Bridge", new Color(.67f, .42f, .22f), false);
        renderer.BarrierMaterial = FloorMaterial("Barrier", new Color(.19f, .22f, .24f), false);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Material FloorMaterial(string name, Color color, bool water)
    {
        string path = Folder + "/Biome" + name;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
        if (material != null) return material; // Preserve artist edits on subsequent setup runs.
        var texture = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var random = new System.Random(217 + (int)color.r * 100);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float value = water ? .86f + .1f * Mathf.Sin(y * Mathf.PI / 4 + .5f * Mathf.Sin(x * Mathf.PI / 16)) : .86f + .12f * (float)random.NextDouble();
            texture.SetPixel(x, y, new Color(value, value, value));
        }
        texture.Apply();
        File.WriteAllBytes(path + ".png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path + ".png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", water ? .25f : 0);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", water ? .25f : 0);
        AssetDatabase.CreateAsset(material, path + ".mat");
        return material;
    }

    [MenuItem("Tools/Eternal Enigma/Overworld/Set Up Scene")]
    public static void SetUpScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring the scene.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Overworld.unity") throw new InvalidOperationException("Open the Overworld scene first.");
        if (UnityEngine.Object.FindFirstObjectByType<OverworldScene>() != null) throw new InvalidOperationException("Overworld is already configured.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Overworld");
        var source = AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
        var template = ScriptableObject.CreateInstance<TileWorldCreatorAsset>();
        template.worldName = "Campaign Terrain";
        template.mapWidth = template.mapHeight = 256;
        template.cellSize = source.cellSize;
        template.mapOrientation = source.mapOrientation;
        foreach (string name in new[] { "Parks", "Roads" })
        {
            var blueprint = new TileWorldCreatorAsset.BlueprintLayerData(name, true);
            template.mapBlueprintLayers.Add(blueprint);
            var build = (InstantiateTiles)source.mapBuildLayers.First(l => l.layerName == name).Clone();
            build.assignedGenerationLayerGuid = blueprint.guid;
            build.ignoreLayers.Clear();
            build.globalPositionOffset = name == "Parks" ? new Vector3(0, 0, .04f) : Vector3.zero;
            build.mergeTiles = true;
            build.colliderTypeVariantA = InstantiateTiles.ColliderTypeVariantA.none;
            build.colliderTypeVariantB = InstantiateTiles.ColliderTypeVariantB.none;
            template.mapBuildLayers.Add(build);
        }
        AssetDatabase.CreateAsset(template, Folder + "/CampaignTerrain.asset");
        var root = new GameObject("Campaign Overworld");
        Undo.RegisterCreatedObjectUndo(root, "Set up campaign overworld");
        var creator = root.AddComponent<TileWorldCreator>();
        creator.twcAsset = template;
        var map = root.AddComponent<CampaignOverworld>();
        map.Template = template;
        map.LayerBindings.Add(new CampaignLayerBinding(OverworldLayers.Ground, "Parks"));
        map.LayerBindings.Add(new CampaignLayerBinding(OverworldLayers.Roads, "Roads"));
        var controller = root.AddComponent<OverworldScene>();
        controller.Map = map;
        controller.PlayerPrefab = AssetDatabase.LoadAssetAtPath<TownAlly>("Assets/Prefabs/Town/Allies/Ally_MC03.prefab");
        controller.ViewCamera = Camera.main;
        controller.ViewCamera.orthographic = true;
        controller.ViewCamera.orthographicSize = 13;
        controller.ViewCamera.farClipPlane = 1000;
        controller.ViewCamera.clearFlags = CameraClearFlags.SolidColor;
        controller.ViewCamera.backgroundColor = new Color(.08f, .13f, .16f);
        controller.TownMarker = CreateMarker("Town", PrimitiveType.Cube, new Color(.3f, .85f, .5f), new Vector3(1.1f, 1.1f, 1.4f));
        controller.DungeonMarker = CreateMarker("Dungeon", PrimitiveType.Cylinder, new Color(.85f, .28f, .25f), new Vector3(1.1f, .65f, 1.1f));
        controller.LandmarkMarker = CreateMarker("Landmark", PrimitiveType.Sphere, new Color(1f, .75f, .2f), Vector3.one * .85f);
        controller.GateMarker = CreateMarker("Gate", PrimitiveType.Cube, new Color(.7f, .35f, .9f), new Vector3(.3f, 1.9f, 1.6f));
        var light = new GameObject("Overworld Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(35, -25, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
        var grid = EternalEnigma.Core.Generation.OverworldGridGenerator.Generate(EternalEnigma.Core.Generation.CampaignGenerator.Generate(map.Seed));
        var start = new Vector3(grid.PlayerStart.X + .5f, grid.PlayerStart.Y + .5f, 0) * template.cellSize;
        controller.ViewCamera.transform.position = start + controller.CameraOffset;
        controller.ViewCamera.transform.LookAt(start);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scene.path)) scenes.Add(new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorUtility.SetDirty(template);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ApplyBiomeFloors();
        Selection.activeGameObject = root;
    }

    private static GameObject CreateMarker(string name, PrimitiveType shape, Color color, Vector3 scale)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
        var root = new GameObject(name + " Marker");
        var model = GameObject.CreatePrimitive(shape);
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = new Vector3(0, 0, -.7f);
        model.transform.localScale = scale;
        if (shape == PrimitiveType.Cylinder) model.transform.localRotation = Quaternion.Euler(90, 0, 0);
        model.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }
}
