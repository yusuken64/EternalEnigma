using EternalEnigma.Core.World;
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
    // Place the plaque on the gate shoulder with the clearest view from the gameplay camera.
    private static Vector3 DistrictSignOffset(TownPlan plan, GridPoint marker)
    {
        var gate = DistrictGateCell(plan, marker);
        var wall = Quaternion.Euler(0, 0, DistrictRotation(plan, marker, false));
        var inset = Mathf.Clamp(Mathf.Abs(marker.X - gate.X) + Mathf.Abs(marker.Y - gate.Y), 1.15f, 3f);
        var center = new Vector3(gate.X, gate.Y, 0);
        var inward = wall * Vector3.up;
        var shoulder = wall * Vector3.right * 2.45f;
        if (shoulder.y < -.01f) shoulder = -shoulder;
        int Clearance(Vector3 point)
        {
            int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y), score = 0;
            for (int row = y - 1; row <= y; row++)
            for (int column = x - 1; column <= x + 1; column++)
            {
                var cell = new GridPoint(column, row);
                if (plan.Contains(cell) && plan.IsWalkable(cell) && !plan.Layers[TownLayers.Buildings].At(cell))
                    score += column == x && row == y ? 6 : 1;
            }
            return score;
        }
        var mount = center + inward * inset + shoulder;
        int best = -1;
        foreach (float depth in new[] { inset, inset + 1f, Mathf.Max(1f, inset - 1f), inset + 2f })
        foreach (int side in new[] { 1, -1 })
        {
            var candidate = center + inward * depth + shoulder * side;
            int score = Clearance(candidate);
            if (score <= best) continue;
            mount = candidate;
            best = score;
        }
        return mount - new Vector3(marker.X, marker.Y, 0);
    }

    public static void Create(EnvironmentKit kit, OverworldBiome biome, Transform parent,
        GridPoint cell, float size, string label, bool dungeon, bool onward = false, float rotation = 0, TownPlan district = null)
    {
        var root = new GameObject(label);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(cell.X + .5f, cell.Y + .5f, 0) * size;
        var gate = kit.Create(dungeon ? "DungeonPortal" : "Gate", biome, root.transform, Vector3.zero, size);
        if (district != null)
        {
            var frame = DistrictGateCell(district, cell);
            gate.transform.localPosition = new Vector3(frame.X - cell.X, frame.Y - cell.Y, 0) * size;
        }
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
        if (dungeon) return; // The entrance building carries the dungeon name and opening status.
        var mount = new Vector3(2.18f, .04f, 0);
        var emblem = WorldSign.Emblem.Gate;
        if (district != null)
        {
            mount = DistrictSignOffset(district, cell);
            emblem = onward ? WorldSign.Emblem.DistrictExit : WorldSign.Emblem.Dungeon;
        }
        WorldSign.Create(root.transform, mount, size, emblem, label);
    }
}
