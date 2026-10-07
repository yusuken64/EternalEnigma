using EternalEnigma.Core.World;
using UnityEngine;

// Shared facade/roof kit, composed at the campaign's miniature settlement scale.
public static class DioramaSettlementGeometry
{
    public static void House(EnvironmentBatch batch,DioramaCatalog catalog,OverworldBiome biome,Vector3 position,float size,uint hash)
    {
        float width=size*.88f,depth=size*.80f,height=.62f;
        foreach(var side in new[]{Vector2Int.down,Vector2Int.right,Vector2Int.up,Vector2Int.left})
        {
            float angle=side.x==1?90:side.y==1?180:side.x==-1?270:0;
            string id=side.y==-1?"FacadeDoor":side.x!=0?"FacadeWindow":"FacadeWall";
            catalog.Add(batch,id,biome,position+new Vector3(side.x*width*.5f,side.y*depth*.5f,0),angle,
                scale:new Vector3((side.x==0?width:depth)*.5f,.5f,height));
        }
        var roof=TownHouseTiles.RoofTile(-.08f,1.08f,.5f,.5f,true,true,true);
        var gable=TownHouseTiles.GableTile(0,.5f,.5f);
        batch.Owner.Meshes.Add(roof);batch.Owner.Meshes.Add(gable);
        var origin=position-new Vector3(width*.5f,depth*.5f,0);var scale=new Vector3(width,depth,height*2);
        batch.Add(roof,catalog.Material("Roof",biome),origin,scale);
        batch.Add(gable,catalog.Material("Plaster",biome),origin,scale);
        // Back gable uses the same authored profile with reversed facing.
        batch.Add(gable,catalog.Material("Plaster",biome),position+new Vector3(width*.5f,depth*.5f,0),scale,180);
        catalog.Add(batch,"RoofRidge",biome,position+Vector3.back*(-TownHouseTiles.RoofHeight(.5f,.5f,.5f)*height*2),
            scale:new Vector3(.55f,depth,.55f));
        if(hash%3!=0)catalog.Add(batch,"Chimney",biome,position+new Vector3(width*.2f,depth*.18f,-DioramaScale.Eaves*height-.32f),scale:Vector3.one*.55f);
    }
}
