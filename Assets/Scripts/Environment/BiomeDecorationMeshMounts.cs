using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Bounds-tested mounts on the irregular legacy stone mesh, without physics components.</summary>
public static class BiomeDecorationMeshMounts
{
    struct Triangle { public Vector3 A,B,C; }
    public static void Legacy(BiomeDecorationSurfaceSet target,IEnumerable<MeshFilter> filters,DungeonFloor floor,float size)
    {
        var cells=new Dictionary<Vector2Int,List<Triangle>>();
        foreach(var filter in filters) {
            var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
            var matrix=target.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var verts=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3) {
                var t=new Triangle{A=matrix.MultiplyPoint3x4(verts[indices[i]]),B=matrix.MultiplyPoint3x4(verts[indices[i+1]]),C=matrix.MultiplyPoint3x4(verts[indices[i+2]])};
                var min=Vector3.Min(t.A,Vector3.Min(t.B,t.C));var max=Vector3.Max(t.A,Vector3.Max(t.B,t.C));
                for(int x=Mathf.FloorToInt(min.x/size);x<=Mathf.FloorToInt(max.x/size);x++)for(int y=Mathf.FloorToInt(min.y/size);y<=Mathf.FloorToInt(max.y/size);y++) {
                    var key=new Vector2Int(x,y);if(!cells.TryGetValue(key,out var list))cells[key]=list=new();list.Add(t);
                }
            }
        }
        for(int y=0;y<floor.Height;y++)for(int x=0;x<floor.Width;x++) {
            if(!floor.Layers[DungeonLayers.Floor][x,y])continue;
            foreach(var d in BiomeDecorations.Directions) {
                if(floor.Layers[DungeonLayers.Floor].At(new GridPoint(x+d.x,y+d.y)))continue;
                var triangles=new List<Triangle>();
                for(int xx=x-1;xx<=x+1;xx++)for(int yy=y-1;yy<=y+1;yy++)if(cells.TryGetValue(new Vector2Int(xx,yy),out var list))triangles.AddRange(list);
                if(triangles.Count==0)continue;
                var direction=new Vector3(d.x,d.y,0);var tangent=new Vector3(d.y,-d.x,0);
                var origin=new Vector3((x+.5f)*size,(y+.5f)*size,-.8f);
                float center=Hit(origin,direction,triangles,size*1.5f);if(center<0)continue;
                bool valid=true;const float half=.20f;
                for(int u=-1;u<=1;u++)for(int v=-1;v<=1;v++) {
                    float distance=Hit(origin+tangent*(u*half)+Vector3.forward*(v*half),direction,triangles,size*1.5f);
                    if(distance<0||Mathf.Abs(distance-center)>.10f)valid=false;
                }
                if(valid)target.Faces.Add(new BiomeDecorationFace{Center=origin+direction*center,Normal=-direction,Width=half*2,Height=half*2,Biome=OverworldBiome.Grassland,Surface=DecorationSurface.BuiltWall});
            }
        }
    }
    static float Hit(Vector3 origin,Vector3 direction,List<Triangle> triangles,float max)
    {
        float nearest=max;bool found=false;
        foreach(var t in triangles) {
            var e1=t.B-t.A;var e2=t.C-t.A;var p=Vector3.Cross(direction,e2);float determinant=Vector3.Dot(e1,p);
            if(Mathf.Abs(determinant)<.000001f)continue;float inv=1/determinant;var delta=origin-t.A;
            float u=Vector3.Dot(delta,p)*inv;if(u<0||u>1)continue;var q=Vector3.Cross(delta,e1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)continue;
            float distance=Vector3.Dot(e2,q)*inv;if(distance>=0&&distance<nearest){nearest=distance;found=true;}
        }
        return found?nearest:-1;
    }
}
