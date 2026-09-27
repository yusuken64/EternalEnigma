using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class EnvironmentArtInstaller
{
    private const string Art = "Assets/Art/EnvironmentKit";
    private const string KitPath = "Assets/Resources/EnvironmentKit/Kit.asset";
    private const string PlaygroundPath = "Assets/Scenes/EnvironmentPlayground.unity";
    private static T Asset<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);

    [MenuItem("Tools/Eternal Enigma/Art/Import and Integrate Environment Kit")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before integrating assets.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(Art + "/Meshes"); Directory.CreateDirectory(Art + "/Materials"); Directory.CreateDirectory(Art + "/Prefabs");
        AssetDatabase.Refresh();
        var kit = Asset<EnvironmentKit>(KitPath);
        if (kit == null) { kit = ScriptableObject.CreateInstance<EnvironmentKit>(); AssetDatabase.CreateAsset(kit, KitPath); }
        var models = new List<EnvironmentModel>();
        var report = new StringBuilder("# Imported environment kit\n\nBlender-generated assets; Unity mesh counts after import. XY ground, negative-Z height, base-centered pivot. Static, no bones, no colliders. Palette UVs; shared materials. CPU read access is retained for TWC chunk combining.\n\n| Model | Triangles | Vertices | Unity bounds |\n|---|---:|---:|---|\n");
        foreach (string path in Directory.GetFiles(Art + "/Models", "*.fbx").OrderBy(p => p))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var model = Asset<GameObject>(path); string id = Path.GetFileNameWithoutExtension(path);
            var source = model.GetComponentsInChildren<MeshFilter>();
            var combined = new Mesh { name = id };
            combined.CombineMeshes(source.Select(f => new CombineInstance { mesh = f.sharedMesh,
                transform = Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0)) * f.transform.localToWorldMatrix }).ToArray(), true, true, false);
            combined.RecalculateBounds();
            // Preserve mesh asset GUIDs on a repeated Blender import.
            string meshPath = Art + "/Meshes/" + id + ".asset";
            var mesh = Asset<Mesh>(meshPath);
            if (mesh == null) { mesh = combined; AssetDatabase.CreateAsset(mesh, meshPath); }
            else { EditorUtility.CopySerialized(combined, mesh); UnityEngine.Object.DestroyImmediate(combined); }
            int triangles = (int)mesh.GetIndexCount(0) / 3;
            if (mesh.bounds.size.x > 3.01f || mesh.bounds.size.z > 3.01f || mesh.bounds.max.z > .1f)
                throw new InvalidOperationException($"Unexpected model import orientation/scale: {id} {mesh.bounds}");
            models.Add(new EnvironmentModel { Id = id, Mesh = mesh, Triangles = triangles });
            report.AppendLine($"| {id} | {triangles} | {mesh.vertexCount} | {mesh.bounds.size} |");
        }
        kit.Models = models.ToArray();
        var palettes = new List<EnvironmentPalette>();
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
        {
            string path = Art + "/Textures/Palette_" + biome + ".png";
            ConfigureTexture(path, false);
            var material = MakeMaterial(Art + "/Materials/" + biome + ".mat", Asset<Texture2D>(path));
            string buildings = Art + "/Textures/Buildings_" + biome + ".png";
            ConfigureTexture(buildings,true,256);
            palettes.Add(new EnvironmentPalette { Biome = biome, Props = material,
                Buildings = MakeMaterial(Art + "/Materials/Buildings_" + biome + ".mat",Asset<Texture2D>(buildings)),
                Ground = Asset<Material>("Assets/Overworld/Biome" + biome + ".mat") });
        }
        ConfigureTexture(Art + "/Textures/MedievalPaving.png", true);
        ConfigureTexture(Art + "/Textures/Ocean.png", true);
        ConfigureTexture(Art + "/Textures/OceanNoise.png", true);
        ConfigureTexture(Art + "/Textures/SmartRoad.png", true);
        kit.Paving = MakeMaterial(Art + "/Materials/MedievalPaving.mat", Asset<Texture2D>(Art + "/Textures/MedievalPaving.png"));
        kit.Road = MakeMaterial(Art + "/Materials/SmartRoad.mat", Asset<Texture2D>(Art + "/Textures/SmartRoad.png"));
        kit.Shore=Asset<Material>(Art+"/Materials/Shoreline.mat");
        if(kit.Shore==null) {kit.Shore=new Material(Shader.Find("EternalEnigma/Smart Shoreline"));AssetDatabase.CreateAsset(kit.Shore,Art+"/Materials/Shoreline.mat");}
        kit.Shore.mainTexture=Asset<Texture2D>(Art+"/Textures/Ocean.png");kit.Shore.SetTexture("_NoiseTex",Asset<Texture2D>(Art+"/Textures/OceanNoise.png"));EditorUtility.SetDirty(kit.Shore);
        kit.Palettes = palettes.ToArray(); EditorUtility.SetDirty(kit);
        foreach (var palette in kit.Palettes)
        {
            Directory.CreateDirectory(Art + "/Prefabs/" + palette.Biome); AssetDatabase.Refresh();
            foreach (var model in kit.Models)
            {
                var root = kit.Create(model.Id, palette.Biome, null, Vector3.zero);
                var skin = root.AddComponent<BiomeModel>(); skin.Kit = kit; skin.ModelId = model.Id;
                PrefabUtility.SaveAsPrefabAsset(root, Art + "/Prefabs/" + palette.Biome + "/" + model.Id + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        kit.TreeModels=InstallTreePicker();EditorUtility.SetDirty(kit);
        PatchPrefab("Assets/Overworld/Gate.prefab", "Gate", kit, Vector3.zero, 1.8f);
        PatchPrefab("Assets/Overworld/Dungeon.prefab", "DungeonPortal", kit, Vector3.zero, 1.7f);
        PatchPrefab("Assets/Overworld/Landmark.prefab", "Shrine", kit, Vector3.zero, 1.5f);
        PatchPrefab("Assets/Overworld/Town.prefab", "House", kit, Vector3.zero, 1.5f);
        foreach (var pair in new[] { ("Shop", "Shop"), ("Ballista", "Trainer"), ("Statue", "Shrine"), ("Entrance", "DungeonPortal") })
            PatchPrefab("Assets/Prefabs/Town/" + pair.Item1 + ".prefab", pair.Item2, kit, new Vector3(1, 1, 0), 1.6f);
        var worldAsset = Asset<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        var decor = worldAsset.mapBlueprintLayers.FirstOrDefault(l => l.layerName == OverworldCosmetics.Layer);
        if (decor == null) { decor = new TileWorldCreatorAsset.BlueprintLayerData(OverworldCosmetics.Layer, true); worldAsset.mapBlueprintLayers.Add(decor); }
        worldAsset.mapBuildLayers.RemoveAll(l => l is OverworldCosmeticLayer);
        worldAsset.mapBuildLayers.Add(new OverworldCosmeticLayer { guid = Guid.NewGuid(), assignedGenerationLayerGuid = decor.guid, layerName = OverworldCosmetics.Layer, Kit = kit, TreeModels=kit.TreeModels });
        EditorUtility.SetDirty(worldAsset);
        var townAsset = Asset<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
        foreach (var layer in townAsset.mapBuildLayers) layer.active = false;
        townAsset.mapBuildLayers.RemoveAll(l => l is TownEnvironmentLayer);
        townAsset.mapBuildLayers.Add(new TownEnvironmentLayer { guid = Guid.NewGuid(), assignedGenerationLayerGuid = townAsset.mapBlueprintLayers.First(l => l.layerName == "Houses").guid,
            layerName = "Medieval streets, houses and parks", Kit = kit, TreeModels=kit.TreeModels });
        EnvironmentSmartTileInstaller.Install(kit, worldAsset, townAsset);
        EditorUtility.SetDirty(townAsset); AssetDatabase.SaveAssets();
        try
        {
            var worldScene = EditorSceneManager.OpenScene("Assets/Scenes/Overworld.unity", OpenSceneMode.Single);
            var map = UnityEngine.Object.FindFirstObjectByType<CampaignOverworld>(); map.CosmeticKit = kit;
            map.GetComponent<OverworldBiomeRenderer>().RoadMaterial = kit.Paving;
            foreach(var biome in map.GetComponent<OverworldBiomeRenderer>().Biomes)
                if(biome.Biome==OverworldBiome.Water) biome.Material=Asset<Material>(Art+"/Materials/Ocean.mat");
            EditorSceneManager.SaveScene(worldScene);
            var townScene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
            var creator = UnityEngine.Object.FindFirstObjectByType<WalkableMap>().TileWorldCreator;
            var style = creator.GetComponent<TownBiomeStyle>() ?? creator.gameObject.AddComponent<TownBiomeStyle>(); style.Kit = kit;
            EnvironmentTownPreview.Bake(creator,townAsset);
            EditorSceneManager.SaveScene(townScene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        File.WriteAllText("Docs/Art/EnvironmentKitSpecs.md", report.ToString());
        BuildPlayground();
        Debug.Log("Environment kit imported, integrated and playground created.");
    }

    private static TreeModelPicker InstallTreePicker()
    {
        const string path=Art+"/TreeModels.asset";
        var picker=Asset<TreeModelPicker>(path);
        if(picker!=null)return picker;
        picker=ScriptableObject.CreateInstance<TreeModelPicker>();
        var choices=new List<TreeModelChoice>();
        void Add(string model,int weight,params OverworldBiome[] biomes) => choices.Add(new TreeModelChoice {
            Prefab=Asset<GameObject>(Art+"/Prefabs/Grassland/"+model+".prefab"),Weight=weight,Biomes=biomes });
        var temperate=new[]{OverworldBiome.Grassland,OverworldBiome.Forest};
        Add("Tree",3,temperate);Add("TreeTall",2,temperate);Add("TreeWide",2,temperate);Add("TreePair",3,temperate);Add("TreeGrove",2,temperate);
        var alpine=new[]{OverworldBiome.Mountain};
        Add("Pine",4,alpine);Add("PinePair",3,alpine);Add("PineGrove",2,alpine);
        Add("SnowPine",4,OverworldBiome.Tundra);Add("SnowPinePair",3,OverworldBiome.Tundra);Add("SnowPineGrove",2,OverworldBiome.Tundra);
        Add("Cactus",3,OverworldBiome.Desert);Add("CactusPair",2,OverworldBiome.Desert);Add("Palm",2,OverworldBiome.Desert);Add("PalmPair",2,OverworldBiome.Desert);
        Add("Willow",3,OverworldBiome.Water);Add("WillowPair",2,OverworldBiome.Water);
        Add("DeadTree",1,OverworldBiome.Marsh);Add("DeadTreePair",1,OverworldBiome.Marsh);Add("Mangrove",3,OverworldBiome.Marsh);Add("MangrovePair",2,OverworldBiome.Marsh);
        Add("CharredTree",3,OverworldBiome.Volcanic);Add("CharredTreePair",2,OverworldBiome.Volcanic);
        picker.Models=choices.ToArray();AssetDatabase.CreateAsset(picker,path);return picker;
    }
    private static void ConfigureTexture(string path, bool paving, int maxSize=128)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = paving;
        importer.filterMode = paving ? FilterMode.Bilinear : FilterMode.Point;
        importer.wrapMode = paving ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();
    }
    private static Material MakeMaterial(string path, Texture2D texture)
    {
        var mat = Asset<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = Color.white; mat.mainTexture = texture; mat.SetFloat("_Glossiness", 0); mat.SetFloat("_Metallic", 0);
        mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
    }
    private static void PatchPrefab(string path, string id, EnvironmentKit kit, Vector3 position, float size)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var previous = root.transform.Find("Environment art"); if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            // Gameplay pivots and scripts stay on the original prefab root.
            root.transform.localScale = Vector3.one; root.transform.localRotation = Quaternion.identity;
            var obj = kit.Create(id, OverworldBiome.Grassland, root.transform, position, size); obj.name = "Environment art";
            var skin = obj.AddComponent<BiomeModel>(); skin.Kit = kit; skin.ModelId = id;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Eternal Enigma/Art/Create Environment Playground")]
    public static void BuildPlayground()
    {
        var kit = Asset<EnvironmentKit>(KitPath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Environment Playground"); var playground = root.AddComponent<EnvironmentPlayground>();
        playground.Kit = kit; playground.TownConfiguration = TownSceneLoader.Default;
        playground.Gallery = new GameObject("Asset Gallery - all models and biome variants").transform;
        for (int i = 0; i < kit.Models.Length; i++)
        {
            var model = kit.Models[i]; var position = new Vector3(i % 5 * 3, i / 5 * 4, 0);
            var prefab = Asset<GameObject>(Art + "/Prefabs/Grassland/" + model.Id + ".prefab");
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, playground.Gallery); obj.transform.localPosition = position;
            obj.transform.localScale = Vector3.one * 1.8f;
            Label(playground.Gallery, $"{model.Id}\n{model.Triangles} triangles", position + new Vector3(0, -1.2f, -.1f), .16f);
        }
        int row = 0;
        foreach (var palette in kit.Palettes)
        {
            var position = new Vector3(19, row++ * 3.1f, 0);
            Label(playground.Gallery, palette.Biome.ToString(), position + new Vector3(2, -1.2f, -.1f), .18f);
            foreach (var pair in new[] { ("House",0), ("Inn",2), ("Shop",4), ("Mountain",6) })
            {
                var prefab = Asset<GameObject>(Art + "/Prefabs/" + palette.Biome + "/" + pair.Item1 + ".prefab");
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, playground.Gallery); obj.transform.localPosition = position + Vector3.right * pair.Item2;
                obj.transform.localScale = Vector3.one * 1.5f;
            }
            var trees=kit.TreeModels.Models.Where(c=>c.Matches(palette.Biome)).ToArray();
            var single=trees.First(c=>!c.Prefab.name.EndsWith("Pair")&&!c.Prefab.name.EndsWith("Grove"));
            var group=trees.First(c=>c.Prefab.name.EndsWith("Pair"));
            int treeColumn=9;
            foreach(var tree in new[]{single,group})
            {
                var prefab=Asset<GameObject>(Art+"/Prefabs/"+palette.Biome+"/"+tree.Prefab.name+".prefab");
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,playground.Gallery);
                obj.transform.localPosition=position+Vector3.right*treeColumn;obj.transform.localScale=Vector3.one*1.8f;treeColumn+=2;
            }
        }
        var worldRoot = new GameObject("Overworld TWC - production template");
        var wc = worldRoot.AddComponent<TileWorldCreator>(); wc.twcAsset = Asset<TileWorldCreatorAsset>("Assets/Overworld/CampaignTerrain.asset");
        var map = worldRoot.AddComponent<CampaignOverworld>(); map.Template = wc.twcAsset; map.CosmeticKit = kit;
        map.LayerBindings = new List<CampaignLayerBinding> { new(OverworldLayers.Ground, "Parks"), new(OverworldLayers.Roads, "Roads") };
        var floors = worldRoot.AddComponent<OverworldBiomeRenderer>();
        floors.Biomes = kit.Palettes.Select(p => new OverworldBiomeMaterial { Biome = p.Biome, Material = p.Biome==OverworldBiome.Water?Asset<Material>(Art+"/Materials/Ocean.mat"):p.Ground }).ToArray();
        floors.RoadMaterial = kit.Paving; floors.BridgeMaterial = Asset<Material>("Assets/Overworld/BiomeBridge.mat"); floors.BarrierMaterial = Asset<Material>("Assets/Overworld/BiomeBarrier.mat");
        playground.Overworld = map;
        var townRoot = new GameObject("Town TWC - production template");
        playground.TownCreator = townRoot.AddComponent<TileWorldCreator>();
        playground.TownTemplate = Asset<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset");
        playground.TownCreator.twcAsset = playground.TownTemplate;
        var townStyle = townRoot.AddComponent<TownBiomeStyle>(); townStyle.Kit = kit; townStyle.OverrideBiome = true;
        var ruleRoot = new GameObject("Smart tile rule laboratory TWC");
        var ruleCreator = ruleRoot.AddComponent<TileWorldCreator>();
        ruleCreator.twcAsset = Asset<TileWorldCreatorAsset>(EnvironmentSmartTileInstaller.ExamplePath);
        playground.RulePreview = ruleRoot.AddComponent<SmartRulePreview>(); playground.RulePreview.Template = ruleCreator.twcAsset;
        playground.RuleLabels = new GameObject("Rule example labels");
        Label(playground.RuleLabels.transform,"THREE CLIFF TIERS",new Vector3(13,0,-.1f),.28f);
        Label(playground.RuleLabels.transform,"ROAD JOINS",new Vector3(47,0,-.1f),.28f);
        Label(playground.RuleLabels.transform,"HOUSE CORNERS & COURTYARD",new Vector3(13,28,-.1f),.25f);
        Label(playground.RuleLabels.transform,"WALL ENDS & JUNCTIONS",new Vector3(47,25,-.1f),.25f);
        var cameraObject = new GameObject("Playground Camera", typeof(Camera), typeof(AudioListener));
        var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.17f,.18f);
        camera.farClipPlane = 2000; playground.ViewCamera = camera;
        var lightObject = new GameObject("Soft daylight", typeof(Light));
        var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(30, -30, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f,.69f,.72f);
        var canvas = GameUISkin.Canvas("Playground controls", null, 10);
        var panel = GameUISkin.Panel(canvas.transform, new Vector2(0,.90f), Vector2.one);
        playground.Status = GameUISkin.Label(panel.transform,"",new Vector2(.01f,.06f),new Vector2(.98f,.42f),20);
        void Button(string label,int i,UnityEngine.Events.UnityAction action)
        {
            var button = GameUISkin.Button(panel.transform,label,new Vector2(.01f+i*.14f,.48f),new Vector2(.14f+i*.14f,.96f),null);
            button.onClick.RemoveAllListeners(); UnityEventTools.AddPersistentListener(button.onClick,action);
        }
        Button("Asset gallery",0,playground.ShowGallery); Button("Overworld",1,playground.ShowOverworld); Button("Town",2,playground.ShowTown);
        Button("Next biome",3,playground.NextBiome); Button("Next seed",4,playground.NextSeed); Button("World overview",5,playground.WorldOverview);
        Button("Smart tile rules",6,playground.ShowRules);
        new GameObject("EventSystem",typeof(EventSystem),typeof(MenuUIInputModule));
        // Rebind after all serialized references exist; ExecuteAlways was enabled when added.
        playground.enabled = false; playground.enabled = true; playground.ShowGallery();
        EditorSceneManager.SaveScene(scene, PlaygroundPath);
        Selection.activeGameObject = root;
    }
    private static void Label(Transform parent, string text, Vector3 position, float scale)
    {
        var obj = new GameObject(text); obj.transform.SetParent(parent,false); obj.transform.localPosition = position;
        var label = obj.AddComponent<TextMesh>(); label.text = text; label.characterSize = scale; label.fontSize = 32;
        label.anchor = TextAnchor.UpperCenter; label.alignment = TextAlignment.Center; label.color = new Color(.93f,.87f,.7f);
    }
}
