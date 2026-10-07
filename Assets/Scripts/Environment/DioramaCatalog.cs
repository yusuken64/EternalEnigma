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
    public static DioramaCatalog Load()=>Resources.Load<DioramaCatalog>("EnvironmentKit/DioramaCatalog");
    public DioramaModel Get(string id)=>Models.FirstOrDefault(m=>m.Id==id);
    public Material Material(string role,OverworldBiome biome)=>Palettes.First(p=>p.Biome==biome).Materials[Array.IndexOf(Roles,role)];
    public void Add(EnvironmentBatch batch,string id,OverworldBiome biome,Vector3 position,float rotation=0,float variation=1,Vector3? scale=null,int? lod=null)
    {
        var model=Get(id);if(model==null)throw new ArgumentException("Unknown diorama model: "+id);
        batch.AddPrefab(model.Prefab,position,scale??Vector3.one*(model.Scale*variation),Quaternion.Euler(0,0,rotation),lod??model.Lod,
            material=>material!=null&&material.shader.name=="EternalEnigma/Diorama Vertex Lit"?Material(material.name.Split('_').Last(),biome):material);
    }
}
