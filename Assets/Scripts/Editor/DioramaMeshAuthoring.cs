using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DioramaMeshAuthoring
{
    const string Folder="Assets/Art/Diorama/Authored";
    [Serializable] public class Part {public string role;public int[] triangles;}
    [Serializable] public class Source {public string id;public Vector3[] vertices,normals;public Color[] colors;public Vector2[] uv;public Part[] parts;public DioramaSocket[] sockets;}
    [Serializable] public class Sources {public string[] roles;public Source[] models;}
    [MenuItem("Tools/Eternal Enigma/Diorama/Import Blender Kit")]
    public static void Import()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var source=JsonUtility.FromJson<Sources>(File.ReadAllText("ArtSource/Diorama/Meshes.json"));
        const string catalogPath="Assets/Resources/EnvironmentKit/DioramaCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<DioramaCatalog>(catalogPath);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<DioramaCatalog>();AssetDatabase.CreateAsset(catalog,catalogPath);}
        catalog.Roles=source.roles;
        catalog.Palettes=Enum.GetValues(typeof(OverworldBiome)).Cast<OverworldBiome>().Select(b=>new DioramaPalette {
            Biome=b,Materials=source.roles.Select(r=>MakeMaterial(b,r)).ToArray()
        }).ToArray();
        var models=new List<DioramaModel>();
        foreach(var data in source.models)
        {
            var mesh=new Mesh {name=data.id,vertices=data.vertices,normals=data.normals,colors=data.colors,uv=data.uv,subMeshCount=data.parts.Length};
            for(int i=0;i<data.parts.Length;i++)mesh.SetTriangles(data.parts[i].triangles,i);
            mesh.RecalculateBounds();
            string path=Folder+"/"+data.id+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}
            else {EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var obj=new GameObject(data.id);obj.AddComponent<MeshFilter>().sharedMesh=saved;
            obj.AddComponent<MeshRenderer>().sharedMaterials=data.parts.Select(p=>catalog.Material(p.role,OverworldBiome.Grassland)).ToArray();
            obj.AddComponent<DioramaModelTag>().Id=data.id;
            var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Folder+"/"+data.id+".prefab");Object.DestroyImmediate(obj);
            float target=data.id switch {
                "SnowPine"=>DioramaScale.Pine,"Palm"=>DioramaScale.Pine,"Willow"=>DioramaScale.Broadleaf,
                "DeadTree"=>DioramaScale.Broadleaf,"CharredTree"=>DioramaScale.Broadleaf,
                "Fence"=>DioramaScale.Fence,"Signboard"=>DioramaScale.Sign,"BoulderLarge"=>DioramaScale.HeroHeight*.9f,
                "BoulderSmall"=>DioramaScale.HeroHeight*.6f,_=>0};
            models.Add(new DioramaModel {Id=data.id,Prefab=prefab,Height=saved.bounds.size.z,Width=Mathf.Max(saved.bounds.size.x,saved.bounds.size.y),TargetHeight=target,
                Triangles=data.parts.Sum(p=>p.triangles.Length/3),Sockets=data.sockets??Array.Empty<DioramaSocket>(),Tree=new[]{"SnowPine","Palm","Willow","DeadTree","CharredTree"}.Contains(data.id)});
        }
        // A rerun retains imported source selections; source mesh GUIDs are never replaced.
        catalog.Models=models.Concat(catalog.Models.Where(m=>m.Id.StartsWith("Pack")||m.Id.StartsWith("Castle"))).ToArray();
        var style=PaintedGroundStyle.Load();
        if(style!=null)
        {
            style.GrassLip=catalog.Get("GrassLip").Prefab.GetComponent<MeshFilter>().sharedMesh;
            style.GrassLipMaterial=catalog.Material("Leaf",OverworldBiome.Grassland);EditorUtility.SetDirty(style);
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("Diorama Blender kit imported: "+models.Count+" models, stable mesh GUIDs, base-centered XY/-Z pivots.");
    }
    static Material MakeMaterial(OverworldBiome biome,string role)
    {
        string path=Folder+"/"+biome+"_"+role+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("EternalEnigma/Diorama Vertex Lit"));AssetDatabase.CreateAsset(m,path);}
        m.name=biome+"_"+role;
        var palette=TownHouseTiles.Palette(biome);
        m.color=role switch {"Plaster"=>palette.plaster,"Timber"=>palette.timber,"Roof"=>palette.roof,"Door"=>palette.door,"Glass"=>palette.glass,
            "Leaf"=>biome==OverworldBiome.Marsh?new Color(.35f,.48f,.22f):new Color(.48f,.69f,.25f),
            "Flower"=>new Color(.93f,.42f,.38f),"Stone"=>new Color(.59f,.60f,.56f),"Metal"=>new Color(.19f,.22f,.24f),
            "Glow"=>new Color(1,.78f,.33f),"Snow"=>new Color(.91f,.95f,1),_=>new Color(.43f,.30f,.19f)};
        m.SetFloat("_Wind",role=="Leaf"?.012f:0);EditorUtility.SetDirty(m);return m;
    }
}
