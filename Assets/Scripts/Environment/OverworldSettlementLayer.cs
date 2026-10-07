using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

[Serializable,ActionName(Name="Diorama landmarks and road signs")]
public sealed class OverworldSettlementLayer : TWCBuildLayer
{
    public const string Layer="Cosmetic/Settlements";
    public override TWCBuildLayer Clone()=>new OverworldSettlementLayer {guid=guid,layerName=layerName,assignedGenerationLayerGuid=assignedGenerationLayerGuid,active=active};
    public override void Execute(TileWorldCreator creator,bool force)
    {
        try
        {
            var grid=creator.GetComponent<CampaignOverworld>()?.CurrentGrid;var catalog=DioramaCatalog.Load();
            if(grid==null||catalog==null)return;
            var root=creator.AddLayerObject(layerName,guid);root.transform.SetParent(creator.worldObject.transform,false);root.transform.localRotation=Quaternion.identity;
            foreach(var child in root.transform.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);DungeonPresentation.Release(child.gameObject);}
            foreach(var owner in root.GetComponents<EnvironmentMeshOwner>())DungeonPresentation.Release(owner);
            var batch=new EnvironmentBatch(root.transform);float size=creator.twcAsset.cellSize;
            foreach(var location in grid.Locations.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                if(grid.TownFootprints.Any(t=>t.LocationId==location.Key))continue;
                var cell=location.Value;var biome=OverworldCosmetics.Biome(grid,cell.X,cell.Y);
                var origin=new Vector3(cell.X+.5f,cell.Y+.5f,0)*size;
                bool dungeon=At(OverworldLayers.StoryDungeons)||At(OverworldLayers.RepeatableDungeons)||At(OverworldLayers.FinalDungeon);
                // The gate has an open centre on the interaction cell. Towers and
                // cottages are placed only off the existing route/lock/bridge masks.
                catalog.Add(batch,"CastleGate",biome,origin+Vector3.up*size*.30f,scale:Vector3.one*.70f);
                int placed=0;
                foreach(var offset in new[]{new Vector2Int(-1,1),new Vector2Int(1,1),new Vector2Int(0,2),new Vector2Int(-2,0),new Vector2Int(2,0)})
                {
                    int x=cell.X+offset.x,y=cell.Y+offset.y;
                    if(x<1||y<1||x>=grid.Width-1||y>=grid.Height-1 ||
                        new[]{OverworldLayers.Roads,OverworldLayers.Bridges,OverworldLayers.Locks,OverworldLayers.Water,OverworldLayers.Mountains,OverworldLayers.TownFootprints}.Any(l=>OverworldCosmetics.InLayer(grid,l,x,y)))continue;
                    var p=new Vector3(x+.5f,y+.5f,0)*size;
                    if(dungeon)catalog.Add(batch,"CastleTower",biome,p,scale:Vector3.one*.62f);
                    else DioramaSettlementGeometry.House(batch,catalog,biome,p,size*.85f,OverworldCosmetics.Hash(grid.CampaignSeed,x,y));
                    if(++placed==2)break;
                }
                catalog.Add(batch,"CastleFlag",biome,origin+new Vector3(size*.60f,size*.32f,-.1f),scale:Vector3.one*.70f);
                bool At(string layer)=>OverworldCosmetics.InLayer(grid,layer,cell.X,cell.Y);
            }
            batch.Finish();
            if(BiomeDecorations.Enabled)BiomeRoadSigns.Build(grid,root.transform,BiomeDecorationCatalog.Load(),size);
        }
        finally {creator.executedBuildLayersCount++;}
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)=>layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);
#endif
}
