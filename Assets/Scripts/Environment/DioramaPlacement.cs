using System;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

public static class DioramaPlacement
{
    // Fit the visible canopy around the existing protected route cells, without moving the Core cell.
    public static bool TreePosition(DioramaModel model,int x,int y,float size,float variation,Func<int,int,bool> protectedCell,out Vector3 position)
    {
        float radius=model.Width*model.Scale*variation*.5f/size;
        foreach(var offset in new[]{Vector2.zero,new Vector2(.26f,0),new Vector2(-.26f,0),new Vector2(0,.26f),new Vector2(0,-.26f),new Vector2(.26f,.26f),new Vector2(-.26f,.26f),new Vector2(.26f,-.26f),new Vector2(-.26f,-.26f)})
        {
            var p=new Vector2(x+.5f,y+.5f)+offset;bool fits=true;
            for(int yy=y-2;yy<=y+2&&fits;yy++) for(int xx=x-2;xx<=x+2;xx++)
                if(protectedCell(xx,yy))
                {
                    var nearest=new Vector2(Mathf.Clamp(p.x,xx,xx+1),Mathf.Clamp(p.y,yy,yy+1));
                    if((p-nearest).sqrMagnitude<(radius+.02f)*(radius+.02f)){fits=false;break;}
                }
            if(fits){position=new Vector3(p.x,p.y,0)*size;return true;}
        }
        position=default;return false;
    }
    public static string GroundCover(OverworldBiome biome,uint hash,bool rock=false)
    {
        if(rock)return hash%2==0?"PackRock01":"PackRock05";
        return biome switch {
            OverworldBiome.Desert=>"PackRock01",OverworldBiome.Tundra=>"BoulderSmall",
            OverworldBiome.Marsh=>hash%2==0?"Reeds":"MushroomRing",OverworldBiome.Water=>"Reeds",
            OverworldBiome.Volcanic=>"BoulderLarge",OverworldBiome.Mountain=>"PackRock05",
            OverworldBiome.Forest=>hash%3==0?"Fern":hash%3==1?"MushroomRing":"TallGrass",
            _=>hash%3==0?"PackGrass01":"PackFlower0"+(1+hash%5)
        };
    }
    public static float Variation(uint hash)=>.96f+(hash>>8)%9*.01f;
}
