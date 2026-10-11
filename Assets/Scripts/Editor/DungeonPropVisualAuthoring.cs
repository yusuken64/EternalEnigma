using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;

public static class DungeonPropVisualAuthoring
{
    const string Folder = "Assets/Resources/DungeonProps";
    const string EnemyPath = "Assets/Prefabs/Dungeon/Enemies/Enemy_BlackKnight.prefab";

    public static void SampleSouthIdle(Enemy enemy)
    {
        enemy.SetFacing(Facing.Down);
        var animator = enemy.Animator;
        var position = animator.transform.localPosition;
        var rotation = animator.transform.localRotation;
        var scale = animator.transform.localScale;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        var idle = enemy.AnimationStates("IdleNormal").First();
        animator.Play(Animator.StringToHash($"{animator.GetLayerName(0)}.{idle}"), 0, 0);
        animator.Update(0);
        // Evaluate the actual controller/avatar pose while keeping the game's XY-plane adapter.
        animator.transform.SetLocalPositionAndRotation(position, rotation);
        animator.transform.localScale = scale;
    }

    public static Vector3[] EnemyPoints(GameObject root)
    {
        var points = new List<Vector3>();
        foreach (var renderer in DioramaFitAuthoring.Renderers(root))
        {
            var skin = renderer as SkinnedMeshRenderer;
            var mesh = skin != null ? new Mesh() : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            try
            {
                // Compensate for renderer scale before applying its world transform once.
                if (skin != null) skin.BakeMesh(mesh, true);
                points.AddRange(DungeonPickupAuthoring.Vertices(mesh).Select(renderer.transform.TransformPoint));
            }
            finally { if (skin != null) Object.DestroyImmediate(mesh); }
        }
        return points.ToArray();
    }

    [MenuItem("Tools/Eternal Enigma/Art/Build Enemy Statue")]
    public static void BuildStatue()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var pieces = new List<Mesh>();
        try
        {
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath), scene);
            var enemy = source.GetComponent<Enemy>();
            SampleSouthIdle(enemy);
            var combines = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var renderer in source.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer))
            {
                var mesh = renderer is SkinnedMeshRenderer ? new Mesh() : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                if (renderer is SkinnedMeshRenderer skin) { skin.BakeMesh(mesh, true); pieces.Add(mesh); }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    combines.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = renderer.transform.localToWorldMatrix });
                    materials.Add(renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)]);
                }
            }
            var baked = new Mesh { name = "StoneHulkStatue" };
            baked.CombineMeshes(combines.ToArray(), false, true);
            var bounds = baked.bounds;
            var offset = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
            baked.vertices = baked.vertices.Select(p => p - offset).ToArray();
            baked.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/StoneHulkStatue.asset");
            // Invalidate uploaded buffers as well as serialized data when rebuilding in this editor session.
            existing.Clear(); EditorUtility.CopySerialized(baked, existing); existing.UploadMeshData(false); Object.DestroyImmediate(baked);
            var statue = PrefabUtility.LoadPrefabContents(Folder + "/StoneHulkStatue.prefab");
            try
            {
                statue.GetComponent<MeshFilter>().sharedMesh = existing;
                statue.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                PrefabUtility.SaveAsPrefabAsset(statue, Folder + "/StoneHulkStatue.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(statue); }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            foreach (var mesh in pieces) Object.DestroyImmediate(mesh);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/Eternal Enigma/Art/Build Dungeon Door")]
    public static void BuildDoor()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/KennyNL/Castle Kit/Models/door.fbx");
        var filter = source.GetComponent<MeshFilter>();
        var mesh = Object.Instantiate(filter.sharedMesh);
        mesh.name = "LockedDoor";
        // Kenney's door is Y-up, Z-wide with a nonuniform import scale and an offset pivot.
        var matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, -90, 0) * source.transform.localRotation, source.transform.localScale);
        var vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        var bounds = new Bounds(vertices[0], Vector3.zero);
        foreach (var point in vertices) bounds.Encapsulate(point);
        var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] - origin) / bounds.size.x;
        mesh.vertices = vertices;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var normals = mesh.normals;
        mesh.uv = vertices.Select((p, i) => Mathf.Abs(normals[i].x) > Mathf.Abs(normals[i].z)
            ? new Vector2(p.z * 2, p.y * 2) : new Vector2(p.x * 2, p.y * 2)).ToArray();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/LockedDoor.asset");
        if (existing == null) AssetDatabase.CreateAsset(mesh, Folder + "/LockedDoor.asset");
        else { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); existing.UploadMeshData(false); Object.DestroyImmediate(mesh); mesh = existing; }
        var root = new GameObject("LockedDoor");
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = new[] {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/DungeonThemes/Materials/Grassland_Interior_Boundary.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/MainMenu/Scenery_mat_Dungeon_wood.mat") };
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/LockedDoor.prefab");
            const string dungeonPath = "Assets/Prefabs/Dungeon/TileWorldDungeon.prefab";
            var dungeon = PrefabUtility.LoadPrefabContents(dungeonPath);
            try { dungeon.GetComponent<TileWorldDungeon>().LockedDoorPrefab = prefab; PrefabUtility.SaveAsPrefabAsset(dungeon, dungeonPath); }
            finally { PrefabUtility.UnloadPrefabContents(dungeon); }
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/Eternal Enigma/Art/Capture Dungeon Prop Fixes")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before capture.");
        var previous = SceneManager.GetActiveScene();
        var review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(review);
        Directory.CreateDirectory("Temp/DungeonPropVisuals");
        try
        {
            var camera = DungeonPickupAuthoring.GameplayCamera(review, Vector3.zero, out _);
            DungeonSmartCapture.Lighting(camera);
            foreach (var biome in new[] { OverworldBiome.Grassland, OverworldBiome.Desert })
            {
                var host = new GameObject("Prop review generator"); var creator = host.AddComponent<TileWorldCreator>();
                var world = new GameObject("Prop review world"); creator.worldObject = world;
                var asset = DungeonPresentation.CloneTemplate(DungeonSmartAuthoring.Template(false)); creator.twcAsset = asset;
                try
                {
                    int seed = biome == OverworldBiome.Grassland ? 44 : 71;
                    CoreDungeonLayerGenerator.Configure(asset, new DungeonFloorOptions(seed));
                    var selection = new DungeonVisualSelection { Biome = biome, Environment = DungeonEnvironmentKind.Interior };
                    var catalog = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog"); var theme = catalog.Get(selection);
                    catalog.Apply(asset, selection, false);
                    creator.SetCustomRandomSeed(seed); creator.ExecuteAllBlueprintLayers(); CoreLayoutCache.ClearResultFlags(asset); creator.ExecuteAllBuildLayers(true);
                    CoreLayoutCache.TryGetDungeon(creator, out var floor);
                    DungeonPresentation.Decorate(creator, floor, theme);
                    var cosmetics = world.transform.Find("Theme cosmetics");
                    var groups = cosmetics.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(f.transform.TransformPoint))
                        .GroupBy(p => new GridPoint(Mathf.FloorToInt(p.x / 2), Mathf.FloorToInt(p.y / 2))).ToArray();
                    var group = groups.FirstOrDefault(g => floor.IsWalkable(new GridPoint(g.Key.X, g.Key.Y - 1))) ?? groups.First();
                    var focus = new Vector3((group.Key.X + .5f) * 2, group.Key.Y * 2 - 1, -.3f);
                    camera.orthographicSize = 4; camera.transform.position = focus + new Vector3(0, -12, -14); camera.transform.LookAt(focus);
                    Save(camera, biome + "_cosmetics");
                    if (biome == OverworldBiome.Desert)
                    {
                        var dungeon = host.AddComponent<TileWorldDungeon>(); dungeon.Setup(creator, floor); dungeon.Interactables = new();
                        dungeon.LockedDoorPrefab = Resources.Load<GameObject>("DungeonProps/LockedDoor");
                        var cell = Enumerable.Range(1, floor.Width - 2).SelectMany(x => Enumerable.Range(1, floor.Height - 2).Select(y => new Vector3Int(x, y)))
                            .First(p => dungeon.IsFloorCell(p) && !dungeon.IsFloorCell(p + Vector3Int.left) && !dungeon.IsFloorCell(p + Vector3Int.right));
                        DungeonProp.Create(dungeon, new DungeonScenery(cell.ToGridPoint(), DungeonSceneryKind.Door, 10, 0, SceneryReward.None), theme);
                        focus = dungeon.CellToWorld(cell) + new Vector3(1, 0, -1);
                        camera.transform.position = focus + new Vector3(0, -12, -14); camera.transform.LookAt(focus);
                        Save(camera, "Door");
                    }
                }
                finally { DungeonPresentation.ClearOutput(world); Object.DestroyImmediate(world); Object.DestroyImmediate(host); DungeonPresentation.ReleaseTemplate(asset); }
            }
            foreach (bool mimic in new[] { false, true })
            {
                var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(mimic ? "Assets/Prefabs/Dungeon/Enemies/Enemy_ChestMonster.prefab" : EnemyPath), review);
                foreach (var script in source.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
                foreach (var sprite in source.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
                var enemy = source.GetComponent<Enemy>(); SampleSouthIdle(enemy); enemy.Animator.enabled = false;
                var prop = Object.Instantiate(mimic ? enemy.GetComponent<EnemyBehavior>().ChestVisualPrefab : Resources.Load<GameObject>("DungeonProps/StoneHulkStatue"));
                if (mimic) prop.transform.localScale = enemy.VisualParent.transform.lossyScale;
                var a = DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(prop));
                var points = EnemyPoints(source);
                var b = new Bounds(points[0], Vector3.zero); foreach (var point in points) b.Encapsulate(point);
                float separation = Mathf.Max(a.size.x, b.size.x) * .6f + 1;
                prop.transform.position += new Vector3(-separation - a.center.x, -a.center.y, DungeonPresentation.GroundPlaneZ - a.max.z);
                source.transform.position += new Vector3(separation - b.center.x, -b.center.y, DungeonPresentation.GroundPlaneZ - b.max.z);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Quad); ground.transform.position = new Vector3(0, 0, .01f);
                ground.transform.localScale = new Vector3(separation * 4, separation * 4, 1);
                ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/DungeonThemes/Materials/Grassland_Interior_Floor.mat");
                camera.orthographicSize = Mathf.Max(4, separation * 1.3f);
                var focus = new Vector3(0, 0, -Mathf.Max(a.size.z, b.size.z) * .4f);
                camera.transform.position = focus + new Vector3(0, -12, -14); camera.transform.LookAt(focus);
                Save(camera, mimic ? "Mimic_comparison" : "Statue_comparison");
                Object.DestroyImmediate(prop); Object.DestroyImmediate(source); Object.DestroyImmediate(ground);
            }
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(review, true); }
    }

    static void Save(Camera camera, string name)
    {
        var image = DungeonSmartCapture.Render(camera, 1600, 1000);
        try { File.WriteAllBytes("Temp/DungeonPropVisuals/" + name + ".png", image.EncodeToPNG()); }
        finally { Object.DestroyImmediate(image); }
    }
}
