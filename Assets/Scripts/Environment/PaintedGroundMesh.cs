using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Half-cell splat lattice; fixed geometry, deterministic organic material boundaries.</summary>
public static class PaintedGroundMesh
{
    public const int ChunkSize = 32;
    public const float DryDepth = .02f, WaterDepth = .045f, BridgeDepth = -.005f;

    public static PaintedGroundOutput Build(Transform parent, GroundSurface[,] cells, PaintedGroundStyle style,
        float cellSize, int seed, int originX = 0, int originY = 0)
    {
        if (style == null || style.DryGround == null || style.Water == null || style.Bridge == null)
            throw new ArgumentException("Painted ground requires all three surface materials.");
        var root = new GameObject("Painted Ground"); root.transform.SetParent(parent, false);
        var output = root.AddComponent<PaintedGroundOutput>();
        int width = cells.GetLength(0), height = cells.GetLength(1);
        output.Seed = seed; output.Width = width; output.Height = height; output.OriginX = originX; output.OriginY = originY;
        foreach (var surface in cells) { output.SurfaceCells[(int)surface]++; output.CellCount++; }
        var owner = root.AddComponent<EnvironmentMeshOwner>();
        for (int cy = 0; cy < height; cy += ChunkSize)
        for (int cx = 0; cx < width; cx += ChunkSize)
        for (int kind = 0; kind < 3; kind++)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var weights0 = new List<Color>(); var weights1 = new List<Vector4>(); var weights2 = new List<Vector2>();
            var triangles = new List<int>(); var indices = new Dictionary<Vector2Int, int>();
            float depth = kind == 1 ? WaterDepth : kind == 2 ? BridgeDepth : DryDepth;
            for (int y = cy; y < Math.Min(cy + ChunkSize, height); y++)
            for (int x = cx; x < Math.Min(cx + ChunkSize, width); x++)
            {
                int cellKind = cells[x,y] == GroundSurface.Water ? 1 : cells[x,y] == GroundSurface.Bridge ? 2 : 0;
                if (kind != cellKind) continue;
                int step = kind == 0 ? 1 : 2;
                for (int dy = 0; dy < 2; dy += step) for (int dx = 0; dx < 2; dx += step)
                {
                    int a = Vertex(x*2+dx,y*2+dy), b = Vertex(x*2+dx+step,y*2+dy);
                    int c = Vertex(x*2+dx+step,y*2+dy+step), d = Vertex(x*2+dx,y*2+dy+step);
                    triangles.AddRange(new[] {a,c,b,a,d,c});
                }
            }
            if (vertices.Count == 0) continue;
            var mesh = new Mesh {name = $"Painted ground {kind} {cx},{cy}", hideFlags = HideFlags.DontSave};
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv);
            mesh.SetColors(weights0); mesh.SetUVs(1,weights1); mesh.SetUVs(2,weights2); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            owner.Meshes.Add(mesh); owner.TriangleCount += triangles.Count/3;
            var chunk = new GameObject(mesh.name); chunk.transform.SetParent(root.transform,false);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = chunk.AddComponent<MeshRenderer>(); renderer.sharedMaterial = kind == 1 ? style.Water : kind == 2 ? style.Bridge : style.DryGround;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            chunk.AddComponent<SilhouetteParticipant>().Role = kind == 1 ? SilhouetteRole.None : SilhouetteRole.Receiver;

            int Vertex(int hx, int hy)
            {
                var key = new Vector2Int(hx,hy); if (indices.TryGetValue(key,out int existing)) return existing;
                int index = vertices.Count; indices.Add(key,index);
                float wx = hx*.5f+originX, wy = hy*.5f+originY;
                vertices.Add(new Vector3(wx*cellSize,wy*cellSize,depth)); normals.Add(Vector3.back);
                uv.Add(new Vector2(wx*cellSize/8,wy*cellSize/8));
                var w = Weights(cells,hx*.5f,hy*.5f,seed,originX,originY);
                weights0.Add(new Color(w[0],w[1],w[2],w[3])); weights1.Add(new Vector4(w[4],w[5],w[6],w[7])); weights2.Add(new Vector2(w[8],0));
                return index;
            }
        }
        return output;
    }

    public static float[] Weights(GroundSurface[,] cells, float x, float y, int seed, int originX = 0, int originY = 0)
    {
        var weights = new float[9];
        // Perturb the material sample, never the lattice: chunk edges and coast geometry remain coincident.
        uint hash = OverworldCosmetics.Hash(seed,Mathf.RoundToInt((x+originX)*2),Mathf.RoundToInt((y+originY)*2));
        x += ((hash&255)/255f-.5f)*.18f; y += ((hash>>8&255)/255f-.5f)*.18f;
        x -= .5f; y -= .5f; int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.75f,x-ix));
        float ty = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.75f,y-iy));
        Add(ix,iy,(1-tx)*(1-ty)); Add(ix+1,iy,tx*(1-ty)); Add(ix,iy+1,(1-tx)*ty); Add(ix+1,iy+1,tx*ty);
        float total = 0; foreach (float w in weights) total += w;
        if (total <= 0) weights[0] = 1;
        else for (int i=0;i<weights.Length;i++) weights[i] /= total;
        return weights;
        void Add(int a,int b,float weight)
        {
            int surface = (int)cells[Mathf.Clamp(a,0,cells.GetLength(0)-1),Mathf.Clamp(b,0,cells.GetLength(1)-1)];
            if (surface < 9) weights[surface] += weight;
        }
    }
}
