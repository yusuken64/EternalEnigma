using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

/// <summary>Called once at generation completion. All output belongs to the generated world.</summary>
public static class BiomeDecorations
{
    public static bool Enabled = true;
    static BiomeDecorationCatalog Catalog(TileWorldCreator creator)
    {
        var binding=creator.GetComponent<BiomeDecorationBinding>();
        var catalog=Enabled?(binding!=null?binding.DecorationsEnabled?binding.Catalog:null:BiomeDecorationCatalog.Load()):null;
        if(catalog==null&&creator.worldObject!=null) {
            var old=creator.worldObject.transform.Find("Biome decorations");
            if(old!=null){DungeonPresentation.ClearOutput(old.gameObject);DungeonPresentation.Release(old.gameObject);}
        }
        return catalog;
    }
    public static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    public static List<Vector2Int> TownPosts(TownPlan plan)
    {
        bool At(string layer,Vector2Int p)=>plan.Layers.TryGetValue(layer,out var mask)&&mask.At(new GridPoint(p.x,p.y));
        var reserved=plan.BuildingSlots.Concat(plan.AllySlots.Select(p=>p.Cell)).Concat(new[]{plan.Exit,plan.DungeonEntrance,plan.PartySpawn}).ToArray();
        var candidates=new List<Vector2Int>();
        for(int y=1;y<plan.Height-1;y++)for(int x=1;x<plan.Width-1;x++) {
            var p=new Vector2Int(x,y);
            if(!At(TownLayers.Walkable,p)||At(TownLayers.Roads,p)||At(TownLayers.Props,p)||plan.IsReserved(new GridPoint(x,y)))continue;
            if(!Directions.Any(d=>At(TownLayers.Roads,p+d)))continue;
            if(reserved.Any(q=>Mathf.Max(Mathf.Abs(q.X-x),Mathf.Abs(q.Y-y))<=2))continue;
            if(Directions.Any(d=>!At(TownLayers.Walkable,p+d)))continue;
            candidates.Add(p);
        }
        return BiomeDecorationPlacement.SelectCells(candidates,plan.Seed,"town-posts",4,24).Take(24).ToList();
    }
    public static void Town(TileWorldCreator creator)
    {
        var catalog=Catalog(creator);if(!Enabled||catalog==null||creator.worldObject==null||!CoreLayoutCache.TryGetTown(creator,out var plan))return;
        var root=BiomeDecorationPlacement.Reset(creator.worldObject.transform);
        var posts=TownPosts(plan);float size=creator.twcAsset.cellSize;
        var biome=creator.GetComponent<TownBiomeStyle>()?.Current??OverworldBiome.Grassland;
        var batch=new EnvironmentBatch(root);
        foreach(var p in posts)BiomeDecorationPlacement.Add(batch,root,catalog,catalog.Get(biome,BiomeDecorationKind.LampPost),new Vector3(p.x+.5f,p.y+.5f,0)*size,size*.8f,0);
        batch.Finish();BiomeDecorationPlacement.Walls(creator.worldObject.transform,root,catalog,plan.Seed,"town",size,posts);
    }
    public static void World(TileWorldCreator creator,OverworldGrid grid)
    {
        var catalog=Catalog(creator);if(!Enabled||catalog==null||creator.worldObject==null)return;
        var root=BiomeDecorationPlacement.Reset(creator.worldObject.transform);
        BiomeDecorationPlacement.Walls(creator.worldObject.transform,root,catalog,grid.CampaignSeed,"overworld",creator.twcAsset.cellSize);
        BiomeRoadSigns.Build(grid,root,catalog,creator.twcAsset.cellSize);
    }
    public static void Dungeon(TileWorldCreator creator,DungeonFloor floor,DungeonVisualSelection selection,bool preview=false)
    {
        var catalog=Catalog(creator);if(!Enabled||catalog==null)return;
        var root=BiomeDecorationPlacement.Reset(creator.worldObject.transform);
        // Cancel legacy root height scaling, retaining gameplay's XY positions.
        root.localScale=new Vector3(1,1,1/creator.worldObject.transform.lossyScale.z);DungeonPresentation.SetGroundHeight(root);
        if(selection.IsLegacy) {
            foreach(var layer in creator.twcAsset.mapBuildLayers.Where(l=>l.layerName.IndexOf("torch",StringComparison.OrdinalIgnoreCase)>=0)) {
                var legacy=creator.worldObject.transform.Find(layer.layerName+"_layer");
                if(legacy!=null){DungeonPresentation.ClearOutput(legacy.gameObject);DungeonPresentation.Release(legacy.gameObject);}
            }
            var surfaces=root.gameObject.AddComponent<BiomeDecorationSurfaceSet>();
            var wallLayers=new HashSet<string>(creator.twcAsset.mapBuildLayers.Where(l=>l.active&&l is TWC.Actions.InstantiateTiles &&
                !new[]{"floor","ground","carpet","torch"}.Any(role=>l.layerName.IndexOf(role,StringComparison.OrdinalIgnoreCase)>=0)).Select(l=>l.layerName+"_layer"));
            var wallFilters=new List<MeshFilter>();
            foreach(var filter in creator.worldObject.GetComponentsInChildren<MeshFilter>()) {
                // The legacy wall renderer has already applied TWC's XZ -> XY transformation.
                var ancestor=filter.transform;bool wall=false;
                while(ancestor!=null&&ancestor!=creator.worldObject.transform){wall|=wallLayers.Contains(ancestor.name);ancestor=ancestor.parent;}
                if(wall && filter.sharedMesh!=null && filter.sharedMesh.isReadable)
                    wallFilters.Add(filter);
            }
            BiomeDecorationMeshMounts.Legacy(surfaces,wallFilters,floor,creator.twcAsset.cellSize);
        }
        foreach(var set in creator.worldObject.GetComponentsInChildren<BiomeDecorationSurfaceSet>())foreach(var face in set.Faces) {
            face.Biome=selection.Biome;
            face.Surface=selection.Environment==DungeonEnvironmentKind.Outdoor?DecorationSurface.NaturalWall:DecorationSurface.BuiltWall;
            face.HasAdjacentFloor=false;
            var center=creator.worldObject.transform.InverseTransformPoint(set.transform.TransformPoint(face.Center));
            var normal=creator.worldObject.transform.InverseTransformDirection(set.transform.TransformDirection(face.Normal)).normalized;
            foreach(float distance in new[]{.1f,.35f,.6f,.9f,1.2f}) {
                var probe=center+normal*creator.twcAsset.cellSize*distance;
                int x=Mathf.FloorToInt(probe.x/creator.twcAsset.cellSize),y=Mathf.FloorToInt(probe.y/creator.twcAsset.cellSize);
                if(!floor.Layers[DungeonLayers.Floor].At(new GridPoint(x,y)))continue;
                face.HasAdjacentFloor=true;
                // Fog uses actor cell coordinates, not the half-cell offset of rendered tiles.
                face.VisibilityFloorWorld=new Vector3(x,y,0)*creator.twcAsset.cellSize;
                break;
            }
        }
        BiomeDecorationPlacement.Walls(creator.worldObject.transform,root,catalog,floor.Seed,"dungeon",creator.twcAsset.cellSize,null,preview);
    }
}
