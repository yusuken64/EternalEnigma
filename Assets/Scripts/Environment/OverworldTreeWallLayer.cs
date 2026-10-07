using System;
using System.Linq;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable, ActionName(Name="Diorama forest walls")]
public sealed class OverworldTreeWallLayer : TWCBuildLayer
{
    public EnvironmentKit Kit;
    public const int MaxTriangles=2000000,ChunkTriangles=96000,TreesPerChunk=192;
    public override TWCBuildLayer Clone()=>new OverworldTreeWallLayer {guid=guid,assignedGenerationLayerGuid=assignedGenerationLayerGuid,layerName=layerName,active=active,Kit=Kit};
    public override void Execute(TileWorldCreator creator,bool force)
    {
        try
        {
            var grid=creator.GetComponent<CampaignOverworld>()?.CurrentGrid;var catalog=DioramaCatalog.Load();if(grid==null||catalog==null||Kit==null)return;
            var root=creator.AddLayerObject(layerName,guid);root.transform.SetParent(creator.worldObject.transform,false);root.transform.localRotation=Quaternion.identity;
            foreach(var child in root.transform.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);Release(child.gameObject);}
            foreach(var old in root.GetComponents<EnvironmentMeshOwner>())Release(old);
            var batch=new EnvironmentBatch(root.transform);var protect=OverworldCosmetics.Protected(grid);
            var candidates=new List<(int x,int y,uint hash)>();float size=creator.twcAsset.cellSize;
            for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
                if((x+y)%2==0&&!protect[x,y]&&OverworldCosmetics.InLayer(grid,OverworldLayers.Trees,x,y)&&!OverworldCosmetics.InLayer(grid,OverworldLayers.Water,x,y))
                    candidates.Add((x,y,OverworldCosmetics.Hash(grid.CampaignSeed^15401,x,y)));
            var chunks=new Dictionary<(int,int),(int count,int triangles)>();int total=0;
            foreach(var p in candidates.OrderBy(p=>p.hash).ThenBy(p=>p.y).ThenBy(p=>p.x))
            {
                var biome=OverworldCosmetics.Biome(grid,p.x,p.y);string id=Kit.TreeModels.Pick(biome,p.hash);var model=catalog.Get(id);if(model==null)continue;
                int cost=Cost(model);var key=(p.x/32,p.y/32);chunks.TryGetValue(key,out var chunk);
                if(total+cost>MaxTriangles||chunk.count>=TreesPerChunk||chunk.triangles+cost>ChunkTriangles)continue;
                if(!DioramaPlacement.TreePosition(model,p.x,p.y,size,DioramaPlacement.Variation(p.hash),
                    (x,y)=>x>=0&&y>=0&&x<grid.Width&&y<grid.Height&&protect[x,y],out var position))continue;
                catalog.Add(batch,id,biome,position,(p.hash>>8)%360,DioramaPlacement.Variation(p.hash),lod:1);
                total+=cost;chunks[key]=(chunk.count+1,chunk.triangles+cost);
            }
            batch.Finish();
        }
        finally {creator.executedBuildLayersCount++;}
    }
    public static int Cost(DioramaModel model)
    {
        var lod=model.Prefab.GetComponentInChildren<LODGroup>();
        if(lod==null)return model.Triangles;
        return lod.GetLODs()[Mathf.Min(1,lod.lodCount-1)].renderers.Where(r=>r!=null).Sum(r=>{
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;return Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s)/3);});
    }
    static void Release(UnityEngine.Object obj){if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset) {layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);Kit=(EnvironmentKit)UnityEditor.EditorGUILayout.ObjectField("Kit",Kit,typeof(EnvironmentKit),false);}
#endif
}
