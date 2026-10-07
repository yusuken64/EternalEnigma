using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DioramaItemAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Audit Item Axes")]
    public static void AuditAxes()
    {
        Directory.CreateDirectory("Docs/Art/Previews/Diorama/Items");
        File.WriteAllText("Docs/Art/Previews/Diorama/Items/SourceBounds.txt",string.Join("\n",new[]{"potion_red","potion_blue","waffle","book","shield","bag","treasure chest","hen"}.Select(id=>{
            var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Atlas+id+".prefab"));
            try{return id+" "+Bounds(obj).ToString("F4");}finally{Object.DestroyImmediate(obj);}
        })));
    }
    public const string Atlas="Assets/Art/3D Props - Adorable Items/Adorable 3D Item_Atlas/Prefabs/";
    const string Weapons="Assets/Art/RPGTinyHeroWavePolyart/Mesh/Weapons/";
    const string Folder="Assets/Art/Diorama/Items";
    public static string Safe(string name)=>Regex.Replace(name,"[^A-Za-z0-9_]", "_");
    public static ItemDefinition[] Definitions()=>AssetDatabase.FindAssets("t:ItemDefinition").Select(AssetDatabase.GUIDToAssetPath).Distinct()
        .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ItemDefinition>().OrderBy(i=>i.ItemName,StringComparer.Ordinal).ToArray();

    [MenuItem("Tools/Eternal Enigma/Diorama/Integrate Items")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Prefabs/Dungeon/DroppedItems/Individual");AssetDatabase.Refresh();
        const string path="Assets/Resources/EnvironmentKit/DioramaItems.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<DioramaItemCatalog>(path);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<DioramaItemCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        var definitions=Definitions();var entries=new List<DioramaItemCatalog.Entry>();
        foreach(var item in definitions.Concat(MaterialCatalog.All).GroupBy(i=>i.ItemName).Select(g=>g.First()))
        {
            var (source,tint)=Mapping(item);
            var prefab=Make(source,tint,Safe(item.ItemName),item.DroppedItemVisual,"Assets/Prefabs/Dungeon/DroppedItems/Individual/"+Safe(item.ItemName)+".prefab");
            entries.Add(new DioramaItemCatalog.Entry{Name=item.ItemName,Source=source,Prefab=prefab.GetComponent<DroppedItem>(),Tint=tint});
        }
        string[] fallback={"bow","waffle","gold coin bag2","key","potion_red","ring","shield","skull","book2","@OHS03_Sword","treasure chest"};
        foreach(var value in Enum.GetValues(typeof(DroppedItemVisual)).Cast<DroppedItemVisual>())
            Make(fallback[(int)value],Color.white,value.ToString(),value,"Assets/Prefabs/Dungeon/DroppedItems/"+value+".prefab");
        catalog.Items=entries.ToArray();
        catalog.Props=new[]{"bag","key","skull","treasure chest","exclamation mark","question mark","cake","waffle","egg","potion_red","potion_blue","jar","hen","duck","butterfly","bee","ladybug","trophy"}
            .Select(id=>new DioramaItemCatalog.Prop {Id=id,Prefab=Make(id,Color.white,"Prop_"+Safe(id),null,Folder+"/Prop_"+Safe(id)+".prefab")}).ToArray();
        foreach(var item in definitions)catalog.Apply(item);
        foreach(var item in definitions)EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        UnifiedPresentationAuthoring.Build();
        foreach(var entry in catalog.Items)
        {
            var definition=definitions.FirstOrDefault(i=>i.ItemName==entry.Name);
            entry.Icon=definition!=null?definition.Icon:UnifiedPresentationAuthoring.RenderIcon(entry.Prefab.gameObject,"Item_"+Safe(entry.Name),true,true);
        }
        foreach(var item in MaterialCatalog.All)catalog.Apply(item);
        MigrateChest(catalog.GetProp("treasure chest"));
        RepairPickupBindings();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Docs/Art/Previews/Diorama/Items");
        File.WriteAllText("Docs/Art/Previews/Diorama/Items/Mapping.csv","item,source,tint,prefab\n"+string.Join("\n",catalog.Items.Select(e=>$"{e.Name},{e.Source},{ColorUtility.ToHtmlStringRGB(e.Tint)},{AssetDatabase.GetAssetPath(e.Prefab)}")));
        Debug.Log($"Diorama items: {catalog.Items.Length} distinct mappings, 11 fallbacks, {catalog.Props.Length} atlas props.");
    }

    [MenuItem("Tools/Eternal Enigma/Diorama/Repair Pickup Bindings")]
    public static void RepairPickupBindings()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Dungeon/DroppedItems"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var pickup=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pose=pickup.transform.Find("Floor pose");if(pose==null)continue;
                var bounds=Bounds(pickup);
                // Logical roots are cell corners; the two-unit grid's artwork is
                // centered one unit inside the cell, matching the existing actors.
                pose.localPosition+=new Vector3(1-bounds.center.x,1-bounds.center.y,0);
                PrefabUtility.SaveAsPrefabAsset(pickup,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(pickup);}
        }
        // Currency has its own interaction behaviour. The enum's Gold fallback is
        // still a DroppedItem; sharing its component would break treasure spawning.
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/DroppedItems/Gold.prefab");
        var root=Object.Instantiate(source);Gold gold;
        try
        {
            Object.DestroyImmediate(root.GetComponent<DroppedItem>());
            root.AddComponent<Gold>().DroppedItemVisual=DroppedItemVisual.Gold;
            root.name="Gold pickup";
            gold=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/GoldPickup.prefab").GetComponent<Gold>();
        }
        finally {Object.DestroyImmediate(root);}
        const string dungeonPath="Assets/Prefabs/Dungeon/TileWorldDungeon.prefab";
        var dungeon=PrefabUtility.LoadPrefabContents(dungeonPath);
        try
        {
            var target=dungeon.GetComponent<TileWorldDungeon>();target.GoldPrefab=gold;
            target.DroppedItemPrefabs=Enum.GetValues(typeof(DroppedItemVisual)).Cast<DroppedItemVisual>()
                .Select(v=>AssetDatabase.LoadAssetAtPath<DroppedItem>("Assets/Prefabs/Dungeon/DroppedItems/"+v+".prefab")).ToList();
            if(target.DroppedItemPrefabs.Any(p=>p==null))throw new InvalidOperationException("Missing dropped-item category prefab.");
            target.SmallKeyPrefab=DioramaItemCatalog.Load().GetProp("key");
            PrefabUtility.SaveAsPrefabAsset(dungeon,dungeonPath);
        }
        finally {PrefabUtility.UnloadPrefabContents(dungeon);}
        AssetDatabase.SaveAssets();
    }

    public static (string source,Color tint) Mapping(ItemDefinition item)
    {
        string name=item.ItemName;uint hash=2166136261;foreach(char c in name)hash=(hash^c)*16777619;
        var tint=Color.HSVToRGB(hash%360/360f,.16f,.86f+(hash>>8)%15*.01f);
        if(item is EquipmentItemDefinition equipment)
        {
            string model=equipment.WeaponModelName;
            if(equipment.IsAmmunition||model=="Arrows")return("@Projectile/"+(string.IsNullOrEmpty(equipment.WeaponModelVariant)?"Arrow01":equipment.WeaponModelVariant)+"Projectile",tint);
            if(model=="Bows"||equipment.WeaponType==WeaponType.BowAndArrow)return("bow",tint);
            if(!string.IsNullOrEmpty(model)&&File.Exists(Weapons+model+".fbx"))return("@"+model,tint);
            return("@OHS03_Sword",tint);
        }
        return name switch {
            "Bread"=>("waffle",Color.white),"Charred Bread"=>("waffle",new Color(.23f,.15f,.12f)),"Spoiled Bread"=>("waffle",new Color(.5f,.70f,.28f)),
            "Potion"=>("potion_red",Color.white),"SP Potion"=>("potion_blue",Color.white),"Antidote"=>("jar",new Color(.38f,.86f,.43f)),
            "Sleep Potion"=>("potion_blue",new Color(.75f,.48f,.95f)),"Frailty Potion"=>("potion_red",new Color(.40f,.30f,.44f)),"Oblivion Draught"=>("ink",new Color(.7f,.62f,.86f)),
            "Bolt Spell"=>("book2",new Color(1,.92f,.38f)),"Explosion Spell"=>("book2",new Color(1,.42f,.31f)),"Warp Spell"=>("book",new Color(.68f,.42f,1)),
            "Iron Ore"=>("jewel",new Color(.48f,.56f,.65f)),"Healing Herb"=>("leaf",new Color(.55f,.85f,.48f)),"Timber"=>("log",Color.white),"Trap Parts"=>("wheel",new Color(.66f,.71f,.75f)),
            _ when name.StartsWith("Potion")=>("potion_red",tint),
            _ when name.Contains("Latte")||name.Contains("Coffee")||name.Contains("Espresso")||name.Contains("Brew")||name.Contains("Shakerato")=>("cup",tint),
            _ when name.Contains("Croquette")=>("ham",tint),_ when name.Contains("Pastry")=>("waffle",tint),
            _ when name.Contains("Croissant")||name.Contains("Danish")=>("cake",tint),_ when name.Contains("Scroll")=>("book2",tint),
            _=>(item.DroppedItemVisual==DroppedItemVisual.Bread?"waffle":"book",tint)
        };
    }

    static GameObject Make(string source,Color tint,string name,DroppedItemVisual? category,string path)
    {
        var root=new GameObject(name);var pose=new GameObject("Floor pose");pose.transform.SetParent(root.transform,false);
        try
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source.StartsWith("@")?Weapons+source.Substring(1)+".fbx":Atlas+source+".prefab");
            if(asset==null)throw new InvalidOperationException("Missing item mesh: "+source);
            var model=Object.Instantiate(asset,pose.transform);model.name="Model";
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(animator);
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Lit(source.StartsWith("@")?AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/RPGTinyHeroWavePolyart/Material/DefaultPolyart.mat"):m,tint)).ToArray();
            // Y-up sources become XY/-Z. Slight roll exposes shields/books/waffles
            // to both the gameplay and menu cameras instead of showing an edge.
            pose.transform.localRotation=source.StartsWith("@Projectile")?Quaternion.Euler(-90,20,-25):
                source.StartsWith("@")?Quaternion.Euler(-30,20,-25):
                source=="book2"?Quaternion.Euler(-85,0,-18):
                source is "exclamation mark" or "question mark" or "trophy"?Quaternion.Euler(-45,0,0):
                source is "book" or "waffle" or "leaf" or "shield"?Quaternion.Euler(-20,0,-18):
                category.HasValue?Quaternion.Euler(-45,0,0):Quaternion.Euler(-90,0,0);
            if(source.StartsWith("@Shield"))pose.transform.localRotation*=Quaternion.Euler(0,180,0);
            if(source is "hen" or "duck")pose.transform.localRotation*=Quaternion.Euler(0,180,0);
            var bounds=Bounds(root);float height=bounds.size.z;
            float scale=Mathf.Min(DioramaScale.Pickup/Mathf.Max(.01f,height),DioramaScale.HeroHeight*.72f/Mathf.Max(bounds.size.x,bounds.size.y));
            pose.transform.localScale=Vector3.one*scale;bounds=Bounds(root);
            float center=category.HasValue?1:0;
            pose.transform.localPosition=new Vector3(center-bounds.center.x,center-bounds.center.y,-bounds.max.z-.025f);
            if(category.HasValue)root.AddComponent<DroppedItem>().DroppedItemVisual=category.Value;
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {Object.DestroyImmediate(root);}
    }
    public static Bounds Bounds(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
        foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
    public static Material Lit(Material source,Color tint)
    {
        string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path=Folder+"/"+key+"_"+ColorUtility.ToHtmlStringRGB(tint)+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.mainTexture=source!=null?source.mainTexture:null;material.color=tint;
        material.SetFloat("_Glossiness",.12f);material.SetFloat("_Metallic",0);
        if(source!=null&&source.name.IndexOf("Alpha",StringComparison.OrdinalIgnoreCase)>=0)
        {
            material.SetFloat("_Mode",2);material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);
            material.DisableKeyword("_ALPHATEST_ON");material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
            material.color=new Color(tint.r,tint.g,tint.b,.25f);
        }
        EditorUtility.SetDirty(material);return material;
    }
    static void MigrateChest(GameObject visual)
    {
        string path=AssetDatabase.FindAssets("Enemy_ChestMonster t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Single();
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Mimic only replaces its disguise; the animated monster remains intact.
            root.GetComponent<EnemyBehavior>().ChestVisualPrefab=visual;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
