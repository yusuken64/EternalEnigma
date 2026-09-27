using EternalEnigma.Core.World;
using TMPro;
using UnityEngine;

public static class TownGateVisuals
{
    public static void Create(EnvironmentKit kit, OverworldBiome biome, Transform parent,
        GridPoint cell, float size, string label, bool dungeon)
    {
        var root = new GameObject(label);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(cell.X + .5f, cell.Y + .5f, 0) * size;
        kit.Create(dungeon ? "DungeonPortal" : "Gate", biome, root.transform, Vector3.zero, size);
        var marker = new GameObject("Gate direction indicator");
        marker.transform.SetParent(root.transform, false);
        float direction = dungeon ? 1 : -1;
        var mesh = new Mesh { name = "Gate arrow" };
        mesh.vertices = new[] { new Vector3(-.28f, -.08f, -.035f) * size,
            new Vector3(.28f, -.08f, -.035f) * size,
            new Vector3(0, direction * .36f, -.035f) * size };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        root.AddComponent<EnvironmentMeshOwner>().Meshes.Add(mesh);
        marker.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = marker.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = kit.Paving;
        var tint = new MaterialPropertyBlock();
        var color = dungeon ? new Color(1f, .65f, .15f) : new Color(.25f, 1f, .85f);
        tint.SetColor("_Color", color); tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
        var caption = new GameObject("Gate label").AddComponent<TextMeshPro>();
        caption.transform.SetParent(root.transform, false);
        caption.transform.localPosition = new Vector3(0, -.7f, -.12f) * size;
        caption.text = label; caption.fontSize = 2.2f * size;
        caption.alignment = TextAlignmentOptions.Center; caption.color = color;
        caption.rectTransform.sizeDelta = new Vector2(3, .5f) * size;
    }
}
