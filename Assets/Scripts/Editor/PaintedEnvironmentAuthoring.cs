using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;

/// <summary>Reproducible texture packing and UV integration; no runtime allocations or new material slots.</summary>
public static class PaintedEnvironmentAuthoring
{
    const string Source = "ArtSource/PaintedEnvironment/";
    const string Output = "Assets/Art/PaintedEnvironment/";
    const string KitTextures = "Assets/Art/EnvironmentKit/Textures/";
    static readonly string[] Biomes = { "Grassland", "Desert", "Water", "Mountain", "Forest", "Tundra", "Marsh", "Volcanic" };
    static readonly Color[] StoneTints = { new Color(.88f,.91f,.86f), new Color(1,.81f,.55f), new Color(.78f,.91f,.91f), new Color(.80f,.85f,.96f), new Color(.70f,.83f,.61f), new Color(.83f,.94f,1), new Color(.73f,.79f,.65f), new Color(.53f,.51f,.55f) };
    [Serializable] public sealed class MeshSource { public string path; public Vector2[] uv; }
    [Serializable] public sealed class MeshSources { public List<MeshSource> meshes = new List<MeshSource>(); }
    static MeshSources originals;


    [MenuItem("Tools/Eternal Enigma/Painted Environment/Update Assignments")]
    public static void UpdateAssignments()
    {
        Materials(); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Eternal Enigma/Painted Environment/Update Ground")]
    public static void UpdateGround()
    {
        foreach(string biome in Biomes) WriteGround(biome);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Eternal Enigma/Painted Environment/Update Grassland Ground")]
    public static void UpdateGrasslandGround()
    {
        WriteGround("Grassland");
        AssetDatabase.SaveAssets();
    }

    static void WriteGround(string biome)
    {
        var texture=Read(Source+"Ground_"+biome+".png");
        try {WriteSurface(texture,new Rect(0,0,1,1),Output+"Ground_"+biome+".png",Color.white);}
        finally {UnityEngine.Object.DestroyImmediate(texture);}
    }

    [MenuItem("Tools/Eternal Enigma/Painted Environment/Update Atlases")]
    public static void UpdateAtlases()
    {
        var art=Read(Source+"Atlas.png");var reference=Read(Source+"Palettes/Grassland.png");
        try
        {
            foreach(string biome in Biomes)
            {
                var palette=Read(Source+"Palettes/"+biome+".png");
                try {WriteAtlas(art,palette,reference,KitTextures+"Buildings_"+biome+".png");}
                finally {UnityEngine.Object.DestroyImmediate(palette);}
            }
        }
        finally {UnityEngine.Object.DestroyImmediate(art);UnityEngine.Object.DestroyImmediate(reference);}
    }

    [MenuItem("Tools/Eternal Enigma/Painted Environment/Integrate")]
    public static void Integrate()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit play mode before authoring assets.");
        Directory.CreateDirectory(Output);
        // Preserve the original semantic palette values and UVs for deterministic reruns.
        Directory.CreateDirectory(Source + "Palettes");
        foreach (string biome in Biomes)
        {
            string copy = Source + "Palettes/" + biome + ".png";
            if (!File.Exists(copy)) File.Copy(KitTextures + "Palette_" + biome + ".png", copy);
        }
        originals = File.Exists(Source + "OriginalUVs.json") ? JsonUtility.FromJson<MeshSources>(File.ReadAllText(Source + "OriginalUVs.json")) : new MeshSources();
        var floor = Read(Source + "Floor.png"); var wall = Read(Source + "Wall.png");
        var atlas = Read(Source + "Atlas.png"); var water = Read(Source + "Water.png");
        var reference = Read(Source + "Palettes/Grassland.png");
        try
        {
            WriteSurface(floor, new Rect(0,0,1,1), Output + "Floor.png", Color.white);
            WriteSurface(wall, new Rect(0,0,1,1), Output + "Wall.png", Color.white);
            WriteSurface(water, new Rect(0,0,1,1), Output + "Water.png", Color.white);
            WriteSurface(floor, new Rect(0,0,1,1), KitTextures + "MedievalPaving.png", new Color(.91f,.88f,.80f));
            WriteSurface(floor, new Rect(0,0,1,1), KitTextures + "SmartRoad.png", new Color(.88f,.83f,.72f));
            for (int b=0; b<Biomes.Length; b++)
            {
                string biome = Biomes[b];
                var palette = Read(Source + "Palettes/" + biome + ".png");
                WriteAtlas(atlas, palette, reference, KitTextures + "Buildings_" + biome + ".png");
                // Identical region meanings let both families use the same newly painted atlas.
                // Keep existing GUIDs, material sharing, and all catalog references.
                UnityEngine.Object.DestroyImmediate(palette);
            }
            UpdateGround();
            // Existing four wall family texture GUIDs remain valid for other themed users.
            WriteSurface(wall,new Rect(0,0,1,1),"Assets/Art/DungeonThemes/Source/Masonry.png",Color.white);
            WriteSurface(wall,new Rect(0,0,1,1),"Assets/Art/DungeonThemes/Source/MossMasonry.png",new Color(.73f,.87f,.64f));
            WriteSurface(wall,new Rect(0,0,1,1),"Assets/Art/DungeonThemes/Source/IceMasonry.png",new Color(.80f,.94f,1));
            WriteSurface(wall,new Rect(0,0,1,1),"Assets/Art/DungeonThemes/Source/BasaltEmbers.png",new Color(.50f,.47f,.48f));
            AssetDatabase.Refresh();
            Materials();
            Meshes();
            PropAtlas(atlas,"Assets/Resources/DungeonProps",new[]{0,1,10,8,3,15,6,12});
            PropAtlas(atlas,"Assets/Resources/BiomeDecorations",new[]{10,1,8,11,6,3,15,12,12,13,10,12});
            File.WriteAllText(Source + "OriginalUVs.json", JsonUtility.ToJson(originals));
            AssetDatabase.SaveAssets();
            Debug.Log("Painted environment integration complete. Shared materials and topology preserved.");
        }
        finally { foreach(var t in new[]{floor,wall,atlas,water,reference}) UnityEngine.Object.DestroyImmediate(t); }
    }
    static Texture2D Read(string path)
    {
        var t = new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!t.LoadImage(File.ReadAllBytes(path))) throw new IOException(path);
        return t;
    }
    public static void WriteSurface(Texture2D source, Rect region, string path, Color tint)
    {
        const int n=1024;
        var pixels=new Color[n*n];
        // Resample newly painted art; periodic edge crossfade makes opposite border samples identical.
        // Only packing/resampling happens here: no synthesized noise or replacement painted detail.
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float u=x/(float)(n-1),v=y/(float)(n-1);
            float wx=Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)/.035f),wy=Mathf.SmoothStep(0,1,Mathf.Min(v,1-v)/.035f);
            Color a=Sample(u,v),b=Sample(1-u,v),c=Sample(u,1-v),d=Sample(1-u,1-v);
            Color row=Color.Lerp((a+b)*.5f,a,wx),other=Color.Lerp((c+d)*.5f,c,wx);
            var color=Color.Lerp((row+other)*.5f,row,wy)*tint; color.a=1; pixels[y*n+x]=color;
        }
        Save(pixels,n,path,false);
        Color Sample(float u,float v)=>source.GetPixelBilinear(region.x+(0.007f+u*.986f)*region.width,region.y+(0.007f+v*.986f)*region.height);
    }
    static void WriteAtlas(Texture2D source,Texture2D palette,Texture2D reference,string path)
    {
        const int n=2048,cell=512,pad=24;
        var pixels=new Color[n*n];
        for(int cy=0;cy<4;cy++) for(int cx=0;cx<4;cx++)
        {
            Color target=palette.GetPixelBilinear((cx+.5f)/4,(cy+.5f)/4),basis=reference.GetPixelBilinear((cx+.5f)/4,(cy+.5f)/4);
            Color ratio=new Color(Mathf.Clamp(target.r/Mathf.Max(.04f,basis.r),.15f,4),Mathf.Clamp(target.g/Mathf.Max(.04f,basis.g),.15f,4),Mathf.Clamp(target.b/Mathf.Max(.04f,basis.b),.15f,4));
            for(int y=0;y<cell;y++) for(int x=0;x<cell;x++)
            {
                float u=Mathf.Clamp01((x-pad)/(float)(cell-1-pad*2)),v=Mathf.Clamp01((y-pad)/(float)(cell-1-pad*2));
                var color=source.GetPixelBilinear((cx+.02f+u*.96f)/4,(cy+.02f+v*.96f)/4)*ratio;
                color.a=1;pixels[(cy*cell+y)*n+cx*cell+x]=color;
            }
        }
        Save(pixels,n,path,true);
    }
    static void Save(Color[] colors,int size,string path,bool atlas)
    {
        var t=new Texture2D(size,size,TextureFormat.RGB24,false);t.SetPixels(colors);t.Apply();
        File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);Import(path,atlas);
    }
    static void Import(string path,bool atlas)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);
        i.textureType=TextureImporterType.Default;i.mipmapEnabled=true;i.filterMode=FilterMode.Trilinear;i.anisoLevel=4;
        i.wrapMode=atlas?TextureWrapMode.Clamp:TextureWrapMode.Repeat;i.maxTextureSize=atlas?2048:1024;
        i.textureCompression=TextureImporterCompression.CompressedHQ;i.isReadable=false;i.SaveAndReimport();
    }
    static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Output+name+".png");
    static void Set(Material m,Texture texture,Color color,float scale=1)
    {
        m.mainTexture=texture;m.mainTextureScale=Vector2.one*scale;m.mainTextureOffset=Vector2.zero;m.color=color;
        if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.08f);
        EditorUtility.SetDirty(m);
    }
    static void Materials()
    {
        var kit=EnvironmentKit.Load();
        foreach(var palette in kit.Palettes)
        {
            string biome=palette.Biome.ToString();
            Set(palette.Props,AssetDatabase.LoadAssetAtPath<Texture2D>(KitTextures+"Buildings_"+biome+".png"),Color.white);
            Set(palette.Buildings,AssetDatabase.LoadAssetAtPath<Texture2D>(KitTextures+"Buildings_"+biome+".png"),Color.white);
            Set(palette.Ground,Texture("Ground_"+biome),Color.white);
            palette.Ground.SetOverrideTag("EnvironmentProjection","Planar");
        }
        foreach(string path in Directory.GetFiles("Assets/Art/DungeonThemes/Materials","*.mat"))
        {
            string name=Path.GetFileNameWithoutExtension(path);var parts=name.Split('_');
            int biome=Array.IndexOf(Biomes,parts[0]);if(biome<0||parts.Length<3)continue;
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            bool outdoor=parts[1]=="Outdoor",boundary=parts[2]=="Boundary",accent=parts[2]=="Accent";
            Color tint=StoneTints[biome];
            if(accent) tint=Color.Lerp(StoneTints[biome],new Color(.64f,.49f,.39f),.35f);
            string masonry = parts[0] switch
            {
                "Forest" or "Marsh" => "MossMasonry", "Tundra" => "IceMasonry",
                "Volcanic" => "BasaltEmbers", _ => "Masonry"
            };
            var surface = boundary && !outdoor
                ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/DungeonThemes/Source/" + masonry + ".png")
                : Texture(outdoor&&!accent?"Ground_"+parts[0]:boundary?"Wall":"Floor");
            Set(m,surface,outdoor&&!accent?Color.white:tint,boundary?1: .5f);
            m.SetOverrideTag("EnvironmentProjection",boundary?"Box":"Planar");
        }
        for(int b=0;b<Biomes.Length;b++)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Overworld/Biome"+Biomes[b]+".mat");
            if(m!=null) Set(m,Texture("Ground_"+Biomes[b]),Color.white,.5f);
        }
        foreach(var material in new[]{kit.Paving,kit.Road}) {material.SetOverrideTag("EnvironmentProjection","Planar");EditorUtility.SetDirty(material);}
        Set(AssetDatabase.LoadAssetAtPath<Material>("Assets/Overworld/BiomeBarrier.mat"),Texture("Ground_Mountain"),Color.white,.5f);
        // The ocean shader, secondary noise layer, animation speeds and shoreline masks are untouched.
        var ocean=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/EnvironmentKit/Materials/Ocean.mat");
        ocean.mainTexture=Texture("Water");ocean.color=new Color(.8f,.9f,1);EditorUtility.SetDirty(ocean);
        foreach(string name in new[]{"Shore","Shoreline"})
        {
            var shore=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/EnvironmentKit/Materials/"+name+".mat");
            shore.SetTexture("_MainTex",Texture("Water"));EditorUtility.SetDirty(shore);
        }
        var bridge=AssetDatabase.LoadAssetAtPath<Material>("Assets/Overworld/BiomeBridge.mat");
        var wood=Read(Source+"Atlas.png");
        try {WriteSurface(wood,new Rect(.25f,0,.25f,.25f),Output+"Bridge.png",Color.white);}
        finally {UnityEngine.Object.DestroyImmediate(wood);}
        Set(bridge,Texture("Bridge"),Color.white,.5f);
    }
    static Vector2[] Original(Mesh mesh)
    {
        string path=AssetDatabase.GetAssetPath(mesh);var entry=originals.meshes.FirstOrDefault(m=>m.path==path);
        if(entry==null){entry=new MeshSource{path=path,uv=mesh.uv};originals.meshes.Add(entry);}
        return entry.uv;
    }
    static void Meshes()
    {
        var kit=EnvironmentKit.Load();
        foreach(var model in kit.Models)
        {
            // Roads/shorelines use continuous UVs and masks, not palette swatches.
            if(model.Id=="Paving"||model.Id.StartsWith("SmartRoad")||model.Id.StartsWith("SmartShore"))continue;
            var mesh=model.Mesh;var old=Original(mesh);var uv=(Vector2[])old.Clone();var v=mesh.vertices;var normals=mesh.normals;
            bool buildings=model.Id.StartsWith("SmartHouse")||new[]{"House","Inn","Shop","Trainer"}.Contains(model.Id);
            for(int i=0;i<v.Length;i++)
            {
                // Already textured building regions retain their semantic mapping.
                if(buildings) {int cx=Mathf.Clamp((int)(old[i].x*4),0,3),cy=Mathf.Clamp((int)(old[i].y*4),0,3);uv[i]=new Vector2(Mathf.Clamp(old[i].x,(cx+.055f)/4,(cx+.945f)/4),Mathf.Clamp(old[i].y,(cy+.055f)/4,(cy+.945f)/4));continue;}
                int x=Mathf.Clamp((int)(old[i].x*4),0,3),y=Mathf.Clamp((int)(old[i].y*4),0,3);
                // Legacy wall caps used the plaster swatch. Match the stone body.
                if ((model.Id == "Wall" || model.Id.StartsWith("SmartWall") || model.Id == "Gate") && x == 2 && y == 0) x = 3;
                Vector2 p=Project(v[i],normals[i],mesh.bounds);
                uv[i]=new Vector2((x+.065f+p.x*.87f)/4,(y+.065f+p.y*.87f)/4);
            }
            mesh.uv=uv;EditorUtility.SetDirty(mesh);
        }
        foreach(string path in Directory.GetFiles("Assets/Art/DungeonThemes/Meshes","*.asset"))
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null||!mesh.name.StartsWith("Crypt"))continue;
            Original(mesh);var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++)
            {var a=n[i]; uv[i]=Mathf.Abs(a.z)>=Mathf.Max(Mathf.Abs(a.x),Mathf.Abs(a.y))?new Vector2(v[i].x,v[i].y):Mathf.Abs(a.x)>Mathf.Abs(a.y)?new Vector2(v[i].y,-v[i].z):new Vector2(v[i].x,-v[i].z);}
            mesh.uv=uv;EditorUtility.SetDirty(mesh);
        }
    }
    static Vector2 Project(Vector3 v,Vector3 n,Bounds bounds)
    {
        var p=v-bounds.min;var size=bounds.size;
        p=new Vector3(p.x/Mathf.Max(size.x,.001f),p.y/Mathf.Max(size.y,.001f),1-p.z/Mathf.Max(size.z,.001f));
        return Mathf.Abs(n.z)>=Mathf.Max(Mathf.Abs(n.x),Mathf.Abs(n.y))?new Vector2(p.x,p.y):Mathf.Abs(n.x)>Mathf.Abs(n.y)?new Vector2(p.y,p.z):new Vector2(p.x,p.z);
    }
    static void PropAtlas(Texture2D art,string folder,int[] sourceCells)
    {
        var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Palette.asset");
        const int size=2048,cell=512,pad=24;var pixels=new Color[size*size];
        for(int index=0;index<16;index++)
        {
            int semantic=Mathf.Min(index,sourceCells.Length-1),source=sourceCells[semantic];
            Color target=palette.GetPixel(semantic,0);
            // Preserve identifying prop colors while carrying the painted luminance and wear.
            for(int y=0;y<cell;y++)for(int x=0;x<cell;x++)
            {
                float u=Mathf.Clamp01((x-pad)/(float)(cell-1-2*pad)),v=Mathf.Clamp01((y-pad)/(float)(cell-1-2*pad));
                Color sample=art.GetPixelBilinear((source%4+.02f+.96f*u)/4,(source/4+.02f+.96f*v)/4);
                float light=Mathf.Clamp(.6f+sample.grayscale,.65f,1.35f);
                Color color=target*light;color.a=1;pixels[(index/4*cell+y)*size+index%4*cell+x]=color;
            }
        }
        string path=Output+Path.GetFileName(folder)+".png";Save(pixels,size,path,true);
        Set(AssetDatabase.LoadAssetAtPath<Material>(folder+"/Palette.mat"),AssetDatabase.LoadAssetAtPath<Texture2D>(path),Color.white);
        foreach(string meshPath in Directory.GetFiles(folder,"*.asset"))
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(mesh==null||mesh.name.Contains("Glow"))continue;
            var old=Original(mesh);var uv=new Vector2[mesh.vertexCount];var vertices=mesh.vertices;var normals=mesh.normals;
            for(int i=0;i<uv.Length;i++)
            {
                int s=Mathf.Clamp((int)(old[i].x*sourceCells.Length),0,sourceCells.Length-1);var p=Project(vertices[i],normals[i],mesh.bounds);
                uv[i]=new Vector2((s%4+.065f+p.x*.87f)/4,(s/4+.065f+p.y*.87f)/4);
            }
            mesh.uv=uv;EditorUtility.SetDirty(mesh);
        }
    }
}
