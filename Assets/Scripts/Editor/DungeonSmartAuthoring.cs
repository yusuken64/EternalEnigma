using System;
using System.IO;
using System.Linq;
using System.Text;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;
using EternalEnigma.Core.World;
using Object = UnityEngine.Object;

public static class DungeonSmartAuthoring
{
    public const string Folder = "Assets/Art/DungeonThemes/PolyartSmartTiles";
    public const string Evidence = "Docs/Art/Verification/DungeonSmartLayers";
    [Serializable] public sealed class MeshData { public string name; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    [Serializable] public sealed class MeshFile { public MeshData[] meshes; public float authoredHeight, authoredCellSize; }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Install Kit and Layers")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Evidence);
        var source = JsonUtility.FromJson<MeshFile>(File.ReadAllText(Folder + "/Meshes.json"));
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/DungeonStone.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = "Dungeon Polyart Stone" };
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG Tiny Fantasy World 01 PA/Texture/Base Map.png");
            material.color = Color.white;
            material.SetFloat("_Glossiness", .08f);
            material.SetFloat("_Metallic", 0);
            AssetDatabase.CreateAsset(material, Folder + "/DungeonStone.mat");
        }
        foreach (var data in source.meshes)
        {
            string path = Folder + "/" + data.name;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path + ".asset");
            if (mesh == null) { mesh = new Mesh { name = data.name }; AssetDatabase.CreateAsset(mesh,path + ".asset"); }
            mesh.Clear(); mesh.vertices = data.vertices; mesh.uv = data.uv; mesh.triangles = data.triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go = new GameObject(data.name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(go,path + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }
        GameObject Part(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab");
        var tiles = LoadOrCreate<TileWorldCreator6TilesPreset>(Folder + "/SixTiles.asset");
        tiles.singleTile = Part("Single"); tiles.deadEndTile = Part("End"); tiles.straightTile = Part("StraightA");
        tiles.cornerTile = Part("Corner"); tiles.threeWayTile = Part("Junction"); tiles.fourWayTile = Part("Cross");
        EditorUtility.SetDirty(tiles);
        var kit = LoadOrCreate<DungeonBoundaryPreset>(Folder + "/Boundary.asset");
        kit.Tiles = tiles; kit.StraightVariants = new[] { Part("StraightA"), Part("StraightB") };
        kit.SolidFill = Part("SolidFill"); kit.QuadrantFill = Part("QuadrantFill"); kit.ConcaveCorner = Part("ConcaveCorner");
        if(source.authoredHeight>0)kit.AuthoredHeight=source.authoredHeight;
        if(source.authoredCellSize>0)kit.AuthoredCellSize=source.authoredCellSize;
        EditorUtility.SetDirty(kit);
        var catalog = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");
        ConfigureAllThemes(catalog,kit);
        foreach (bool throne in new[] { false, true })
        {
            var asset = Template(throne);
            var before = BlueprintSignature(asset);
            for (int i=0;i<asset.mapBuildLayers.Count;i++)
            {
                var old = asset.mapBuildLayers[i];
                // Only the known production presentation layers are migrated. Extra user layers survive.
                if (old is InstantiateTiles tilesLayer && old.layerName is "Dungeon" or "Floor" or "Carpet")
                {
                    DungeonThemeTileLayer layer = old.layerName == "Dungeon" ? new DungeonBoundaryLayer() : new DungeonThemeTileLayer();
                    layer.guid = old.guid; layer.assignedGenerationLayerGuid = old.assignedGenerationLayerGuid;
                    layer.layerName = old.layerName; layer.active = old.active; layer.foldout = old.foldout;
                    layer.Role = old.layerName == "Dungeon" ? DungeonThemeRole.Boundary : old.layerName == "Floor" ? DungeonThemeRole.Floor : DungeonThemeRole.Accent;
                    layer.Offset = tilesLayer.globalPositionOffset + (throne && layer.Role == DungeonThemeRole.Accent ? new Vector3(0,0,-.0005f) : Vector3.zero);
                    layer.IgnoreLayers = tilesLayer.ignoreLayers.ToArray();
                    asset.mapBuildLayers[i] = layer;
                }
                // These two authored legacy effects were already retired by the production theme adapter.
                if (old.layerName is "Columns" or "Torchlights") old.active = false;
                if (old is DungeonBoundaryLayer boundary && Mathf.Approximately(boundary.WallHeight,1.5075164f))
                    boundary.WallHeight=kit.AuthoredHeight;
                if (old is DungeonBoundaryLayer smart && smart.PresentationVersion==0)
                { smart.CutawayForeground=true;smart.PresentationVersion=1; }
            }
            catalog.Apply(asset,new DungeonVisualSelection { Biome = OverworldBiome.Grassland, Environment = DungeonEnvironmentKind.Interior, UseBiomePresentation = true },throne);
            if (before != BlueprintSignature(asset)) throw new InvalidOperationException("Blueprint mutation during presentation migration.");
            File.WriteAllText(Evidence + "/" + asset.name + "_Blueprint.sha256", before + "\n");
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Installed Polyart dungeon kit and committed three presentation layers per template; blueprint signatures unchanged.");
    }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Apply Kit to All Biomes")]
    public static void ApplyKitToAllBiomes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        var kit = AssetDatabase.LoadAssetAtPath<DungeonBoundaryPreset>(Folder + "/Boundary.asset");
        if (kit == null) throw new InvalidOperationException("Install the dungeon smart tile kit first.");
        ConfigureAllThemes(Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog"),kit);
        AssetDatabase.SaveAssets();
        Debug.Log("All 16 dungeon biome/environment selections have smart boundaries for regular and throne layouts.");
    }

    static void ConfigureAllThemes(DungeonThemeCatalog catalog,DungeonBoundaryPreset kit)
    {
        if (!AssetDatabase.IsValidFolder(Folder + "/Themes")) AssetDatabase.CreateFolder(Folder,"Themes");
        foreach (var theme in catalog.Themes)
        {
            var boundary = kit;
            if (theme.Biome != OverworldBiome.Grassland || theme.Environment != DungeonEnvironmentKind.Interior)
            {
                boundary = LoadOrCreate<DungeonBoundaryPreset>($"{Folder}/Themes/{theme.Biome}_{theme.Environment}.asset");
                boundary.Tiles = kit.Tiles; boundary.StraightVariants = kit.StraightVariants.ToArray();
                boundary.SolidFill = kit.SolidFill; boundary.QuadrantFill = kit.QuadrantFill; boundary.ConcaveCorner = kit.ConcaveCorner;
                boundary.AuthoredCellSize = kit.AuthoredCellSize; boundary.AuthoredHeight = kit.AuthoredHeight;
                // Existing dungeon-owned surfaces carry each biome's painted texture and Box projection.
                // Reuse them read-only; cutaway materials are owned by each generated layer.
                boundary.MaterialOverride = theme.RegularBoundary.edgeTile.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                EditorUtility.SetDirty(boundary);
            }
            // Preserve any explicitly authored replacement kit on subsequent installer runs.
            if (theme.RegularSmartBoundary == null) theme.RegularSmartBoundary = boundary;
            if (theme.ThroneSmartBoundary == null) theme.ThroneSmartBoundary = boundary;
        }
        EditorUtility.SetDirty(catalog);
    }
    public static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); }
        return asset;
    }
    public static string BlueprintSignature(TileWorldCreatorAsset asset)
    {
        var bytes = TWC.OdinSerializer.SerializationUtility.SerializeValue(asset.mapBlueprintLayers, TWC.OdinSerializer.DataFormat.Binary);
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Inspect Sources")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Evidence);
        var report = new StringBuilder();
        foreach (bool throne in new[] { false, true })
        {
            var asset = Template(throne);
            report.AppendLine($"{asset.name} grid={asset.cellSize} orientation={asset.mapOrientation}");
            foreach (var b in asset.mapBlueprintLayers) report.AppendLine($" blueprint {b.guid} {b.layerName}");
            foreach (var layer in asset.mapBuildLayers)
            {
                report.AppendLine($" build {layer.guid} {layer.layerName} {layer.GetType().Name} active={layer.active} blueprint={layer.assignedGenerationLayerGuid}");
                if (layer is InstantiateTiles tiles)
                    report.AppendLine($"  offset={tiles.globalPositionOffset} excludes={string.Join(",",tiles.ignoreLayers)} presets={string.Join(",",tiles.tiles.Select(t=>t.preset?.name))}");
            }
        }
        File.WriteAllText(Evidence + "/SourceLayers.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    public static TileWorldCreatorAsset Template(bool throne) => AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>(
        "Assets/Prefabs/Dungeon/" + (throne ? "DungeonThroneAsset" : "DungeonAsset") + ".asset");
}
