using System;
using EternalEnigma.Core.World;
using UnityEngine;

public static class DungeonPropModels
{
    public static string ModelFor(DungeonSceneryKind kind, OverworldBiome biome)
    {
        if (kind == DungeonSceneryKind.Hazard) return "HazardPool";
        if (kind == DungeonSceneryKind.Container)
            return biome == OverworldBiome.Desert || biome == OverworldBiome.Marsh ? "Urn" : "Chest";
        return biome == OverworldBiome.Mountain || biome == OverworldBiome.Tundra || biome == OverworldBiome.Volcanic
            ? "CrystalOre" : biome == OverworldBiome.Desert ? "Urn" : "Crate";
    }

    public static GameObject Create(string id, Transform parent, float cellSize)
    {
        var mesh = Resources.Load<Mesh>("DungeonProps/" + id);
        var material = Resources.Load<Material>("DungeonProps/Palette");
        if (mesh == null || material == null)
            throw new InvalidOperationException("Missing Blender dungeon prop: " + id + ". Run Art/Import Dungeon Props.");
        var visual = new GameObject(id);
        visual.transform.SetParent(parent, false);
        var bounds = mesh.bounds;
        float scale = cellSize * .72f / Mathf.Max(bounds.size.x, bounds.size.y);
        scale = Mathf.Min(scale, cellSize * .65f / Mathf.Max(.01f, bounds.size.z));
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(cellSize * .5f - bounds.center.x * scale,
            cellSize * .5f - bounds.center.y * scale, DungeonPresentation.GroundPlaneZ - bounds.max.z * scale);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = material;
        return visual;
    }
}
