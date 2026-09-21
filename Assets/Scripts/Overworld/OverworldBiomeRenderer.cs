using System;
using System.Collections.Generic;
using EternalEnigma.Core.World;
using TWC;
using UnityEngine;

[Serializable]
public sealed class OverworldBiomeMaterial
{
    public OverworldBiome Biome;
    public Material Material;
}

/// <summary>Chunked, textured XY floor surfaces; core masks remain movement authority.</summary>
[ExecuteAlways, RequireComponent(typeof(CampaignOverworld))]
public sealed class OverworldBiomeRenderer : MonoBehaviour
{
    public OverworldBiomeMaterial[] Biomes = Array.Empty<OverworldBiomeMaterial>();
    public Material RoadMaterial;
    public Material BridgeMaterial;
    public Material BarrierMaterial;
    private TileWorldCreator creator;
    private GameObject surfaces;
    private GameObject cachedSurfaces;
    public GameObject RenderedSurfaces => surfaces != null ? surfaces : cachedSurfaces;
    private readonly List<Mesh> meshes = new();
    private Renderer[] hiddenRenderers = Array.Empty<Renderer>();

    private void OnEnable()
    {
        creator = GetComponent<TileWorldCreator>();
        creator.OnBuildLayersComplete += Build;
    }

    private void Build(TileWorldCreator _)
    {
        var grid = GetComponent<CampaignOverworld>().CurrentGrid;
        if (grid == null) return;
        Clear();
        surfaces = new GameObject("Biome Floors") { hideFlags = HideFlags.DontSave };
        surfaces.transform.SetParent(transform, false);
        // Town actors keep their root at the cell corner and their visuals half a
        // tile inside it. Match that convention without changing logical positions.
        float halfTile = creator.twcAsset.cellSize * .5f;
        surfaces.transform.localPosition = new Vector3(halfTile, halfTile, 0);
        foreach (var entry in Biomes)
            Draw(grid, OverworldLayers.Landscape(entry.Biome), entry.Material, .045f);
        foreach (var entry in Biomes)
        {
            string layer = entry.Biome == OverworldBiome.Water ? OverworldLayers.Water : OverworldLayers.Biome(entry.Biome);
            Draw(grid, layer, entry.Material, .02f);
        }
        foreach (var entry in Biomes)
        {
            if (entry.Biome == OverworldBiome.Mountain)
                Draw(grid, OverworldLayers.Mountains, entry.Material, .015f, raised: true);
            if (entry.Biome == OverworldBiome.Forest)
                Draw(grid, OverworldLayers.Trees, entry.Material, .01f, raised: true);
        }
        Draw(grid, OverworldLayers.Roads, RoadMaterial, .005f, excludeWater: true);
        Draw(grid, OverworldLayers.Bridges, BridgeMaterial, -.005f);
        hiddenRenderers = creator.worldObject.GetComponentsInChildren<Renderer>();
        foreach (var renderer in hiddenRenderers) renderer.enabled = false;
    }

    private void Draw(OverworldGrid grid, string layerName, Material material, float z, bool excludeWater = false, bool raised = false)
    {
        if (material == null || !grid.Layers.TryGetValue(layerName, out var mask)) return;
        float size = creator.twcAsset.cellSize;
        const int chunkSize = 32;
        for (int cy = 0; cy < grid.Height; cy += chunkSize)
        for (int cx = 0; cx < grid.Width; cx += chunkSize)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = cy; y < Math.Min(cy + chunkSize, grid.Height); y++)
            for (int x = cx; x < Math.Min(cx + chunkSize, grid.Width); x++)
            {
                if (!mask[x, y] || (excludeWater && grid.RequiresBoat(new GridPoint(x, y)))) continue;
                int first = vertices.Count;
                vertices.Add(new Vector3((x - .5f) * size, (y - .5f) * size, z));
                vertices.Add(new Vector3((x + .5f) * size, (y - .5f) * size, z));
                vertices.Add(new Vector3((x + .5f) * size, (y + .5f) * size, z));
                vertices.Add(new Vector3((x - .5f) * size, (y + .5f) * size, z));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                if (raised)
                {
                    vertices.Add(new Vector3(x * size, y * size, z - size * .22f));
                    uv.Add(new Vector2(.5f, .5f));
                    triangles.AddRange(new[] { first,first+4,first+1, first+1,first+4,first+2,
                        first+2,first+4,first+3, first+3,first+4,first });
                }
                else triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
            if (vertices.Count == 0) continue;
            var mesh = new Mesh { name = layerName + " floor", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh);
            var chunk = new GameObject(layerName + " " + cx + "," + cy) { hideFlags = HideFlags.DontSave };
            chunk.transform.SetParent(surfaces.transform, false);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (raised)
            {
                // Keep the biome texture while making the impassable ridge legible at player height.
                var tint = material.color * .65f;
                tint.a = material.color.a;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", tint);
                properties.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(properties);
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public GameObject ReleaseSurfacesOwnership(out Mesh[] generatedMeshes)
    {
        cachedSurfaces = surfaces;
        surfaces = null;
        generatedMeshes = meshes.ToArray();
        meshes.Clear();
        // Cached TWC renderers must stay hidden when this scene's component is disabled.
        hiddenRenderers = Array.Empty<Renderer>();
        return cachedSurfaces;
    }

    public void UseCachedSurfaces(GameObject cached) { cachedSurfaces = cached; }

    private void OnDisable()
    {
        if (creator != null) creator.OnBuildLayersComplete -= Build;
        // worldObject is a creating getter; never call it while its scene is unloading.
        foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
        hiddenRenderers = Array.Empty<Renderer>();
        Clear();
        cachedSurfaces = null;
    }

    private void Clear()
    {
        if (surfaces != null) { if (Application.isPlaying) Destroy(surfaces); else DestroyImmediate(surfaces); }
        foreach (var mesh in meshes) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        meshes.Clear();
        surfaces = null;
    }
}
