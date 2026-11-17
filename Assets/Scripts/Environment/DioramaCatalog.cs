using System;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable] public sealed class DioramaModel
{
    public string Id;
    public GameObject Prefab;
    public float Height, Width, TargetHeight;
    public int Triangles, Lod;
    public bool Tree;
    public DioramaSocket[] Sockets=Array.Empty<DioramaSocket>();
    public float Scale => TargetHeight>0 ? DioramaScale.ToHeight(Height,TargetHeight) : 1;
}
[Serializable] public sealed class DioramaSocket {public string kind;public Vector3 center;public float width,height;}
[Serializable] public sealed class DioramaPalette {public OverworldBiome Biome;public Material[] Materials;}

[CreateAssetMenu(menuName="Game/Art/Diorama Catalog")]
public sealed class DioramaCatalog : ScriptableObject
{
    public DioramaModel[] Models=Array.Empty<DioramaModel>();
    public string[] Roles=Array.Empty<string>();
    public DioramaPalette[] Palettes=Array.Empty<DioramaPalette>();
    public Material[] Cliffs=Array.Empty<Material>();
    public Mesh CliffGrid;
    public bool TreeWalls;
    public static DioramaCatalog Load()=>GameContent.Load<DioramaCatalog>("EnvironmentKit/DioramaCatalog");
    public DioramaModel Get(string id)=>Models.FirstOrDefault(m=>m.Id==id);
    public Material Material(string role,OverworldBiome biome)=>Palettes.First(p=>p.Biome==biome).Materials[Array.IndexOf(Roles,role)];
    public void Add(EnvironmentBatch batch,string id,OverworldBiome biome,Vector3 position,float rotation=0,float variation=1,Vector3? scale=null,int? lod=null)
    {
        var model=Get(id);if(model==null)throw new ArgumentException("Unknown diorama model: "+id);
        batch.AddPrefab(model.Prefab,position,scale??Vector3.one*(model.Scale*variation),Quaternion.Euler(0,0,rotation),lod??model.Lod,
            BiomeMap(biome));
    }
    // Per-submesh remaps ran shader.name/name.Split for every prop. Resolve each (material, biome) once.
    [NonSerialized] private System.Collections.Generic.Dictionary<(Material,OverworldBiome),Material> remapped;
    [NonSerialized] private System.Collections.Generic.Dictionary<OverworldBiome,Func<Material,Material>> biomeMaps;
    private Func<Material,Material> BiomeMap(OverworldBiome biome)
    {
        biomeMaps??=new System.Collections.Generic.Dictionary<OverworldBiome,Func<Material,Material>>();
        if(!biomeMaps.TryGetValue(biome,out var map))biomeMaps[biome]=map=material=>Remap(material,biome);
        return map;
    }
    private Material Remap(Material material,OverworldBiome biome)
    {
        if(material==null)return material;
        remapped??=new System.Collections.Generic.Dictionary<(Material,OverworldBiome),Material>();
        if(remapped.TryGetValue((material,biome),out var result))return result;
        result=material.shader.name=="EternalEnigma/Diorama Vertex Lit"?Material(material.name.Split('_').Last(),biome):material;
        remapped[(material,biome)]=result;
        return result;
    }
}
