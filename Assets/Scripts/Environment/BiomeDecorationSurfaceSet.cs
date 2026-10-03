using System;
using System.Collections.Generic;
using UnityEngine;
using EternalEnigma.Core.World;

[Serializable]
public sealed class BiomeDecorationFace
{
    public Vector3 Center, Normal;
    public float Width, Height;
    public OverworldBiome Biome;
    public DecorationSurface Surface;
    public string HouseId;
    public int PreferredKind=-1;
    [NonSerialized] public bool HasAdjacentFloor;
    [NonSerialized] public Vector3 VisibilityFloorWorld;
}

/// <summary>Faces supplied by rendered TWC edge pieces or explicit facade sockets.</summary>
public sealed class BiomeDecorationSurfaceSet : MonoBehaviour
{
    public List<BiomeDecorationFace> Faces = new();
    // Pull only vertical planar faces. Sloping roofs and flat biome seams cannot qualify.
    public void Add(Mesh mesh, Matrix4x4 matrix, OverworldBiome biome, DecorationSurface surface, float cellSize, string houseId=null)
    {
        if(surface==DecorationSurface.Facade) {
            var catalog=BiomeDecorationCatalog.Load();if(catalog==null)return;
            var definition=System.Array.Find(catalog.Facades,f=>f.Model==mesh.name);if(definition==null)return;
            foreach(var socket in definition.Sockets) {
                var bounds=new Bounds(socket.Center,new Vector3(socket.Normal.x==0?socket.Width:.02f,socket.Normal.y==0?socket.Width:.02f,socket.Height));
                if(System.Array.Exists(definition.Exclusions,b=>b.Intersects(bounds)))continue;
                float scale=matrix.MultiplyVector(Vector3.right).magnitude;
                Faces.Add(new BiomeDecorationFace {Center=matrix.MultiplyPoint3x4(socket.Center),Normal=matrix.MultiplyVector(socket.Normal).normalized,Width=socket.Width*scale,Height=socket.Height*scale,Biome=biome,Surface=surface,HouseId=houseId,PreferredKind=(int)socket.Kind});
            }
            return;
        }
        var vertices=mesh.vertices;var indices=mesh.triangles;
        var groups=new Dictionary<(int,int,int,int,int),List<Vector3>>();
        var areas=new Dictionary<(int,int,int,int,int),float>();
        for(int i=0;i<indices.Length;i+=3) {
            var a=matrix.MultiplyPoint3x4(vertices[indices[i]]);var b=matrix.MultiplyPoint3x4(vertices[indices[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[i+2]]);
            var n=Vector3.Cross(b-a,c-a).normalized;
            // Legacy stone sides are slightly irregular triangles rather than axis-aligned quads.
            // An inscribed square guarantees support without spanning adjacent triangles or holes.
            if(Mathf.Abs(n.z)>.015f && Mathf.Abs(n.z)<.85f) {
                var center=(a+b+c)/3;
                float radius=Mathf.Min(Vector3.Cross(a-center,b-center).magnitude/Mathf.Max(.0001f,(b-a).magnitude),
                    Vector3.Cross(b-center,c-center).magnitude/Mathf.Max(.0001f,(c-b).magnitude),Vector3.Cross(c-center,a-center).magnitude/Mathf.Max(.0001f,(a-c).magnitude));
                float extent=radius*1.3f;
                if(extent>cellSize*.08f)Faces.Add(new BiomeDecorationFace {Center=center,Normal=n,Width=extent,Height=extent,Biome=biome,Surface=surface,HouseId=houseId});
            }
            if(Mathf.Abs(n.z)>.015f || Mathf.Max(Mathf.Abs(n.x),Mathf.Abs(n.y))<.99f) continue;
            int side=Mathf.Abs(n.x)>.99f?(n.x>0?0:1):(n.y>0?2:3);
            float plane=Mathf.Abs(n.x)>.99f?a.x:a.y;
            // Include height band to avoid bridging door/window holes in fragmented facades.
            var min=Vector3.Min(a,Vector3.Min(b,c));var max=Vector3.Max(a,Vector3.Max(b,c));
            var key=(side,Mathf.RoundToInt(plane/cellSize*1000),Mathf.RoundToInt(min.z/cellSize*1000),Mathf.FloorToInt((min.x+max.x)*.5f/cellSize),Mathf.FloorToInt((min.y+max.y)*.5f/cellSize));
            if(!groups.TryGetValue(key,out var points))groups[key]=points=new();
            points.Add(a);points.Add(b);points.Add(c);
            areas.TryGetValue(key,out float area);areas[key]=area+Vector3.Cross(b-a,c-a).magnitude*.5f;
        }
        foreach(var pair in groups) {
            var points=pair.Value;var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            float width=pair.Key.Item1<2?bounds.size.y:bounds.size.x;
            if(width<cellSize*.32f || bounds.size.z<cellSize*.24f)continue;
            if(areas[pair.Key]<width*bounds.size.z*.98f)continue; // A bounding rectangle spanning an opening is not a supporting face.
            var normal=pair.Key.Item1==0?Vector3.right:pair.Key.Item1==1?Vector3.left:pair.Key.Item1==2?Vector3.up:Vector3.down;
            Faces.Add(new BiomeDecorationFace {Center=bounds.center,Normal=normal,Width=width,Height=bounds.size.z,Biome=biome,Surface=surface,HouseId=houseId});
        }
    }
}
