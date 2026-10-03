using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public static class BiomeDecorationPlacement
{
    public static uint Hash(int seed,string context,string candidate)
    {
        unchecked { uint h=(uint)seed^0x6b7a921du;foreach(char c in "biome-decoration/v1/"+context+"/"+candidate)h=(h^c)*16777619u;h^=h>>16;h*=0x7feb352du;return h^(h>>15); }
    }
    public static string Id(Vector3 p) => Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
    public static List<Vector2Int> SelectCells(IEnumerable<Vector2Int> candidates,int seed,string context,int separation,int cap)
    {
        var result=new List<Vector2Int>();var counts=new Dictionary<Vector2Int,int>();
        foreach(var p in candidates.Distinct().OrderBy(p=>Hash(seed,context,p.x+","+p.y)).ThenBy(p=>p.x).ThenBy(p=>p.y)) {
            var chunk=new Vector2Int(Mathf.FloorToInt(p.x/32f),Mathf.FloorToInt(p.y/32f));counts.TryGetValue(chunk,out int count);
            if(count>=cap || result.Any(q=>(p-q).sqrMagnitude<separation*separation))continue;
            result.Add(p);counts[chunk]=count+1;
        }
        return result;
    }
    public static Transform Reset(Transform parent)
    {
        var old=parent.Find("Biome decorations");if(old!=null){DungeonPresentation.ClearOutput(old.gameObject);DungeonPresentation.Release(old.gameObject);}
        var root=new GameObject("Biome decorations").transform;root.SetParent(parent,false);return root;
    }
    public static void Add(EnvironmentBatch batch,Transform root,BiomeDecorationCatalog catalog,BiomeDecorationAsset asset,Vector3 position,float scale,float angle,bool effects=true,Vector3? floorCell=null,bool preview=false)
        => Add(batch,root,catalog,asset,position,scale,Quaternion.Euler(0,0,angle),effects,floorCell,preview);
    public static void Add(EnvironmentBatch batch,Transform root,BiomeDecorationCatalog catalog,BiomeDecorationAsset asset,Vector3 position,float scale,Quaternion rotation,bool effects=true,Vector3? floorCell=null,bool preview=false)
    {
        batch.Add(asset.Mesh,catalog.Material,position,Vector3.one*scale,rotation,
            asset.Mesh.bounds.size.z*scale>.5f?SilhouetteRole.Caster:SilhouetteRole.None);
        if(asset.Effect==null || !effects)return;
        var effect=UnityEngine.Object.Instantiate(asset.Effect,root);
        effect.transform.localPosition=position+rotation*asset.EffectOffset*scale;
        effect.GetComponent<BiomeDecorationEffect>().SetScale(effect.transform.localScale*scale);
        if(floorCell.HasValue){var fog=effect.AddComponent<BiomeDecorationFog>();fog.FloorPosition=root.TransformPoint(floorCell.Value);fog.Preview=preview;effect.GetComponent<BiomeDecorationEffect>().FloorVisible=preview;}
    }
    public static void Walls(Transform world,Transform root,BiomeDecorationCatalog catalog,int seed,string context,float cellSize,List<Vector2Int> posts=null,bool preview=false)
    {
        var batch=new EnvironmentBatch(root);var placed=new List<(Vector3 center,string house)>();var counts=new Dictionary<Vector2Int,int>();var houses=new HashSet<string>();
        var faces=world.GetComponentsInChildren<BiomeDecorationSurfaceSet>().SelectMany(set=>set.Faces.Select(f=>(set,f))).ToList();
        foreach(var pair in faces.OrderBy(p=>Hash(seed,context,Id(p.set.transform.TransformPoint(p.f.Center)))).ThenBy(p=>Id(p.f.Center),StringComparer.Ordinal)) {
            var f=pair.f;var center=root.InverseTransformPoint(pair.set.transform.TransformPoint(f.Center));
            var normal=root.InverseTransformDirection(pair.set.transform.TransformDirection(f.Normal)).normalized;
            var p=new Vector2Int(Mathf.FloorToInt(center.x/cellSize),Mathf.FloorToInt(center.y/cellSize));
            uint h=Hash(seed,context,Id(center));
            var kind=f.Surface==DecorationSurface.NaturalWall?BiomeDecorationKind.Accent:h%5<2?BiomeDecorationKind.Fixture:h%5==2?BiomeDecorationKind.Ornament:BiomeDecorationKind.Accent;
            if(f.PreferredKind>=0)kind=(BiomeDecorationKind)f.PreferredKind;
            if(kind==BiomeDecorationKind.Fixture&&posts!=null&&posts.Any(q=>(p-q).sqrMagnitude<=4))continue;
            if(f.HouseId!=null && kind==BiomeDecorationKind.Accent)continue;
            string houseKey=f.HouseId+"/"+kind;
            if(f.HouseId!=null&&houses.Contains(houseKey))continue;
            if(placed.Any(q=>(f.HouseId==null||q.house!=f.HouseId)&&new Vector2(q.center.x-center.x,q.center.y-center.y).sqrMagnitude<9*cellSize*cellSize))continue;
            var chunk=new Vector2Int(Mathf.FloorToInt(p.x/32f),Mathf.FloorToInt(p.y/32f));counts.TryGetValue(chunk,out int count);if(count>=24)continue;
            var asset=catalog.Get(f.Biome,kind);if((asset.Surfaces&f.Surface)==0)continue;
            float scale=cellSize*(context=="dungeon"?asset.DungeonScale:context=="town"?asset.TownScale:asset.OverworldScale);
            // Fit the complete imported bounds with a safety margin inside this rendered face.
            scale=Mathf.Min(scale,f.Width*.65f/asset.Bounds.size.x,f.Height*.65f/asset.Bounds.size.z);
            if(scale<cellSize*(f.Surface==DecorationSurface.BuiltWall?.12f:.06f))continue;
            var up=Vector3.ProjectOnPlane(Vector3.back,normal).normalized;
            var rotation=Quaternion.LookRotation(-up,-normal);
            var position=center+normal*.025f*cellSize+rotation*asset.MountOffset*scale;
            if(context=="dungeon" && root.TransformPoint(position+Vector3.back*asset.Bounds.size.z*scale).z < -3.1f)continue;
            Add(batch,root,catalog,asset,position,scale,rotation,context!="dungeon"||f.HasAdjacentFloor,
                context=="dungeon"&&f.HasAdjacentFloor?root.InverseTransformPoint(f.VisibilityFloorWorld):(Vector3?)null,preview);
            placed.Add((center,f.HouseId));counts[chunk]=count+1;if(f.HouseId!=null)houses.Add(houseKey);
        }
        batch.Finish();
    }
}
