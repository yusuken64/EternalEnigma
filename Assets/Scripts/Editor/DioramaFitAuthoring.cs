using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Reproducible Phase 0 measurements. Vendor content is read only; adapters live in our folder.
public static class DioramaFitAuthoring
{
    public const string EnvironmentSource = "Assets/RPG Tiny Fantasy World 01 PA";
    public const string MonsterSource = "Assets/RPGMonsterWave4Polyart";
    public const string Destination = "Assets/Art/Diorama";
    public const string Evidence = "Docs/Art/Previews/Diorama/Fit";
    public const string HeroPath = "Assets/Prefabs/Town/Allies/Ally_MC01.prefab";

    [Serializable] public sealed class MeshRecord
    {
        public string name, path;
        public int vertices, triangles, submeshes;
        public bool readable;
    }
    [Serializable] public sealed class AssetRecord
    {
        public string path;
        public Vector3 size, center;
        public int lod0Triangles, lod1Triangles, colliders;
        public string[] materials, shaders, animations;
        public MeshRecord[] meshes;
    }
    [Serializable] public sealed class Report
    {
        public string unityVersion;
        public bool webGLSupported;
        public float heroHeight;
        public AssetRecord hero;
        public AssetRecord[] environment, monsters;
    }

    [MenuItem("Tools/Eternal Enigma/Diorama/Audit Imported Assets")]
    public static void Audit()
    {
        Directory.CreateDirectory(Evidence);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var hero = Measure(HeroPath, scene);
            var report = new Report {
                unityVersion = Application.unityVersion,
                webGLSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL),
                hero = hero, heroHeight = hero.size.z,
                environment = Prefabs(EnvironmentSource + "/Prefab").Select(p => Measure(p, scene)).ToArray(),
                monsters = Prefabs(MonsterSource + "/Prefab").Where(p => !p.Contains("/Weapons/")).Select(p => Measure(p, scene)).ToArray()
            };
            File.WriteAllText(Evidence + "/Measurements.json", JsonUtility.ToJson(report, true));
            Debug.Log($"Diorama fit: H={report.heroHeight:F4}; {report.environment.Length} environment prefabs, {report.monsters.Length} monsters. WebGL support={report.webGLSupported}. {Evidence}/Measurements.json");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static string[] Prefabs(string folder) => AssetDatabase.FindAssets("t:Prefab", new[] { folder })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    static AssetRecord Measure(string path, Scene scene)
    {
        var root = Spawn(path, scene);
        try
        {
            var rs = Renderers(root);
            var lod = root.GetComponentInChildren<LODGroup>();
            var levels = lod != null ? lod.GetLODs() : Array.Empty<LOD>();
            var first = levels.Length > 0 ? levels[0].renderers : rs;
            var allMeshes = rs.Select(Mesh).Where(m => m != null).Distinct().ToArray();
            var materials = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            var bounds = Bounds(first);
            return new AssetRecord {
                path = path, size = bounds.size, center = bounds.center,
                lod0Triangles = first.Sum(r => Triangles(Mesh(r))),
                lod1Triangles = levels.Length > 1 ? levels[1].renderers.Sum(r => Triangles(Mesh(r))) : -1,
                colliders = root.GetComponentsInChildren<Collider>(true).Length + root.GetComponentsInChildren<Collider2D>(true).Length,
                materials = materials.Select(AssetDatabase.GetAssetPath).ToArray(),
                shaders = materials.Select(m => m.shader != null ? m.shader.name : "MISSING").ToArray(),
                animations = root.GetComponentsInChildren<Animator>(true).Where(a => a.runtimeAnimatorController != null)
                    .SelectMany(a => a.runtimeAnimatorController.animationClips).Where(c => c != null).Select(c => c.name).Distinct().OrderBy(n => n).ToArray(),
                meshes = allMeshes.Select(m => new MeshRecord {
                    name = m.name, path = AssetDatabase.GetAssetPath(m), vertices = m.vertexCount,
                    triangles = Triangles(m), submeshes = m.subMeshCount, readable = m.isReadable
                }).ToArray()
            };
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static Mesh Mesh(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
    public static int Triangles(Mesh mesh) => mesh == null ? 0 : Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3);
    public static Renderer[] Renderers(GameObject root) => root.GetComponentsInChildren<Renderer>()
        .Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
    public static Bounds Bounds(IEnumerable<Renderer> renderers)
    {
        bool first = true; var bounds = new Bounds();
        foreach (var r in renderers.Where(r => r != null))
        {
            // Skinned renderer culling bounds can include every animation. Bake the current pose.
            if (r is SkinnedMeshRenderer skinned)
            {
                var mesh = new Mesh(); skinned.BakeMesh(mesh);
                foreach (var p in mesh.vertices)
                {
                    var world = skinned.transform.TransformPoint(p);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
                }
                Object.DestroyImmediate(mesh);
            }
            else if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    public static GameObject Spawn(string path, Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing fit asset: " + path);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        root.transform.position = Vector3.zero;
        foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null) script.enabled = false;
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            var idle = animator.runtimeAnimatorController?.animationClips.FirstOrDefault(c => c.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0);
            if (idle != null) idle.SampleAnimation(animator.gameObject, 0);
            animator.enabled = false;
        }
        return root;
    }

    // The imported shader may be missing, so read serialized texture slots rather than HasProperty.
    static Texture SourceTexture(Material source)
    {
        var serialized = new SerializedObject(source);
        var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
        foreach (string key in new[] { "_BaseMap", "_BaseColor", "_MainTex", "_BaseColorMap" })
            for (int i = 0; i < textures.arraySize; i++)
            {
                var item = textures.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("first").stringValue == key && item.FindPropertyRelative("second.m_Texture").objectReferenceValue is Texture texture) return texture;
            }
        return null;
    }

    public static Material LitMaterial(Material source)
    {
        string pack = AssetDatabase.GetAssetPath(source).StartsWith(MonsterSource, StringComparison.Ordinal) ? "Wave4" : "Environment";
        string path = $"{Destination}/Materials/{pack}_{source.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool exists = material != null;
        Directory.CreateDirectory(Destination + "/Materials"); AssetDatabase.Refresh();
        var shader = Shader.Find(pack == "Environment" && (source.name.StartsWith("Tree") || source.name.StartsWith("Grass")) ? "EternalEnigma/Diorama Foliage" : "Standard");
        if (!exists) material = new Material(shader) { name = pack + "_" + source.name };
        else material.shader = shader;
        material.mainTexture = SourceTexture(source); material.color = Color.white;
        material.SetFloat("_Glossiness", .12f); material.SetFloat("_Metallic", 0);
        if (material.HasProperty("_SpecularHighlights")) { material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); material.SetFloat("_SpecularHighlights", 0); }
        material.enableInstancing = true;
        if (!exists) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
        return material;
    }

    public static string Adapt(string sourcePath, bool monster = false)
    {
        string folder = Destination + (monster ? "/Wave4" : "/Prefabs");
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string path = folder + "/" + Path.GetFileName(sourcePath);
        var root = new GameObject(Path.GetFileNameWithoutExtension(sourcePath));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(-90, monster ? 180 : 0, 0);
        try
        {
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var collider in model.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
            // Wave 4's imported prefabs resolve to the environment atlas in this project.
            // Bind the Wave 4 atlas explicitly in our adapter, preserving the vendor prefab.
            var monsterMaterial = monster ? AssetDatabase.LoadAssetAtPath<Material>(MonsterSource + "/Material/DefaultPolyart.mat") : null;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => monsterMaterial != null ? LitMaterial(monsterMaterial) : m != null ? LitMaterial(m) : null).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
        return path;
    }

    [MenuItem("Tools/Eternal Enigma/Diorama/Capture Fit Candidates")]
    public static void CaptureFit()
    {
        Directory.CreateDirectory(Evidence);
        var report = JsonUtility.FromJson<Report>(File.ReadAllText(Evidence + "/Measurements.json"));
        var trees = Prefabs(EnvironmentSource + "/Prefab/TreePlants").Where(p => Path.GetFileName(p).StartsWith("Tree")).ToArray();
        var adapters = trees.Select(p => Adapt(p)).ToArray();
        var ratios = new List<string> { "asset,nativeHeight,scale,worldHeight,canopyWidth,screenHeightRatio" };
        for (int i = 0; i < adapters.Length; i++)
            CapturePair(adapters[i], "Tree" + (i + 1), report.heroHeight * 2.85f, ratios,
                report.environment.First(a => a.path == trees[i]).size.y);
        foreach (var name in new[] { "GroundPadding01_1", "Mountain01", "WoodFence01", "SignPost01", "RoadA01", "WatchTower01" })
        {
            var source = report.environment.First(a => Path.GetFileNameWithoutExtension(a.path) == name);
            CapturePair(Adapt(source.path), name, name.StartsWith("Mountain") || name.StartsWith("WatchTower") ? report.heroHeight * 3 : name.StartsWith("Sign") ? report.heroHeight * 1.1f : report.heroHeight * .6f, null, source.size.y);
        }
        foreach (var monster in report.monsters)
            CapturePair(Adapt(monster.path, true), "Wave4_" + Path.GetFileNameWithoutExtension(monster.path), report.heroHeight * 1.4f, null, monster.size.y);
        CaptureGroup(report.heroHeight);
        File.WriteAllLines(Evidence + "/TreeScale.csv", ratios);
        AssetDatabase.SaveAssets();
        Debug.Log("Diorama fit captures saved to " + Evidence);
    }

    static void CapturePair(string path, string name, float targetHeight, List<string> ratios, float nativeHeight)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var hero = Spawn(HeroPath, scene);
            hero.GetComponent<TownAlly>()?.SetFacing(Facing.Down);
            var item = Spawn(path, scene);
            foreach (var lod in item.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            var itemBounds = Bounds(Renderers(item).Where(r => !r.name.Contains("LOD1") && !r.name.Contains("LOD2")));
            // Thin padding/roads are fitted by footprint; do not turn them into towers.
            float scale = name.StartsWith("Ground") || name.StartsWith("Road") ? 2f / Mathf.Max(itemBounds.size.x, itemBounds.size.y) : targetHeight / Mathf.Max(.001f, itemBounds.size.z);
            item.transform.localScale *= scale;
            itemBounds = Bounds(Renderers(item).Where(r => !r.name.Contains("LOD1") && !r.name.Contains("LOD2")));
            float gap = itemBounds.size.x * .5f + .75f;
            item.transform.position = new Vector3(gap * .5f - itemBounds.center.x, -itemBounds.center.y, -itemBounds.max.z);
            var heroBounds = Bounds(Renderers(hero));
            hero.transform.position = new Vector3(-gap * .5f - heroBounds.center.x, -heroBounds.center.y, -heroBounds.max.z);
            itemBounds = Bounds(Renderers(item).Where(r => !r.name.Contains("LOD1") && !r.name.Contains("LOD2")));
            heroBounds = Bounds(Renderers(hero));
            var combined = itemBounds; combined.Encapsulate(heroBounds);
            if (name.StartsWith("Wave4_"))
            {
                var existing = Spawn("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab", scene);
                var bounds = Bounds(Renderers(existing));
                existing.transform.position = new Vector3(heroBounds.min.x - bounds.size.x * .5f - .55f - bounds.center.x, -bounds.center.y, -bounds.max.z);
                combined.Encapsulate(Bounds(Renderers(existing)));
            }
            var cameraObject = new GameObject("Gameplay fit camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = Mathf.Max(2.2f, combined.size.x * .4f, combined.size.z * .7f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100; camera.backgroundColor = new Color(.22f,.29f,.24f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = combined.center + new Vector3(0, -14, -20); camera.transform.LookAt(combined.center, Vector3.up);
            var lighting = new GameObject("Fit daylight", typeof(Light)); SceneManager.MoveGameObjectToScene(lighting, scene);
            var light = lighting.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; lighting.transform.rotation = camera.transform.rotation * Quaternion.Euler(0, -20, 0);
            var fillObject = new GameObject("Fit sky fill", typeof(Light)); SceneManager.MoveGameObjectToScene(fillObject, scene);
            var fill = fillObject.GetComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .35f; fill.transform.rotation = Quaternion.Euler(25, 150, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(0, 0, .025f); floor.transform.localScale = Vector3.one * 40;
            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(.43f,.56f,.28f) }; floorMat.SetFloat("_Glossiness",0); floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            try { Save(camera, Evidence + "/" + name + ".png"); }
            finally { Object.DestroyImmediate(floorMat); }
            if (ratios != null)
            {
                float Project(Bounds b) => Mathf.Abs(camera.WorldToViewportPoint(new Vector3(b.center.x, b.center.y, b.min.z)).y - camera.WorldToViewportPoint(new Vector3(b.center.x, b.center.y, b.max.z)).y);
                ratios.Add(FormattableString.Invariant($"{name},{nativeHeight:F5},{scale:F5},{itemBounds.size.z:F5},{itemBounds.size.x:F5},{Project(itemBounds)/Project(heroBounds):F3}"));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static void Save(Camera camera, string path, int width = 960, int height = 600)
    {
        var target = RenderTexture.GetTemporary(width,height,24); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var image = new Texture2D(width,height,TextureFormat.RGB24,false);
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
    }

    static void CaptureGroup(float heroHeight)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            float cursor = 0; var bounds = new Bounds(); bool first = true;
            var specs = new[] { (HeroPath,1f), (Destination+"/Prefabs/Tree01.prefab",3.1f),
                (Destination+"/Prefabs/Tree03.prefab",2.85f), (Destination+"/Prefabs/WoodFence01.prefab",.6f), (Destination+"/Prefabs/SignPost01.prefab",1.1f) };
            foreach (var spec in specs)
            {
                var obj = Spawn(spec.Item1, scene); obj.GetComponent<TownAlly>()?.SetFacing(Facing.Down);
                foreach(var lod in obj.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                var rs = Renderers(obj).Where(r=>!r.name.Contains("LOD1")&&!r.name.Contains("LOD2")).ToArray();
                var b = Bounds(rs); obj.transform.localScale *= heroHeight * spec.Item2 / b.size.z; b = Bounds(rs);
                obj.transform.position = new Vector3(cursor - b.min.x, -b.center.y, -b.max.z); cursor += b.size.x + .6f; b = Bounds(rs);
                if(first) { bounds=b;first=false; } else bounds.Encapsulate(b);
            }
            var camera = new GameObject("Same-depth fit",typeof(Camera)).GetComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
            camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=Mathf.Max(3,bounds.size.x*.35f);
            camera.transform.position=bounds.center+new Vector3(0,-14,-20);camera.transform.LookAt(bounds.center,Vector3.up);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.43f,.56f,.29f);
            var light=new GameObject("Fit key",typeof(Light)).GetComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);
            light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=camera.transform.rotation*Quaternion.Euler(0,-20,0);
            Save(camera,Evidence+"/Environment_Group.png",1280,600);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
