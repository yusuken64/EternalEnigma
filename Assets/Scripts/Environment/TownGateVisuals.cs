using EternalEnigma.Core.World;
using TMPro;
using UnityEngine;

public static class TownGateVisuals
{
    private static int DistrictSide(TownPlan plan, GridPoint cell)
    {
        int[] edges = { cell.Y, plan.Width - 1 - cell.X, plan.Height - 1 - cell.Y, cell.X };
        int side = 0; for (int i = 1; i < edges.Length; i++) if (edges[i] < edges[side]) side = i;
        return side;
    }
    internal static float DistrictRotation(TownPlan plan, GridPoint cell, bool onward) =>
        DistrictSide(plan, cell) * 90 + (onward ? 180 : 0);

    internal static GridPoint DistrictGateCell(TownPlan plan, GridPoint cell) => DistrictSide(plan, cell) switch
    {
        0 => new GridPoint(cell.X, 0),
        1 => new GridPoint(plan.Width - 1, cell.Y),
        2 => new GridPoint(cell.X, plan.Height - 1),
        _ => new GridPoint(0, cell.Y)
    };

    internal static bool DistrictApproach(TownPlan plan, GridPoint marker, int x, int y)
    {
        var gate = DistrictGateCell(plan, marker);
        return gate.X == marker.X
            ? Mathf.Abs(x - gate.X) <= 1 && y >= Mathf.Min(gate.Y, marker.Y) && y <= Mathf.Max(gate.Y, marker.Y)
            : Mathf.Abs(y - gate.Y) <= 1 && x >= Mathf.Min(gate.X, marker.X) && x <= Mathf.Max(gate.X, marker.X);
    }
    public static void Create(EnvironmentKit kit, OverworldBiome biome, Transform parent,
        GridPoint cell, float size, string label, bool dungeon, bool onward = false, float rotation = 0, GridPoint? gateCell = null)
    {
        var root = new GameObject(label);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(cell.X + .5f, cell.Y + .5f, 0) * size;
        var gate = kit.Create(dungeon ? "DungeonPortal" : "Gate", biome, root.transform, Vector3.zero, size);
        if (gateCell is { } frame)
            gate.transform.localPosition = new Vector3(frame.X - cell.X, frame.Y - cell.Y, 0) * size;
        gate.transform.localRotation = Quaternion.Euler(0, 0, rotation) * gate.transform.localRotation;
        if (!dungeon)
        {
            // Span the three-cell opening while preserving pillar widths.
            var gateMesh = Object.Instantiate(kit.Mesh("Gate"));
            gateMesh.name = "Town gate spanning exit";
            var vertices = gateMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].x += Mathf.Clamp(vertices[i].x / .3f, -1f, 1f);
            gateMesh.vertices = vertices;
            gateMesh.RecalculateNormals();
            gateMesh.RecalculateBounds();
            gate.GetComponent<MeshFilter>().sharedMesh = gateMesh;
            gate.AddComponent<EnvironmentMeshOwner>().Meshes.Add(gateMesh);
        }
        var marker = new GameObject("Gate direction indicator");
        marker.transform.SetParent(root.transform, false);
        marker.transform.localRotation = Quaternion.Euler(0, 0, rotation);
        float direction = dungeon || onward ? 1 : -1;
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
        var color = dungeon || onward ? new Color(1f, .65f, .15f) : new Color(.25f, 1f, .85f);
        tint.SetColor("_Color", color); tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
        var caption = new GameObject("Gate label").AddComponent<TextMeshPro>();
        caption.transform.SetParent(root.transform, false);
        caption.transform.localPosition = new Vector3(0, -.7f, -.12f) * size;
        caption.text = label; caption.fontSize = 2.2f * size;
        GameTypography.Apply(caption, GameTextRole.Heading);
        caption.alignment = TextAlignmentOptions.Center; caption.color = color;
        caption.rectTransform.sizeDelta = new Vector2(3, .5f) * size;
    }
}
