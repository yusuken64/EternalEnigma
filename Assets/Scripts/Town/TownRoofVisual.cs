using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>A visual-only roof for one door and its building body.</summary>
public sealed class TownRoofVisual : MonoBehaviour
{
    public GridPoint Door { get; private set; }
    public ShopRoom Room { get; private set; }
    private Mesh ownedMesh;

    public void Initialize(GridPoint door, ShopRoom room, IReadOnlyCollection<GridPoint> cells,
        float size, Material material)
    {
        Door = door;
        Room = room;
        var body = new HashSet<GridPoint>(cells);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const float eave = -1.18f, ridge = -1.72f;
        foreach (var cell in body)
        {
            // A horizontal run shares one ridge. Shorter rows around a notch get
            // their own ridge, while adjacent full rows remain one continuous pitch.
            int left = cell.X, right = cell.X;
            while (body.Contains(new GridPoint(left - 1, cell.Y))) left--;
            while (body.Contains(new GridPoint(right + 1, cell.Y))) right++;
            float centre = (left + right + 1) * .5f;
            float halfWidth = (right - left + 1) * .5f;
            float Height(float x) => eave + (ridge - eave) * (1f - Mathf.Abs(x - centre) / halfWidth);
            float x0 = cell.X, xm = cell.X + .5f, x1 = cell.X + 1f;
            AddQuad(vertices, triangles, size,
                new Vector3(x0, cell.Y, Height(x0)), new Vector3(x0, cell.Y + 1, Height(x0)),
                new Vector3(xm, cell.Y + 1, Height(xm)), new Vector3(xm, cell.Y, Height(xm)));
            AddQuad(vertices, triangles, size,
                new Vector3(xm, cell.Y, Height(xm)), new Vector3(xm, cell.Y + 1, Height(xm)),
                new Vector3(x1, cell.Y + 1, Height(x1)), new Vector3(x1, cell.Y, Height(x1)));
        }
        ownedMesh = new Mesh { name = "Town roof " + door };
        ownedMesh.SetVertices(vertices);
        ownedMesh.SetTriangles(triangles, 0);
        ownedMesh.RecalculateNormals();
        ownedMesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, float size,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a * size); vertices.Add(b * size);
        vertices.Add(c * size); vertices.Add(d * size);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private void OnDestroy()
    {
        if (ownedMesh != null) Destroy(ownedMesh);
    }
}
