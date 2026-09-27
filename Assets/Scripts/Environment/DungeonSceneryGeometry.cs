using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

// Small, owned palette-mapped meshes; no collider or imported-asset mutation.
internal static class DungeonSceneryGeometry
{
    internal static Mesh Build(DungeonSceneryKind kind, OverworldBiome biome)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 color)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
            uv.AddRange(new[]{color,color,color,color});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        void Box(Vector3 center,Vector3 size,Vector2 color)
        {
            var a=center-size/2;var b=center+size/2;
            Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z),color);
            Quad(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),color);
            Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),color);
            Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),color);
            Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),color);
            Quad(new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,a.y,b.z),color);
        }
        void Rings(float[] radius,float[] heights,Vector2 color)
        {
            const int sides=8;
            for(int ring=0;ring<radius.Length-1;ring++) for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 P(float angle,int r)=>new Vector3(Mathf.Cos(angle)*radius[r],Mathf.Sin(angle)*radius[r],-heights[r]);
                Quad(P(a,ring),P(b,ring),P(b,ring+1),P(a,ring+1),color);
            }
        }
        if(kind==DungeonSceneryKind.Hazard)
            Rings(new[]{0f,.78f,.85f,0f},new[]{0f,.04f,.07f,.07f},new Vector2(.125f,.875f));
        else if(biome==OverworldBiome.Desert || biome==OverworldBiome.Marsh && kind==DungeonSceneryKind.Container)
            Rings(new[]{0f,.3f,.55f,.42f,.24f,0f},new[]{0f,0f,.38f,.7f,.85f,.85f},new Vector2(.625f,.625f));
        else if(kind==DungeonSceneryKind.Destructible && (biome==OverworldBiome.Mountain || biome==OverworldBiome.Tundra || biome==OverworldBiome.Volcanic))
            Rings(new[]{0f,.42f,.36f,0f},new[]{0f,.12f,.7f,1f},new Vector2(.875f,.875f));
        else
        {
            var wood=new Vector2(.375f,.125f);var band=new Vector2(.625f,.875f);
            Box(new Vector3(0,0,-.35f),new Vector3(1.1f,.85f,.7f),wood);
            Box(new Vector3(-.34f,0,-.74f),new Vector3(.1f,.89f,.1f),band);
            Box(new Vector3(.34f,0,-.74f),new Vector3(.1f,.89f,.1f),band);
            if(kind==DungeonSceneryKind.Container) Box(new Vector3(0,-.45f,-.48f),new Vector3(.16f,.06f,.22f),new Vector2(.125f,.625f));
        }
        var mesh=new Mesh {name=$"{biome} {kind}"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
}
