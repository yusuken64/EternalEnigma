using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Visual output is wholly owned by the generated world, including ambient animations and effects.</summary>
public static class TownInteriorRendering
{
    public static void Configure(TileWorldCreatorAsset asset)
    {
        var catalog=TownInteriorCatalog.Load();if(catalog==null)return;
        foreach(var pair in new[]{(TownLayers.Carpet,catalog.Carpet),(TownLayers.Counters,catalog.Counter)})
        {
            var blueprint=asset.mapBlueprintLayers.FirstOrDefault(l=>l.layerName==pair.Item1);if(blueprint==null)continue;
            string name="Town interior "+pair.Item1;
            asset.mapBuildLayers.RemoveAll(l=>l.layerName==name);
            asset.mapBuildLayers.Add(new EnvironmentSmartTileLayer {layerName=name,assignedGenerationLayerGuid=blueprint.guid,
                guid=new Guid(pair.Item1==TownLayers.Carpet?"d71dd1c5-038a-4b6b-bdd6-6023daef1001":"d71dd1c5-038a-4b6b-bdd6-6023daef1002"),
                active=true,Kit=EnvironmentKit.Load(),QuarterTiles=pair.Item2,SurfaceMaterial=catalog.BiomeMaterials[0],TownPalette=true,
                Elevation=pair.Item1==TownLayers.Carpet?.065f:0,HeightScale=pair.Item1==TownLayers.Counters?2:1});
        }
    }

    public static void Build(TileWorldCreator creator)
    {
        if(creator.worldObject==null||!CoreLayoutCache.TryGetTown(creator,out var plan))return;
        var old=creator.worldObject.transform.Find("Town interiors");
        if(old!=null){old.gameObject.SetActive(false);DungeonPresentation.ClearOutput(old.gameObject);DungeonPresentation.Release(old.gameObject);}
        var catalog=TownInteriorCatalog.Load();if(catalog==null||plan.Interiors.Count==0)return;
        var root=new GameObject("Town interiors").transform;root.SetParent(creator.worldObject.transform,false);
        var audit=root.gameObject.AddComponent<TownInteriorOutput>();
        float size=creator.twcAsset.cellSize;
        var biome=creator.GetComponent<TownBiomeStyle>()?.Current??OverworldBiome.Grassland;
        var batch=new EnvironmentBatch(root);var perches=new List<(Vector3 position,bool inside)>();
        // Thin walls sit in the centers of blocked cells. Extend the interior paving to
        // their centerlines so outdoor ground cannot show through the room perimeter.
        var kit=EnvironmentKit.Load();var boundary=plan.Layers[TownLayers.ShopWalls];
        var roomFloor=plan.Interiors.SelectMany(i=>plan.ShopRoomAt(i.Door).Floor.Where(c=>c.Y>=i.Door.Y+2)).ToHashSet();
        for(int x=0;x<plan.Width;x++)for(int y=0;y<plan.Height;y++)
        {
            if(!boundary[x,y])continue;
            foreach(int dx in new[]{-1,1})foreach(int dy in new[]{-1,1})
            {
                if(roomFloor.Contains(new GridPoint(x+dx,y))||roomFloor.Contains(new GridPoint(x,y+dy))||roomFloor.Contains(new GridPoint(x+dx,y+dy)))
                {
                    float px=roomFloor.Contains(new GridPoint(x+dx,y))?.03f:0,py=roomFloor.Contains(new GridPoint(x,y+dy))?.03f:0;
                    batch.Add(kit.Mesh("Paving"),kit.Road,new Vector3(x+.5f+dx*(.25f+px/2),y+.5f+dy*(.25f+py/2),-.005f)*size,new Vector3(.5f+px,.5f+py,1)*size);
                }
            }
        }
        foreach(var interior in plan.Interiors)
        foreach(var p in interior.Props)
        {
            var asset=catalog.Get(p.Asset);if(asset==null)throw new InvalidOperationException("Missing interior asset "+p.Asset);
            var position=new Vector3(p.Cell.X+.5f,p.Cell.Y+.5f,-p.Elevation)*size;
            batch.Add(asset.Mesh,catalog.Material(biome),position,Vector3.one*size,p.QuarterTurns*90);
            if(p.Asset is "Shelf" or "Cupboard" or "Bookcase")
                perches.Add((position+Vector3.back*1.295f*size,true));
            if(p.Asset is "Table" or "Bedside" or "Lectern")
            {
                string goods=p.Asset=="Lectern"?"Books":p.Asset=="Bedside"?"Candles":"Tableware";
                batch.Add(catalog.Get(goods).Mesh,catalog.Material(biome),position+Vector3.back*(p.Asset=="Lectern"?.885f:.67f)*size,Vector3.one*size*.8f);
            }
            if(p.Asset=="Hearth")
                batch.Add(catalog.Get("Cookware").Mesh,catalog.Material(biome),position+new Vector3(0,-.04f,-.695f)*size,Vector3.one*size*.8f);
        }
        MountWalls(creator,plan,catalog,root,batch,perches,size,biome,audit);
        // The exterior enclosure is outside Core movement space. Use its authored horizontal cap,
        // with a narrow footed sign rather than putting birds on public floor or interaction cells.
        var sign=catalog.Get("PerchSign");var wall=EnvironmentKit.Load()?.Mesh("Wall");
        if(sign!=null && wall!=null)
            foreach(int x in new[]{-1,plan.Width})
            {
                int y=3+(int)(BiomeDecorationPlacement.Hash(plan.Seed,"fence-perch",x.ToString())%(uint)(plan.Height-6));
                var position=new Vector3(x+.5f,y+.5f,wall.bounds.min.z)*size;
                batch.Add(sign.Mesh,catalog.Material(biome),position,Vector3.one*size);
                perches.Add((position+Vector3.back*.96f*size,false));
            }
        batch.Finish();
        var chosen=new List<Vector3>();
        var birds=new[]{"BirdBlue","BirdRed","BirdYellow"};
        for(int i=0;i<birds.Length;i++)
        {
            var ordered=perches.Where(p=>chosen.All(c=>(p.position-c).sqrMagnitude>size*size*4))
                .OrderBy(p=>p.inside==(i==2)?0:1)
                .ThenBy(p=>BiomeDecorationPlacement.Hash(plan.Seed,"birds-"+i,p.position.ToString("F3"))).ToArray();
            if(ordered.Length==0)continue;
            var perch=ordered[0];chosen.Add(perch.position);
            audit.Birds.Add(new TownBirdPerch {Variant=birds[i],Position=perch.position,Interior=perch.inside});
            var bird=Object.Instantiate(catalog.Get(birds[i]).Prefab,root);bird.name=birds[i]+" on supported perch";
            bird.transform.localPosition=perch.position;bird.transform.localScale=Vector3.one*size*.6f;
            bird.GetComponent<TownAmbientAnimation>().Phase=(BiomeDecorationPlacement.Hash(plan.Seed,birds[i],"phase")%1000)/1000f;
        }
    }

    private static void MountWalls(TileWorldCreator creator,TownPlan plan,TownInteriorCatalog catalog,Transform root,
        EnvironmentBatch batch,List<(Vector3 position,bool inside)> perches,float size,OverworldBiome biome,TownInteriorOutput audit)
    {
        var used=new List<Vector3>();var furniture=plan.Interiors.SelectMany(i=>i.Occupied).ToHashSet();
        var faces=creator.worldObject.GetComponentsInChildren<BiomeDecorationSurfaceSet>()
            .SelectMany(set=>set.Faces.Select(face=>(set,face)))
            .OrderBy(p=>BiomeDecorationPlacement.Hash(plan.Seed,"interior-wall",p.face.Center.ToString("F3")));
        foreach(var pair in faces)
        {
            var face=pair.face;
            if(face.Surface!=DecorationSurface.BuiltWall || face.Width<size*.25f || face.Height<size*.35f)continue;
            audit.SupportingFaces++;
            var normal=root.InverseTransformDirection(pair.set.transform.TransformDirection(face.Normal));
            if(Mathf.Abs(normal.z)>.01f)continue;
            var center=root.InverseTransformPoint(pair.set.transform.TransformPoint(face.Center));
            var wallCell=new GridPoint(Mathf.FloorToInt(center.x/size),Mathf.FloorToInt(center.y/size));
            var walls=plan.Layers[TownLayers.ShopWalls];
            bool horizontal=walls.At(new GridPoint(wallCell.X-1,wallCell.Y))||walls.At(new GridPoint(wallCell.X+1,wallCell.Y));
            bool vertical=walls.At(new GridPoint(wallCell.X,wallCell.Y-1))||walls.At(new GridPoint(wallCell.X,wallCell.Y+1));
            if(horizontal && vertical)continue; // No fittings on corners or junction posts.
            var adjacent=center+normal*size*.6f;
            var cell=new GridPoint(Mathf.FloorToInt(adjacent.x/size),Mathf.FloorToInt(adjacent.y/size));
            if(!plan.IsWalkable(cell)||furniture.Contains(cell))continue;
            bool inside=plan.Layers[TownLayers.ShopFloor].At(cell);
            if(!inside)continue;
            audit.InteriorFaces++;
            if(plan.BuildingSlots.Any(d=>Mathf.Abs(d.X-cell.X)<=1&&Mathf.Abs(d.Y-cell.Y)<=2))continue;
            if(used.Any(p=>(p-center).sqrMagnitude<size*size*4))continue;
            if(used.Count>=24)break;
            var h=BiomeDecorationPlacement.Hash(plan.Seed,"wall-kind",cell.ToString());
            string id=(h%5) switch {0=>"Window",1=>"Banner",2=>"Frame",3=>"Sconce",_=>"Lantern"};
            if(h%7==0 && plan.Interiors.Any(i=>i.Spec.Kind==TownInteriorKind.Inn && plan.ShopRoomAt(i.Door).Floor.Contains(cell)))id="KeyBoard";
            // Model front is -Y. Fit the full model within the verified rectangular supporting face.
            var mesh=catalog.Get(id).Mesh;
            float scale=Mathf.Min(size*.7f,face.Width*.9f/mesh.bounds.size.x,face.Height*.9f/mesh.bounds.size.z);
            if(mesh.bounds.size.x*scale>face.Width*.95f||mesh.bounds.size.z*scale>face.Height*.95f)continue;
            var rotation=Quaternion.FromToRotation(Vector3.down,normal);
            var position=center-rotation*mesh.bounds.center*scale+normal*size*.045f;
            batch.Add(mesh,catalog.Material(biome),position,Vector3.one*scale,rotation);
            used.Add(center);
            audit.WallMountCount++;
            if(id=="Window")
            {
                // Source sill spans .94 x .35, with top at .2675. A .6-sized bird fits its depth.
                var perch=position+rotation*new Vector3(0,-.15f,-.2675f)*scale;
                // Reject narrow sills that cannot support the complete flutter envelope.
                if(.35f*scale>=.32f*size)perches.Add((perch,inside));
            }
            if(id is "Sconce" or "Lantern")
            {
                var effects=BiomeDecorationCatalog.Load();var effect=effects?.Get(biome,BiomeDecorationKind.Fixture)?.Effect;
                if(effect!=null){var glow=Object.Instantiate(effect,root);glow.transform.localPosition=position+rotation*new Vector3(0,-.13f,-.60f)*scale;
                    var budget=glow.GetComponent<BiomeDecorationEffect>();budget.AllowAccentLight=false;budget.SetScale(Vector3.one*size*.045f);}
            }
        }
    }

    public static void SpawnTownsfolk(Town town)
    {
        foreach(var npc in town.Townsfolk)if(npc!=null){npc.gameObject.SetActive(false);Object.Destroy(npc.gameObject);}
        town.Townsfolk.Clear();var catalog=TownInteriorCatalog.Load();if(catalog==null)return;
        var names=new[]{"Cat","Dog"};
        foreach(var p in TownNpcPlacement.Generate(town.Plan))
        {
            var definition=catalog.Characters.Single(d=>d.Id==names[p.Roll]);
            var go=new GameObject(definition.DisplayName);go.transform.SetParent(town.transform);
            go.transform.position=town.WalkableMap.CellToWorld(p.Cell.ToCell());
            var npc=go.AddComponent<TownNpc>();npc.Definition=definition;npc.Cell=p.Cell.ToCell();
            var visual=Object.Instantiate(definition.Prefab,go.transform);float size=town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
            visual.transform.localPosition=new Vector3(.5f,.5f,0)*size;visual.transform.localScale=Vector3.one*size*.65f;npc.Visual=visual.transform;
            town.Townsfolk.Add(npc);
        }
    }
}
