using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

public static class DungeonPresentation
{
    public const float GroundPlaneZ = .05f;
    public static void PrepareMapRoot(Transform root, bool legacy)
    {
        // Configure before building: moving this root afterwards shifts the ground into units.
        root.position = legacy ? new Vector3(0, 0, -1.51f) : Vector3.zero;
        root.localScale = legacy ? new Vector3(1, 1, 3.35f) : Vector3.one;
    }
    // Legacy TWC output sits at Z=-1.51 to compensate for its old tile meshes.
    // Themed meshes are already authored on the XY ground plane; don't inherit that lift.
    public static void SetGroundHeight(Transform layer, float height = 0)
    {
        // Leave a small gap beneath unit feet and selection sprites at Z=0.
        var position = layer.position; position.z = GroundPlaneZ + height; layer.position = position;
    }
    public static TileWorldCreatorAsset CloneTemplate(TileWorldCreatorAsset source)
    {
        var clone=UnityEngine.Object.Instantiate(source);clone.hideFlags=HideFlags.DontSave;
        // Preview textures are generated per clone, never borrowed from an authored asset.
        foreach(var layer in clone.mapBlueprintLayers) layer.previewTextureMap=null;
        return clone;
    }
    public static void ReleaseTemplate(TileWorldCreatorAsset asset)
    {
        if(asset==null)return;
        foreach(var texture in asset.mapBlueprintLayers.Select(l=>l.previewTextureMap).Where(t=>t!=null).Distinct())
        {
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(texture))continue;
#endif
            Release(texture);
        }
        Release(asset);
    }
    public static void ClearOutput(GameObject root)
    {
        // Baked scene meshes are not assets. Shared imported/persistent meshes must survive.
        var owned=new HashSet<Mesh>(root.GetComponentsInChildren<EnvironmentMeshOwner>(true).SelectMany(o=>o.Meshes));
        foreach(var mesh in owned) if(mesh!=null) Release(mesh);
        foreach(var owner in root.GetComponentsInChildren<EnvironmentMeshOwner>(true)) owner.Meshes.Clear();
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Where(m=>m!=null).Distinct())
        {
            if(owned.Contains(mesh)) continue;
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
            // Only TWC's combined meshes are owned here; prefab meshes are shared.
            if(mesh.name.Contains("_Cluster_")) Release(mesh);
        }
        foreach(Transform child in root.transform.Cast<Transform>().ToArray()) Release(child.gameObject);
    }
    public static void TrackLegacyMeshes(GameObject root)
    {
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=filter.sharedMesh;
            if(mesh==null || !mesh.name.Contains("_Cluster_")) continue;
#if UNITY_EDITOR
            if(UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
            var owner=filter.GetComponent<EnvironmentMeshOwner>() ?? filter.gameObject.AddComponent<EnvironmentMeshOwner>();
            if(!owner.Meshes.Contains(mesh)) owner.Meshes.Add(mesh);
        }
    }
    public static void Release(UnityEngine.Object obj)
    {
        if(obj is GameObject go) go.SetActive(false);
        if(Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
    }
    public static bool DecorationAllowed(DungeonFloor floor,int x,int y)
    {
        // The complete rotated prop footprint is fitted inside this blocked cell below.
        return x>=0 && y>=0 && x<floor.Width && y<floor.Height && !floor.Layers[DungeonLayers.Floor][x,y];
    }
    public static uint Hash(int seed,int x,int y)
    { unchecked { uint h=(uint)seed ^ (uint)x*0x9e3779b9u ^ (uint)y*0x85ebca6bu ^ 0x6d2b79f5u; h=(h^(h>>16))*0x7feb352du;return h^(h>>15); } }
    public static void PreviewScenery(TileWorldCreator creator,DungeonFloor floor,DungeonTheme theme)
    {
        if(floor.Scenery.Count==0)return;
        var root=new GameObject("Scenery preview");root.transform.SetParent(creator.worldObject.transform,false);
        SetGroundHeight(root.transform);
        var dungeon=root.AddComponent<TileWorldDungeon>();dungeon.Interactables=new();dungeon.Setup(creator,floor);
        foreach(var definition in floor.Scenery)
        {
            var prop=DungeonProp.Create(dungeon,definition,theme);
            prop.GetComponent<FogHiddenVisual>().enabled=false;
        }
    }
    public static void Decorate(TileWorldCreator creator,DungeonFloor floor,DungeonTheme theme)
    {
        var kit=EnvironmentKit.Load();
        if(kit==null) return;
        var root=new GameObject("Theme cosmetics"); root.transform.SetParent(creator.worldObject.transform,false);
        SetGroundHeight(root.transform);
        var batch=new EnvironmentBatch(root.transform);
        float size=creator.twcAsset.cellSize;
        for(int cy=0;cy<floor.Height;cy+=32) for(int cx=0;cx<floor.Width;cx+=32)
        {
            var candidates=new List<(int x,int y,uint hash)>();
            for(int y=cy;y<Math.Min(cy+32,floor.Height);y++) for(int x=cx;x<Math.Min(cx+32,floor.Width);x++)
                if(DecorationAllowed(floor,x,y) && (floor.Layers[DungeonLayers.Floor].At(new GridPoint(x-1,y)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x+1,y)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x,y-1)) || floor.Layers[DungeonLayers.Floor].At(new GridPoint(x,y+1)))) candidates.Add((x,y,Hash(floor.Seed,x,y)));
            foreach(var p in candidates.OrderBy(p=>p.hash).Take(16))
            {
                string id=theme.UseTrees && (p.hash&1)==0 ? kit.TreeModels.Pick(theme.Biome,p.hash) : theme.Decorations.Length>0 ? theme.Decorations[p.hash%(uint)theme.Decorations.Length] : "Rock";
                var model=kit.Models.FirstOrDefault(m=>m.Id==id);
                Mesh mesh=model?.Mesh;
                if(id=="CryptRoots") mesh=Resources.Load<Mesh>("DungeonThemes/CryptRoots");
                if(mesh==null || mesh.triangles.Length/3>120) continue;
                // Constrain full footprint and height below the fog plane (-3.35).
                float extent=Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y);
                // .6 * sqrt(2) < 1: even a square rotated 45 degrees stays inside its cell.
                float scale=Mathf.Min(size*.6f/Mathf.Max(.01f,extent),.85f/Mathf.Max(.01f,mesh.bounds.size.z));
                float baseHeight=theme.Environment==DungeonEnvironmentKind.Outdoor ? .65f : .92f;
                float angle=p.hash%360;
                var center=Quaternion.Euler(0,0,angle)*new Vector3(mesh.bounds.center.x,mesh.bounds.center.y,0)*scale;
                batch.Add(mesh,theme.DecorationMaterial,new Vector3((p.x+.5f)*size-center.x,(p.y+.5f)*size-center.y,-baseHeight-mesh.bounds.max.z*scale),Vector3.one*scale,angle);
            }
        }
        batch.Finish();
    }
}
