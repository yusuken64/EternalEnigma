using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Save-slot landscapes composed with the same cottage, ground and biome palettes as gameplay.</summary>
public static class CampaignBackgroundCottages
{
    private const string ScenePath = "Assets/Scenes/Editor/BiomeBackgrounds.unity";
    private const string Folder = "Assets/Art/CampaignBackgrounds/SceneMeshes";

    [MenuItem("Tools/Eternal Enigma/Campaign Backgrounds/Rebuild With Cottages")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before baking campaign backgrounds.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var author = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BiomeBackgroundAuthoring>(true)).Single();
            foreach (var set in author.Sets) Compose(set);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            PruneUnusedMeshes(scene);
            author.Bake(true);
            Debug.Log("Rebuilt eight save-slot biome landscapes with a central production cottage in every image.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void Compose(BiomeBackgroundAuthoring.Set set)
    {
        var catalog = DioramaCatalog.Load(); var style = PaintedGroundStyle.Load(); var kit = EnvironmentKit.Load();
        if (catalog == null || style == null) throw new InvalidOperationException("Production cottage and ground assets are required.");
        var old = set.Root.transform.Find("Save slot cottage");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        // Retain the old authored set for editing, but give the regenerated picture
        // one coherent composition. No old planes or floating legacy props remain visible.
        foreach (Transform child in set.Root.transform)
            if (child.GetComponent<Camera>() == null && child.GetComponent<Light>() == null) child.gameObject.SetActive(false);
        var root = new GameObject("Save slot cottage").transform; root.SetParent(set.Root.transform, false);
        var batch = new EnvironmentBatch(root);
        const int width = 48, height = 64, originX = -24, originY = -20;
        var surfaces = new GroundSurface[width, height];
        var surface = set.Biome == OverworldBiome.Water ? GroundSurface.Sand : PaintedGroundStyle.Surface(set.Biome);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int wx = x + originX, wy = y + originY;
            surfaces[x, y] = surface;
            if (wy < 0 && Mathf.Abs(wx - Mathf.Sin(wy * .17f) * 1.4f) < 1.05f)
                surfaces[x, y] = set.Biome == OverworldBiome.Tundra ? GroundSurface.Cobble : GroundSurface.Dirt;
        }
        var ground = new Material(style.DryGround) { name = "Save slot quiet ground" };
        ground.SetFloat("_DetailSoftness", 2.5f); ground.SetFloat("_DetailContrast", .68f);
        ground.SetFloat("_DetailStrength", .32f); ground.SetFloat("_TileSize", 7);
        ground = Save(ground, "QuietGround.mat");
        var painted = PaintedGroundMesh.Build(root, surfaces, style, 1, 42, originX, originY, ground);
        if (set.Biome is OverworldBiome.Water or OverworldBiome.Marsh)
        {
            bool coast = set.Biome == OverworldBiome.Water;
            float Shore(float y) => (coast ? 5 : 7) + Mathf.Sin(y * (coast ? .18f : .22f)) * 1.3f;
            foreach (var filter in painted.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; var vertices = mesh.vertices;
                if (vertices.All(v => v.x > Shore(v.y))) { Object.DestroyImmediate(filter.gameObject); continue; }
                for (int i = 0; i < vertices.Length; i++) vertices[i].x = Mathf.Min(vertices[i].x, Shore(vertices[i].y));
                mesh.vertices = vertices; mesh.RecalculateBounds();
            }
            var water = new Material(style.Water) { name = set.Biome + " calm water" };
            water.color = coast ? new Color(.82f, .92f, .94f) : new Color(.48f, .65f, .40f);
            water.SetFloat("_WaveStrength", .12f); water = Save(water, set.Biome + "-Water.mat");
            Strip(batch, water, "Water below shore", _ => 0, -24, 24, -20, 44, PaintedGroundMesh.WaterDepth);
            var edge = Solid(coast ? new Color(.76f, .84f, .72f) : new Color(.41f, .48f, .30f), set.Biome + "-Shore.mat");
            Strip(batch, edge, "Curved shoreline", Shore, -.04f, coast ? .12f : .20f, -20, 44, -.012f);
            if (coast)
            {
                var wood = Solid(new Color(.34f, .22f, .13f), "Dock-Wood.mat");
                for (float x = 2.1f; x < 8.4f; x += .38f)
                    batch.Add(TownHouseTiles.Cube, wood, new Vector3(x, -3.2f, -.15f), new Vector3(.35f, 1.5f, .14f));
                foreach (float x in new[] { 4.6f, 8.1f }) foreach (float y in new[] { -3.9f, -2.5f })
                    batch.Add(TownHouseTiles.Cube, wood, new Vector3(x, y, -.33f), new Vector3(.16f, .16f, .85f));
            }
        }

        // The central silhouette survives the wide artwork being reduced to a small slot card.
        // Separate XY/Z composition preserves useful doorway height at this miniature scale.
        var cottage = new GameObject("Biome cottage").transform; cottage.SetParent(root, false);
        cottage.localPosition = new Vector3(0, 2, 0); cottage.localScale = new Vector3(1.3f, 1.3f, 1.7f);
        var house = new EnvironmentBatch(cottage);
        DioramaSettlementGeometry.House(house, catalog, set.Biome, Vector3.zero, 4.6f, 1);
        house.Finish();
        var shingles = Save(TownHouseTiles.ShingleTexture(batch.Owner), "CottageShingles.asset");
        var roof = new Material(Shader.Find("Standard")) { name = "Cottage roof shingles", mainTexture = shingles,
            color = TownHouseTiles.Palette(set.Biome).roof * .9f };
        roof.SetFloat("_Glossiness", .04f); roof = Save(roof, set.Biome + "-Roof.mat");
        foreach (var renderer in cottage.GetComponentsInChildren<MeshRenderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name.EndsWith("_Roof", StringComparison.Ordinal) ? roof : m).ToArray();
        catalog.Add(batch, "Mailbox", set.Biome, new Vector3(3.4f, -1.1f, 0), variation: 1.4f);
        if (set.Biome is OverworldBiome.Grassland or OverworldBiome.Forest or OverworldBiome.Water)
            catalog.Add(batch, "FlowerBox", set.Biome, new Vector3(-2.5f, -.4f, 0), variation: 1.5f);

        void Prop(string id, float x, float y, float scale, float angle = 0) =>
            catalog.Add(batch, id, set.Biome, new Vector3(x, y, 0), angle, scale);
        switch (set.Biome)
        {
            case OverworldBiome.Grassland:
                Prop("PackTree01", -7, 8, 1.7f, 15); Prop("PackTree02", 6, 10, 1.6f, 120);
                Prop("BoulderLarge", -9, 3, 1.8f, 25); Prop("PackFlower01", -5, -5, 2); Prop("PackGrass03", 5, -5, 2);
                break;
            case OverworldBiome.Forest:
                Prop("PackTree01", -6.5f, 8, 2.0f, 15); Prop("PackTree03", 6.5f, 8, 1.9f, 110);
                Prop("PackTree02", -1, 14, 2.1f, 45); Prop("Fern", -5, -4, 2); Prop("MushroomRing", 5, -3, 1.8f);
                break;
            case OverworldBiome.Desert:
                batch.Add(kit.Mesh("Cactus"), kit.Material(set.Biome), new Vector3(-6.5f, 6, 0), Vector3.one * 4.5f);
                batch.Add(kit.Mesh("Cactus"), kit.Material(set.Biome), new Vector3(6.5f, 8, 0), Vector3.one * 3.5f, 25);
                Prop("BoulderLarge", -8, 13, 4.5f, 10); Prop("BoulderLarge", 8, 14, 4, 80); Prop("BoulderSmall", 5, -4, 1.3f);
                break;
            case OverworldBiome.Mountain:
                Prop("BoulderLarge", -7, 11, 5, 35); Prop("BoulderLarge", 6, 14, 6, 100);
                Prop("PackTree03", -7, 2, 1.3f, 70); Prop("PackTree03", 7, 6, 1.5f, 30); Prop("BoulderSmall", 5, -4, 1.6f);
                break;
            case OverworldBiome.Water:
                Prop("Palm", -6.5f, 7, 1.9f, 15); Prop("Palm", 2.5f, 10, 1.7f, 50);
                Prop("BoulderLarge", -8, 11, 2.3f, 80); Prop("PackGrass03", -5, -4, 1.5f);
                break;
            case OverworldBiome.Marsh:
                Prop("Willow", -7, 9, 1.9f, 10); Prop("DeadTree", 6, 12, 1.6f, 70);
                Prop("Reeds", 6, -4, 2); Prop("Reeds", 6, 5, 2.1f, 50); Prop("MushroomRing", -5, -3, 1.6f);
                break;
            case OverworldBiome.Tundra:
                Prop("SnowPine", -6.5f, 8, 1.8f, 10); Prop("SnowPine", 6.5f, 9, 1.9f, 60);
                Prop("BoulderLarge", -9, 13, 4, 30); Prop("BoulderLarge", 7, 14, 3.5f, 110);
                break;
            case OverworldBiome.Volcanic:
                Prop("CharredTree", -6, 7, 1.6f, 20); Prop("CharredTree", 9, 7, 1.4f, 110);
                Prop("BoulderLarge", -8, 12, 4.4f, 50); Prop("BoulderLarge", 8, 14, 4.8f, 90);
                var lava = new Material(Shader.Find("Standard")) { name = "Distant lava", color = new Color(.58f, .16f, .045f) };
                lava.SetColor("_EmissionColor", new Color(.28f, .035f, 0)); lava.EnableKeyword("_EMISSION");
                lava = Save(lava, "DistantLava.mat");
                float LavaCenter(float y) => 6.6f + Mathf.Sin(y * .23f) * .7f;
                var basalt = Solid(new Color(.16f, .14f, .15f), "Lava-Bank.mat");
                Strip(batch, basalt, "Lava bank", LavaCenter, -.85f, .85f, -20, 30, -.016f);
                Strip(batch, lava, "Visible lava stream", LavaCenter, -.48f, .48f, -20, 30, -.035f);
                break;
        }
        batch.Finish();
        // These are authored still-image scenes, so contact and cast shadows can
        // be retained without adding shadow work to the runtime town renderer.
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.receiveShadows = true;
            if (renderer.GetComponent<SilhouetteParticipant>()?.Role == SilhouetteRole.Caster)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        var camera = set.Camera; camera.orthographic = true; camera.orthographicSize = 7.0f;
        camera.transform.localPosition = new Vector3(7, -18, -13);
        camera.transform.LookAt(set.Root.transform.TransformPoint(new Vector3(0, 2, -1.4f)), Vector3.back);
        foreach (var light in set.Root.GetComponentsInChildren<Light>(true))
        {
            light.intensity = 1; light.color = new Color(1, .96f, .88f);
            light.transform.rotation = Quaternion.LookRotation(new Vector3(.47f, .33f, .82f), Vector3.up);
            light.shadows = LightShadows.Soft; light.shadowStrength = .68f;
            light.shadowBias = .025f; light.shadowNormalBias = .12f;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
        }
        int index = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            filter.sharedMesh = Save(Object.Instantiate(filter.sharedMesh), set.Biome + "-" + index++ + ".asset");
        foreach (var owner in root.GetComponentsInChildren<EnvironmentMeshOwner>(true)) Object.DestroyImmediate(owner);
        AssetDatabase.SaveAssets();
    }

    private static Material Solid(Color color, string name)
    {
        var material = new Material(Shader.Find("Standard")) { name = name, color = color };
        material.SetFloat("_Glossiness", .04f); return Save(material, name);
    }

    private static void Strip(EnvironmentBatch batch, Material material, string name, Func<float, float> center,
        float left, float right, float bottom, float top, float depth)
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>();
        var uv = new List<Vector2>(); var indices = new List<int>();
        int rows = Mathf.CeilToInt((top - bottom) * 2);
        for (int row = 0; row <= rows; row++)
        {
            float y = Mathf.Lerp(bottom, top, row / (float)rows), x = center(y);
            vertices.Add(new Vector3(x + left, y, depth)); vertices.Add(new Vector3(x + right, y, depth));
            normals.Add(Vector3.back); normals.Add(Vector3.back);
            uv.Add(new Vector2((x + left) / 8, y / 8)); uv.Add(new Vector2((x + right) / 8, y / 8));
            if (row < rows) { int i = row * 2; indices.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 }); }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds(); batch.Owner.Meshes.Add(mesh);
        batch.Add(mesh, material, Vector3.zero, Vector3.one, role: SilhouetteRole.Receiver);
    }

    private static void PruneUnusedMeshes(Scene scene)
    {
        var used = new HashSet<string>(scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
            .Where(f => f.sharedMesh != null).Select(f => AssetDatabase.GetAssetPath(f.sharedMesh)), StringComparer.Ordinal);
        string absoluteFolder = Path.GetFullPath(Folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string absolutePath = Path.GetFullPath(path);
            if (!absolutePath.StartsWith(absoluteFolder, StringComparison.OrdinalIgnoreCase) || used.Contains(path)) continue;
            string[] parts = Path.GetFileNameWithoutExtension(path).Split('-');
            if (parts.Length != 2 || !Enum.TryParse(parts[0], out OverworldBiome _) || !int.TryParse(parts[1], out _)) continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static T Save<T>(T value, string name) where T : Object
    {
        value.hideFlags = HideFlags.None;
        string path = Folder + "/" + name; var saved = AssetDatabase.LoadAssetAtPath<T>(path);
        if (saved == null) { AssetDatabase.CreateAsset(value, path); return value; }
        // CopySerialized updates the asset payload without invalidating all of
        // Unity's native mesh/material render data. Reusing a numbered output
        // after the composition changes can therefore display its previous
        // geometry until the editor reloads. Typed setters update both sides.
        if (value is Mesh mesh && saved is Mesh savedMesh)
        {
            savedMesh.Clear(false); savedMesh.indexFormat = mesh.indexFormat;
            savedMesh.vertices = mesh.vertices; savedMesh.normals = mesh.normals;
            savedMesh.tangents = mesh.tangents; savedMesh.colors = mesh.colors;
            var coordinates = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            {
                mesh.GetUVs(channel, coordinates);
                if (coordinates.Count == 0) continue;
                int dimension = mesh.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + channel));
                if (dimension == 2) savedMesh.SetUVs(channel, coordinates.Select(v => new Vector2(v.x, v.y)).ToList());
                else if (dimension == 3) savedMesh.SetUVs(channel, coordinates.Select(v => new Vector3(v.x, v.y, v.z)).ToList());
                else savedMesh.SetUVs(channel, coordinates);
            }
            savedMesh.subMeshCount = mesh.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                savedMesh.SetIndices(mesh.GetIndices(sub), mesh.GetTopology(sub), sub, false);
            savedMesh.bounds = mesh.bounds;
            savedMesh.UploadMeshData(false);
        }
        else if (value is Material material && saved is Material savedMaterial)
        {
            savedMaterial.shader = material.shader;
            savedMaterial.CopyPropertiesFromMaterial(material);
            savedMaterial.enableInstancing = material.enableInstancing;
            savedMaterial.doubleSidedGI = material.doubleSidedGI;
            savedMaterial.globalIlluminationFlags = material.globalIlluminationFlags;
        }
        else if (value is Texture2D texture && saved is Texture2D savedTexture)
        {
            savedTexture.Reinitialize(texture.width, texture.height, texture.format, texture.mipmapCount > 1);
            savedTexture.SetPixels(texture.GetPixels());
            savedTexture.wrapMode = texture.wrapMode; savedTexture.filterMode = texture.filterMode;
            savedTexture.Apply(true, false);
        }
        else EditorUtility.CopySerialized(value, saved);
        saved.name = value.name;
        Object.DestroyImmediate(value); EditorUtility.SetDirty(saved); return saved;
    }
}
