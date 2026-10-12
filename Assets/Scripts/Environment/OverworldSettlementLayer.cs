using System;
using System.Linq;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Progression;
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
            float size=creator.twcAsset.cellSize;
            foreach(var location in grid.Locations.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                if(grid.TownFootprints.Any(t=>t.LocationId==location.Key))continue;
                var locationRoot = new GameObject("Location " + location.Key);
                locationRoot.transform.SetParent(root.transform, false);
                var batch = new EnvironmentBatch(locationRoot.transform);
                var cell=location.Value;var biome=OverworldCosmetics.Biome(grid,cell.X,cell.Y);
                var origin=new Vector3(cell.X+.5f,cell.Y+.5f,0)*size;
                var footprint=grid.LocationFootprints.FirstOrDefault(f=>f.LocationId==location.Key);
                if(footprint?.EncounterKind is { } kind)
                {
                    BuildEncounter(batch,catalog,grid,footprint,kind,biome,size);
                    batch.Finish();
                    continue;
                }
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
                batch.Finish();
                bool At(string layer)=>OverworldCosmetics.InLayer(grid,layer,cell.X,cell.Y);
            }
            if(BiomeDecorations.Enabled)BiomeRoadSigns.Build(grid,root.transform,BiomeDecorationCatalog.Load(),size);
        }
        finally {creator.executedBuildLayersCount++;}
    }
    private static void BuildEncounter(EnvironmentBatch batch,DioramaCatalog catalog,OverworldGrid grid,
        GridLocationFootprint footprint,EncounterKind kind,OverworldBiome biome,float size)
    {
        var origin=new Vector3(footprint.Entrance.X+.5f,footprint.Entrance.Y+.5f,0)*size;
        bool Clear(GridPoint cell)=>!cell.Equals(footprint.Entrance) && !cell.Equals(footprint.Approach) &&
            !(footprint.OtherApproach?.Equals(cell)??false) &&
            !new[]{OverworldLayers.Roads,OverworldLayers.Bridges,OverworldLayers.Locks,OverworldLayers.Water,OverworldLayers.TownFootprints}
                .Any(layer=>OverworldCosmetics.InLayer(grid,layer,cell.X,cell.Y));
        var cells=footprint.Cells.Where(Clear).OrderBy(c=>OverworldCosmetics.Hash(grid.CampaignSeed,c.X,c.Y)).ToArray();
        string marker=kind==EncounterKind.BoulderCave?"RockCap":kind==EncounterKind.LostWoods?"Signboard":
            kind==EncounterKind.Sokoban?"BoulderSmall":kind==EncounterKind.LightPattern?"FlowerBox":
            kind==EncounterKind.Lure?"Fence":kind==EncounterKind.Herding?"Signboard":"CastleGate";
        // Keep the interaction cell visibly open; the marker sits behind it.
        catalog.Add(batch,marker,biome,origin+Vector3.up*size*.3f,variation:.7f);
        int limit=kind==EncounterKind.LostWoods?20:kind==EncounterKind.BoulderCave?8:4;
        foreach(var cell in cells.Take(limit))
        {
            var p=new Vector3(cell.X+.5f,cell.Y+.5f,0)*size;
            uint hash=OverworldCosmetics.Hash(grid.CampaignSeed,cell.X,cell.Y);
            if(kind==EncounterKind.OvertakenCity)DioramaSettlementGeometry.House(batch,catalog,biome,p,size*.85f,hash);
            else catalog.Add(batch,kind switch
            {
                EncounterKind.LostWoods=>hash%2==0?"PackTree01":"PackTree03",
                EncounterKind.BoulderCave=>"BoulderLarge",
                EncounterKind.Bridge=>"Fence",
                EncounterKind.Prison or EncounterKind.BossArena=>"CastleTower",
                EncounterKind.Ambush=>"CastleFlag",
                _=>"BoulderSmall"
            },biome,p,(hash>>8)%360,kind==EncounterKind.BoulderCave?1.2f:.65f);
        }
        if(kind==EncounterKind.Bridge)
            foreach(var approach in new[]{footprint.Approach,footprint.OtherApproach??footprint.Approach}.Distinct())
                catalog.Add(batch,"CastleFlag",biome,new Vector3(approach.X+.85f,approach.Y+.65f,0)*size,variation:.5f);
    }
#if UNITY_EDITOR
    public override void DrawGUI(TileWorldCreatorAsset asset)=>layerName=UnityEditor.EditorGUILayout.TextField("Layer name",layerName);
#endif
}
