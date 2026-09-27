using System;
using System.IO;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEditor;
using UnityEngine;
using EternalEnigma.Core.World;
using UnityEditor.SceneManagement;

public static class DungeonThemeAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Capture All Themes")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
        var generator=UnityEngine.Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
        if(generator==null) throw new InvalidOperationException("Open DungeonScene before capturing previews.");
        if(DungeonThemePreview.Building) throw new InvalidOperationException("Wait for the current preview to finish.");
        int index=0;
        Action completed=null;
        void Next()
        {
            if(index==32) {DungeonThemePreview.Completed-=completed;DungeonThemePreview.Clear();Debug.Log("Captured all 32 biome/layout previews.");return;}
            var selection=new DungeonVisualSelection {Biome=(OverworldBiome)(index/4),Environment=(DungeonEnvironmentKind)((index/2)%2)};
            DungeonThemePreview.Build(generator,selection,12345,index%2==1);
        }
        void Tick() {EditorApplication.update-=Tick;Next();}
        completed=()=> {Capture(((OverworldBiome)(index/4))+"_"+((DungeonEnvironmentKind)((index/2)%2))+"_"+(index%2==1?"Throne":"Regular"));Debug.Log("Captured dungeon theme "+index);index++;EditorApplication.update+=Tick;};
        DungeonThemePreview.Completed+=completed;Next();
    }
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Restore Preview")]
    public static void RestorePreview()=>DungeonThemePreview.Clear();
    const string Root="Assets/Art/DungeonThemes";
    const string ResourcesRoot="Assets/Resources/DungeonThemes";
    [Serializable] class MeshSource { public string name; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    [Serializable] class MeshSources { public MeshSource[] meshes; }
    static T Save<T>(T obj,string path) where T:UnityEngine.Object
    {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null) {AssetDatabase.CreateAsset(obj,path);return obj;}
        EditorUtility.CopySerialized(obj,existing);UnityEngine.Object.DestroyImmediate(obj);return existing;
    }
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Install Catalog")]
    public static void Install()
    {
        Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Presets");Directory.CreateDirectory(ResourcesRoot);
        AssetDatabase.Refresh();
        foreach(var source in JsonUtility.FromJson<MeshSources>(File.ReadAllText(Root+"/Source/Modules.json")).meshes)
        {
            // Blender Z-up to Unity XY/-Z is a reflection, so reverse the winding.
            for(int i=0;i<source.triangles.Length;i+=3) (source.triangles[i],source.triangles[i+2])=(source.triangles[i+2],source.triangles[i]);
            var mesh=new Mesh {name=source.name,vertices=source.vertices,uv=source.uv,triangles=source.triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            Save(mesh,(source.name=="CryptRoots" ? ResourcesRoot : Root+"/Meshes")+"/"+source.name+".asset");
        }
        var kit=EnvironmentKit.Load();
        Color[] colors={new(.35f,.46f,.23f),new(.68f,.49f,.26f),new(.37f,.53f,.55f),new(.39f,.40f,.43f),new(.28f,.38f,.22f),new(.68f,.81f,.86f),new(.28f,.34f,.22f),new(.19f,.17f,.20f)};
        string[] names={"Existing dungeon","Sandstone tomb","Flooded ruins","Rock chambers","Root-covered ruins","Ice crypt","Moss crypt","Basalt halls"};
        string[][] props={new[]{"Rock"},new[]{"Rock","Cactus","Palm"},new[]{"Reeds","Rock"},new[]{"Rock","MountainPeak"},new[]{"CryptRoots","Mushrooms"},new[]{"Rock","SnowPine"},new[]{"CryptRoots","Mushrooms","Reeds"},new[]{"Basalt","CharredTree"}};
        var themes=new System.Collections.Generic.List<DungeonTheme>();
        foreach(OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) foreach(DungeonEnvironmentKind kind in Enum.GetValues(typeof(DungeonEnvironmentKind)))
        {
            int i=(int)biome;bool outdoor=kind==DungeonEnvironmentKind.Outdoor;string id=biome+"_"+kind;
            string masonry=biome==OverworldBiome.Volcanic?"BasaltEmbers":biome==OverworldBiome.Marsh?"MossMasonry":biome==OverworldBiome.Tundra?"IceMasonry":"Masonry";
            var wall=Material(id+"_Boundary",masonry=="Masonry" || outdoor&&biome!=OverworldBiome.Volcanic?colors[i]:Color.white,(outdoor&&biome!=OverworldBiome.Volcanic)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Source/"+masonry+".png"));
            var groundColor=biome==OverworldBiome.Water?new Color(.64f,.62f,.48f):outdoor?Color.Lerp(colors[i],new Color(.43f,.34f,.24f),.30f):colors[i]*.85f;
            var ground=Material(id+"_Floor",groundColor,
                outdoor?AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Overworld/Biome"+(biome==OverworldBiome.Water||biome==OverworldBiome.Marsh?OverworldBiome.Desert:biome)+".png"):AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Source/Masonry.png"));
            var accent=Material(id+"_Accent",Color.Lerp(colors[i],Color.white,.2f),null);
            Material pool=null;
            if(biome==OverworldBiome.Water || biome==OverworldBiome.Marsh || biome==OverworldBiome.Volcanic)
                pool=Material(id+"_Pool",biome==OverworldBiome.Volcanic?new Color(.77f,.21f,.045f):biome==OverworldBiome.Marsh?new Color(.19f,.29f,.22f):new Color(.16f,.40f,.52f),null);
            var floorMesh=UnityEngine.Object.Instantiate(kit.Mesh("Paving"));floorMesh.name="Theme paving";
            floorMesh.uv=floorMesh.vertices.Select(v=>new Vector2(v.x+.5f,v.y+.5f)).ToArray();floorMesh=Save(floorMesh,Root+"/Meshes/Paving.asset");
            var floorPiece=Piece(id+"_Floor",floorMesh,ground);
            var floorPreset=Preset(id+"_Floor",floorPiece,floorPiece,floorPiece,floorPiece);
            var accentPiece=Piece(id+"_Accent",floorMesh,accent);
            var accentPreset=Preset(id+"_Accent",accentPiece,accentPiece,accentPiece,accentPiece);
            string family=outdoor || biome==OverworldBiome.Mountain ? "SmartMountain" : "Crypt";
            Mesh M(string suffix)
            {
                var source=family=="Crypt"?AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Crypt"+suffix+".asset"):kit.Mesh(family+suffix);
                if(biome!=OverworldBiome.Forest || outdoor) return source;
                var combined=new Mesh {name="Root-covered "+suffix};
                var roots=AssetDatabase.LoadAssetAtPath<Mesh>(ResourcesRoot+"/CryptRoots.asset");
                combined.CombineMeshes(new[]{new CombineInstance {mesh=source,transform=Matrix4x4.identity},new CombineInstance {mesh=roots,transform=Matrix4x4.TRS(new Vector3(-.1f,-.1f,-.94f),Quaternion.identity,Vector3.one*.45f)}});
                return Save(combined,Root+"/Meshes/Root"+suffix+".asset");
            }
            var boundary=Preset(id+"_Boundary",Piece(id+"_Edge",M("Edge"),wall),Piece(id+"_Outer",M("Outer"),wall),Piece(id+"_Inner",M("Inner"),wall),
                Piece(id+"_Fill",pool!=null?floorMesh:M("Fill"),pool!=null?pool:wall));
            // Both layouts explicitly own a preset reference; artists may diverge the throne preset later.
            var throne=UnityEngine.Object.Instantiate(boundary);throne.name=id+"_Throne";throne=Save(throne,Root+"/Presets/"+id+"_Throne.asset");
            themes.Add(new DungeonTheme {Name=outdoor?biome+" outdoors":names[i],Biome=biome,Environment=kind,
                RegularBoundary=boundary,ThroneBoundary=throne,Floor=floorPreset,Accent=accentPreset,PoolMaterial=pool,
                Decorations=!outdoor&&biome==OverworldBiome.Desert?new[]{"Rock"}:props[i],UseTrees=outdoor,DecorationMaterial=kit.Material(biome),Ambient=Color.Lerp(colors[i],Color.white,outdoor?.55f:.35f),
                LightColor=Color.Lerp(colors[i],Color.white,.8f),LightIntensity=outdoor?1.1f:.8f});
        }
        var catalog=ScriptableObject.CreateInstance<DungeonThemeCatalog>();catalog.Themes=themes.ToArray();catalog=Save(catalog,ResourcesRoot+"/Catalog.asset");
        var generator=UnityEngine.Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
        if(generator!=null) {generator.ThemeCatalog=catalog;EditorUtility.SetDirty(generator);EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);EditorSceneManager.SaveScene(generator.gameObject.scene);}
        AssetDatabase.SaveAssets();Debug.Log("Installed 16 dungeon themes; original TWC templates/presets untouched.");
    }
    static Material Material(string name,Color color,UnityEngine.Texture texture)
    {
        var m=new Material(Shader.Find("Standard")) {name=name,color=color,mainTexture=texture};m.SetFloat("_Glossiness",.08f);
        return Save(m,Root+"/Materials/"+name+".mat");
    }
    static GameObject Piece(string id,Mesh mesh,Material material)
    {
        var go=new GameObject(id);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+id+".prefab");UnityEngine.Object.DestroyImmediate(go);return prefab;
    }
    static TileWorldCreator4TilesPreset Preset(string id,GameObject edge,GameObject outer,GameObject inner,GameObject fill)
    {
        var p=ScriptableObject.CreateInstance<TileWorldCreator4TilesPreset>();p.name=id;p.edgeTile=edge;p.exteriorCornerTile=outer;p.interiorCornerTile=inner;p.fillTile=fill;
        return Save(p,Root+"/Presets/"+id+".asset");
    }
    [MenuItem("Tools/Eternal Enigma/Dungeon Themes/Capture Baseline")]
    public static void CaptureBaseline()
    {
        Directory.CreateDirectory("Docs/Art/Previews/DungeonThemes");
        var generator = UnityEngine.Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
        foreach (var creator in new[] { generator.TileWorldCreator, generator.ThroneTileWorldCreator })
        {
            var a = creator.twcAsset;
            Debug.Log(a.name + " world=" + a.worldName + " size=" + a.cellSize + " layers: " + string.Join("; ", a.mapBuildLayers.Select(l => l.layerName + ":" + l.GetType().Name + (l is InstantiateTiles t ? " presets=" + string.Join(",",t.tiles.Select(p=>p.preset != null ? p.preset.name : "null")) : ""))));
        }
        Capture("Baseline");
    }
    public static void Capture(string name)
    {
        var go = new GameObject("Theme capture camera");
        var camera = go.AddComponent<Camera>();
        var world = DungeonThemePreview.World != null ? DungeonThemePreview.World : GameObject.Find("TileWorldCreator_Map");
        var renderers = world.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        camera.orthographic=true; camera.orthographicSize=Mathf.Max(bounds.size.y*.55f,bounds.size.x*.55f/1.6f);
        camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-100);
        camera.farClipPlane=300; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.08f,.10f);
        var old = camera.targetTexture;
        var rt = RenderTexture.GetTemporary(1280, 800, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280,800,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1280,800),0,0); texture.Apply(); File.WriteAllBytes("Docs/Art/Previews/DungeonThemes/"+name+".png",texture.EncodeToPNG()); }
        finally { camera.targetTexture=old; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
}


