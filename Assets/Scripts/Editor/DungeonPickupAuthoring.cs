using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DungeonPickupAuthoring
{
    public const string TablePath = "Assets/Resources/DungeonThemes/PickupPresentation.asset";
    public const string Folder = "Assets/Art/DungeonThemes/Pickups";
    public const string HeroModelPath = "Assets/Prefabs/Town/Allies/Ally_MC03.prefab";
    public const string HeroRigPath = "Assets/Prefabs/Dungeon/Ally.prefab";
    const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Production moves the town model into the dungeon rig, whose visual parent has
    // its own scale. Measuring the town prefab alone underestimates dungeon heroes.
    public static GameObject SpawnHero(Scene scene)
    {
        var source = DioramaFitAuthoring.Spawn(HeroModelPath,scene);
        var hero = DioramaFitAuthoring.Spawn(HeroRigPath,scene);
        PrefabUtility.UnpackPrefabInstance(source,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        PrefabUtility.UnpackPrefabInstance(hero,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var placeholder = hero.transform.Find("GameObject/RPGHeroHP");
        hero.GetComponent<Ally>().ReplaceModel(placeholder.gameObject,source.GetComponent<TownAlly>().AnimatedModel);
        Object.DestroyImmediate(placeholder.gameObject);
        Object.DestroyImmediate(source);
        return hero;
    }

    // Copy the production camera and let its existing controller establish its runtime pose.
    // No scene, controller, hero asset, icon or shared material is saved by this workflow.
    public static Camera GameplayCamera(Scene destination, Vector3 target, out Vector3 offset)
    {
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/DungeonScene.unity");
        try
        {
            var original = source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CameraController>(true)).First();
            var host = new GameObject("Gameplay camera copy"); SceneManager.MoveGameObjectToScene(host,destination);
            var camera = host.AddComponent<Camera>(); camera.CopyFrom(original.Camera); camera.enabled = false; camera.scene = destination;
            var controller = host.AddComponent<CameraController>(); controller.Camera = camera; controller.CameraOffset = original.CameraOffset;
            var focus = new GameObject("Capture focus"); SceneManager.MoveGameObjectToScene(focus,destination); focus.transform.position = target;
            controller.SetFollowTarget(focus.transform);
            typeof(CameraController).GetMethod("Start",Methods).Invoke(controller,null);
            typeof(CameraController).GetMethod("SnapToFollowTarget",Methods).Invoke(controller,null);
            offset = controller.CameraOffset;
            Object.DestroyImmediate(controller); Object.DestroyImmediate(focus);
            camera.aspect = 1.6f;
            return camera;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }

    [MenuItem("Tools/Eternal Enigma/Dungeon Smart Layers/Calibrate Pickups")]
    public static void Calibrate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(DungeonSmartAuthoring.Evidence);
        AssetDatabase.Refresh();
        var catalog = DioramaItemCatalog.Load();
        // Shared shop props get dedicated dungeon adapters, including the mimic's disguise.
        CopyAdapter(catalog.GetProp("key"),"SmallKey");
        CopyAdapter(catalog.GetProp("treasure chest"),"ChestDisguise");
        CopyAdapter(catalog.GetProp("bag"),"Bag");
        var table = DungeonSmartAuthoring.LoadOrCreate<DungeonPickupPresentation>(TablePath);
        var previous = table.Items.ToDictionary(e=>AssetDatabase.GetAssetPath(e.Prefab));
        var entries = new List<DungeonPickupPresentation.Entry>();
        foreach (var item in catalog.Items) Add(item.Name,item.Source,item.Prefab.gameObject,Vector2.one);
        string[] fallbacks = { "bow","waffle","gold coin bag2","key","potion_red","ring","shield","skull","book2","@OHS03_Sword","treasure chest" };
        foreach (DroppedItemVisual value in Enum.GetValues(typeof(DroppedItemVisual)))
            Add("Category: "+value,fallbacks[(int)value],AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/DroppedItems/"+value+".prefab"),Vector2.one);
        Add("Currency","gold coin bag2",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Diorama/Items/GoldPickup.prefab"),Vector2.one);
        Add("Small key","key",AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/SmallKey.prefab"),Vector2.zero);
        Add("Chest disguise","treasure chest",AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ChestDisguise.prefab"),Vector2.zero);
        Add("Bag","bag",AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Bag.prefab"),Vector2.zero);
        var disguise = entries.Single(e=>e.Name=="Chest disguise");
        var enemy = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_ChestMonster.prefab");
        disguise.ParentScale = enemy.VisualParent.transform.lossyScale;
        disguise.CellCenter = Vector2.one - (Vector2)enemy.VisualParent.transform.localPosition;
        void Add(string name,string source,GameObject prefab,Vector2 center)
        {
            if (prefab == null) throw new InvalidOperationException("Missing dungeon pickup: "+name);
            if (previous.TryGetValue(AssetDatabase.GetAssetPath(prefab),out var entry)) { entries.Add(entry); return; }
            entry = Defaults(name,source,prefab); entry.CellCenter = center; entries.Add(entry);
        }
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camera = GameplayCamera(scene,Vector3.zero,out var offset);
            var hero = SpawnHero(scene);
            var heroPoints = Points(hero); var heroScreen = Project(camera,heroPoints);
            float heroScreenHeight = heroScreen.Max(p=>p.y)-heroScreen.Min(p=>p.y);
            float heroHeight = heroPoints.Max(p=>p.z)-heroPoints.Min(p=>p.z);
            table.HeroPrefab = HeroModelPath; table.HeroRigPrefab = HeroRigPath; table.HeroHeight = heroHeight;
            table.HeroScreenHeight = heroScreenHeight; table.CameraOffset = offset; table.OrthographicSize = camera.orthographicSize;
            foreach (var entry in entries)
            {
                string path = AssetDatabase.GetAssetPath(entry.Prefab);
                var obj = PrefabUtility.LoadPrefabContents(path);
                var originalScale = obj.transform.localScale;
                try
                {
                    // A mimic's visual lives under the existing enemy rig. Bake only the
                    // visual child's compensation, preserving the adapter's root transform.
                    obj.transform.localScale = Vector3.Scale(originalScale,entry.ParentScale);
                    var pose = obj.transform.Find("Floor pose");
                    if (pose == null || pose.childCount != 1) throw new InvalidOperationException("Unexpected floor visual: "+path);
                    pose.localRotation = Quaternion.Euler(entry.Euler); pose.localScale = Vector3.one;
                    pose.GetChild(0).localScale = entry.Shape;
                    if (entry.Size == DungeonPickupSize.Elongated)
                        FitEquipmentPose(obj,pose,entry,camera,heroScreenHeight,heroHeight);
                    var points = Points(obj);
                    float scale = entry.Size == DungeonPickupSize.Chest
                        ? heroHeight * entry.TargetRatio / (points.Max(p=>p.z)-points.Min(p=>p.z))
                        : heroScreenHeight * entry.TargetRatio / Diameter(Project(camera,points));
                    pose.localScale = Vector3.one * scale;
                    var bounds = VisualBounds(obj);
                    // A .18-unit margin within a two-unit cell, measured on actual geometry.
                    if (Mathf.Max(bounds.size.x,bounds.size.y)>1.64f)
                        pose.localScale *= 1.64f / Mathf.Max(bounds.size.x,bounds.size.y);
                    bounds = VisualBounds(obj);
                    points = Points(obj);
                    pose.position += new Vector3(entry.CellCenter.x-bounds.center.x,entry.CellCenter.y-bounds.center.y,-.025f-points.Max(p=>p.z));
                    points = Points(obj);
                    var footprint = pose.GetComponent<DungeonPickupFootprint>() ?? pose.gameObject.AddComponent<DungeonPickupFootprint>();
                    footprint.SupportPoint = pose.InverseTransformPoint(points.OrderByDescending(p=>p.z).First());
                    entry.BakedScale = pose.localScale.x;
                    entry.ProjectedRatio = Diameter(Project(camera,points))/heroScreenHeight;
                    entry.WorldHeightRatio = (points.Max(p=>p.z)-points.Min(p=>p.z))/heroHeight;
                    obj.transform.localScale = originalScale;
                    PrefabUtility.SaveAsPrefabAsset(obj,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(obj); }
            }
            table.Items = entries.ToArray(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets();
            BindAdapters();
            WriteTable(table);
            Debug.Log($"Calibrated {entries.Count} dungeon floor visuals against unchanged hero H={heroHeight:F6}, camera size={camera.orthographicSize:F6}.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void FitEquipmentPose(GameObject obj,Transform pose,DungeonPickupPresentation.Entry entry,Camera camera,float heroScreenHeight,float heroHeight)
    {
        var local = Points(obj).Select(p=>pose.InverseTransformPoint(p)).ToArray();
        var start = Quaternion.Euler(entry.Euler);
        float bestRatio=0;var bestRotation=start;
        // Search the closest readable tilt which reaches its projected target inside a
        // cell. Persist the chosen angle in this item's editable row; never shrink a
        // long weapon silently below the reference range just to satisfy footprint.
        var rotations = (from x in Enumerable.Range(-6,13)
                         from y in Enumerable.Range(-6,13)
                         from z in Enumerable.Range(-3,7)
                         let rotation = Quaternion.Euler(entry.Euler+new Vector3(x*15,y*15,z*15))
                         orderby Quaternion.Angle(start,rotation)
                         select rotation);
        foreach(var rotation in rotations)
        {
            var matrix = pose.parent.localToWorldMatrix*Matrix4x4.TRS(pose.localPosition,rotation,Vector3.one);
            var points = local.Select(matrix.MultiplyPoint3x4).ToArray();
            float scale = heroScreenHeight*entry.TargetRatio/Diameter(Project(camera,points));
            pose.localRotation=rotation;
            var bounds=VisualBounds(obj);
            float width = bounds.size.x,depth=bounds.size.y;
            float height = points.Max(p=>p.z)-points.Min(p=>p.z);
            float available=Mathf.Min(1.639f/Mathf.Max(width,depth),heroHeight/Mathf.Max(.001f,height));
            float feasible=Diameter(Project(camera,points))*available/heroScreenHeight;
            if(feasible>bestRatio){bestRatio=feasible;bestRotation=rotation;}
            if(Mathf.Max(width,depth)*scale>1.639f || height*scale>heroHeight)continue;
            pose.localRotation=rotation;entry.Euler=rotation.eulerAngles;return;
        }
        if(bestRatio>=.51f)
        {
            pose.localRotation=bestRotation;entry.Euler=bestRotation.eulerAngles;
            entry.TargetRatio=Mathf.Min(entry.TargetRatio,bestRatio*.995f);return;
        }
        throw new InvalidOperationException("No cell-safe reference pose for "+entry.Name+"; maximum ratio "+bestRatio);
    }

    public static Bounds VisualBounds(GameObject obj)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var filter in obj.GetComponentsInChildren<MeshFilter>())
        {
            var source=filter.sharedMesh.bounds;
            foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
            {
                var p=filter.transform.TransformPoint(source.center+Vector3.Scale(source.extents,new Vector3(x,y,z)));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }

    static DungeonPickupPresentation.Entry Defaults(string name,string source,GameObject prefab)
    {
        var entry = new DungeonPickupPresentation.Entry { Name=name,Source=source,Prefab=prefab,
            Euler=prefab.transform.Find("Floor pose").localEulerAngles,TargetRatio=.43f };
        if (source.StartsWith("@Projectile"))
        { entry.Size=DungeonPickupSize.Elongated;entry.TargetRatio=.57f;entry.Euler=new Vector3(-90,18,-28);entry.Shape=new Vector3(2.8f,2.8f,1); }
        else if (source.StartsWith("@Wand"))
        { entry.Size=DungeonPickupSize.Elongated;entry.TargetRatio=.58f;entry.Euler=new Vector3(-25,20,-38);entry.Shape=new Vector3(1.5f,1,1.5f); }
        else if (source.StartsWith("@Spear"))
        { entry.Size=DungeonPickupSize.Elongated;entry.TargetRatio=.58f;entry.Euler=new Vector3(-24,20,-38);entry.Shape=new Vector3(1.65f,1,1.65f); }
        else if (source.StartsWith("@") && !source.StartsWith("@Shield"))
        { entry.Size=DungeonPickupSize.Elongated;entry.TargetRatio=source.Contains("Axe")||source.Contains("Hammer")?.57f:.58f;entry.Euler=new Vector3(-25,20,-32);entry.Shape=new Vector3(1.25f,1,1.6f); }
        else if (source=="bow") {entry.Size=DungeonPickupSize.Elongated;entry.TargetRatio=.58f;}
        else if (source=="treasure chest") {entry.Size=DungeonPickupSize.Chest;entry.TargetRatio=.5f;entry.Euler=new Vector3(-90,0,-12);entry.Shape=new Vector3(1.22f,1.8f,1.1f);}
        else if (source is "key" or "ring") entry.TargetRatio=.37f;
        else if (source is "book" or "book2") entry.TargetRatio=.43f;
        else if (source is "bag" or "gold coin bag2") entry.TargetRatio=.43f;
        else if (source=="shield" || source.StartsWith("@Shield")) entry.TargetRatio=.43f;
        else if (source is "leaf" or "jewel" or "wheel") entry.TargetRatio=.39f;
        return entry;
    }
    static void CopyAdapter(GameObject source,string name)
    {
        string path=Folder+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)==null && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path))
            throw new InvalidOperationException("Cannot create dungeon adapter: "+name);
    }
    static void BindAdapters()
    {
        string path="Assets/Prefabs/Dungeon/TileWorldDungeon.prefab";
        var dungeon=PrefabUtility.LoadPrefabContents(path);
        try { dungeon.GetComponent<TileWorldDungeon>().SmallKeyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/SmallKey.prefab");PrefabUtility.SaveAsPrefabAsset(dungeon,path); }
        finally { PrefabUtility.UnloadPrefabContents(dungeon); }
        path=AssetDatabase.FindAssets("Enemy_ChestMonster t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Single();
        var chest=PrefabUtility.LoadPrefabContents(path);
        try { chest.GetComponent<EnemyBehavior>().ChestVisualPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ChestDisguise.prefab");PrefabUtility.SaveAsPrefabAsset(chest,path); }
        finally { PrefabUtility.UnloadPrefabContents(chest); }
        AssetDatabase.SaveAssets();
    }
    public static Vector3[] Points(GameObject root)
    {
        var points=new List<Vector3>();
        foreach (var r in DioramaFitAuthoring.Renderers(root))
        {
            var mesh = r is SkinnedMeshRenderer skin ? new Mesh() : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh==null) continue;
            try
            {
                if (r is SkinnedMeshRenderer skinned) skinned.BakeMesh(mesh);
                points.AddRange(Vertices(mesh).Select(v=>r.transform.TransformPoint(v)));
            }
            finally { if(r is SkinnedMeshRenderer)Object.DestroyImmediate(mesh); }
        }
        return points.ToArray();
    }
    static Vector3[] Vertices(Mesh mesh)
    {
        if(mesh.isReadable)return mesh.vertices;
        // Editor MeshUtility reads imported GPU meshes during Play Mode without
        // changing the hero/vendor importer's Read/Write setting.
        using(var data=MeshUtility.AcquireReadOnlyMeshData(mesh))
        using(var vertices=new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount,Unity.Collections.Allocator.Temp))
        {data[0].GetVertices(vertices);return vertices.ToArray();}
    }
    public static Vector2[] Project(Camera camera,Vector3[] points) => points.Select(p=>{
        var screen=camera.WorldToViewportPoint(p);return new Vector2(screen.x*camera.aspect,screen.y);
    }).ToArray();
    public static float Diameter(Vector2[] points)
    {
        var sorted=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
        if(sorted.Length<2)return 0;
        float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        var hull=new List<Vector2>();
        foreach(var p in sorted){while(hull.Count>1&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=sorted.Length-2;i>=0;i--){var p=sorted[i];while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        float distance=0;foreach(var a in hull)foreach(var b in hull)distance=Mathf.Max(distance,(a-b).sqrMagnitude);
        return Mathf.Sqrt(distance);
    }
    static void WriteTable(DungeonPickupPresentation table)
    {
        var text=new StringBuilder("name,source,size,target_ratio,projected_diameter_ratio,world_height_ratio,scale,rotation,shape,prefab\n");
        foreach(var e in table.Items)text.AppendLine($"\"{e.Name}\",{e.Source},{e.Size},{e.TargetRatio:F4},{e.ProjectedRatio:F4},{e.WorldHeightRatio:F4},{e.BakedScale:F6},\"{e.Euler}\",\"{e.Shape}\",{AssetDatabase.GetAssetPath(e.Prefab)}");
        File.WriteAllText(DungeonSmartAuthoring.Evidence+"/PickupAdjustments.csv",text.ToString());
    }
}
