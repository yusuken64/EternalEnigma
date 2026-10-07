using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public static class DioramaTownDressing
{
    public static void Build(EnvironmentBatch batch,TownPlan plan,OverworldBiome biome,float size,DioramaCatalog diorama)
    {
        var interiors=TownInteriorCatalog.Load();
        // Furnish only blocked footprint corners; doors and their approach cells remain clear.
        foreach(var footprint in plan.Footprints)
        {
            var front=footprint.Cells.Min(p=>p.Y);
            foreach(var cell in footprint.Cells.Where(p=>p.Y==front).OrderBy(p=>p.X).Where((p,i)=>i==0||i==footprint.Cells.Count(p=>p.Y==front)-1))
            {
                if(plan.Layers[TownLayers.Walkable].At(cell)||plan.IsReserved(cell))continue;
                uint hash=OverworldCosmetics.Hash(plan.Seed^31991,cell.X,cell.Y);
                var position=new Vector3(cell.X+.5f,cell.Y+.12f,0)*size;
                string id=hash%2==0?"Barrel":"Crate";var prop=interiors?.Get(id);
                if(prop!=null)batch.Add(prop.Mesh,interiors.Material(biome),position,Vector3.one*DioramaScale.ToHeight(prop.Mesh.bounds.size.z,DioramaScale.Barrel),(int)((hash>>16)%24)-12);
                diorama.Add(batch,"FlowerBox",biome,position+new Vector3(.52f,.15f,0));
            }
        }
        for(int y=1;y<plan.Height-1;y++) for(int x=1;x<plan.Width-1;x++)
        {
            if(!plan.Layers[TownLayers.Parks][x,y]||plan.IsReserved(new GridPoint(x,y)))continue;
            // Fence segments decorate blocked park edges, never an open walkway.
            if(plan.Layers[TownLayers.Walkable][x,y])continue;
            uint hash=OverworldCosmetics.Hash(plan.Seed^7193,x,y);if(hash%3!=0)continue;
            foreach(var d in BiomeDecorations.Directions)
                if(!plan.Layers[TownLayers.Parks][x+d.x,y+d.y]&&!plan.Layers[TownLayers.Roads][x+d.x,y+d.y])
                    diorama.Add(batch,"Fence",biome,new Vector3(x+.5f+d.x*.38f,y+.5f+d.y*.38f,0)*size,d.x!=0?90:0);
        }
    }
}
