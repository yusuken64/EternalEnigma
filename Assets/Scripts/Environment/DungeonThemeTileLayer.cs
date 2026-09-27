using System;
using System.Linq;
using TWC;
using TWC.Actions;
using UnityEngine;

/// <summary>Synchronous, random-free TWC presentation using native quarter-tile classifications.</summary>
[Serializable]
public sealed class DungeonThemeTileLayer : TWCBuildLayer
{
    public TileWorldCreator4TilesPreset Preset;
    public Vector3 Offset;
    public string[] IgnoreLayers = Array.Empty<string>();
    public override TWCBuildLayer Clone() => new DungeonThemeTileLayer { guid=guid, assignedGenerationLayerGuid=assignedGenerationLayerGuid,
        layerName=layerName, active=active, Preset=Preset, Offset=Offset, IgnoreLayers=(string[])IgnoreLayers.Clone() };
    public override void Execute(TileWorldCreator creator, bool force)
    {
        try
        {
            var map=creator.GetGeneratedBlueprintMap(assignedGenerationLayerGuid.ToString());
            if(map==null || Preset==null) return;
            var root=creator.AddLayerObject(layerName,guid);
            root.transform.SetParent(creator.worldObject.transform,false); root.transform.localRotation=Quaternion.identity;
            root.transform.localPosition=Offset;
            DungeonPresentation.SetGroundHeight(root.transform, Offset.z);
            var batch=new EnvironmentBatch(root.transform);
            float unit=creator.twcAsset.cellSize*.5f;
            foreach(var tile in map.clusters.Values.SelectMany(c=>c.Values))
            {
                int x=(int)tile.position.x/2,y=(int)tile.position.z/2;
                if(IgnoreLayers.Any(id=> { var mask=creator.GetMapOutputFromBlueprintLayer(new Guid(id)); return mask!=null && mask[x,y]; })) continue;
                var prefab=tile.tileType switch { TileData.TileType.edge=>Preset.edgeTile, TileData.TileType.exteriorCorner=>Preset.exteriorCornerTile,
                    TileData.TileType.interiorCorner=>Preset.interiorCornerTile, _=>Preset.fillTile };
                if(prefab==null) continue;
                var filter=prefab.GetComponentInChildren<MeshFilter>();
                batch.Add(filter.sharedMesh,filter.GetComponent<MeshRenderer>().sharedMaterial,
                    new Vector3((tile.position.x+.5f)*unit,(tile.position.z+.5f)*unit,0),Vector3.one*unit,-tile.yRotation);
            }
            batch.Finish();
        }
        finally { creator.executedBuildLayersCount++; }
    }
}
