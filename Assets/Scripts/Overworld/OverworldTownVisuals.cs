using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Cached, collider-free town miniatures. Gameplay remains at each GridTown.Entrance.</summary>
internal static class OverworldTownVisuals
{
    public static void Build(OverworldGrid grid, Transform parent, float cellSize,
        Material stone, Material paving, List<Mesh> ownedMeshes)
    {
        foreach (var town in grid.TownFootprints)
        {
            var root = new GameObject("Town " + town.LocationId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(town.Entrance.X, town.Entrance.Y, 0) * cellSize;
            var walls = new Geometry(town, cellSize);
            var roofs = new Geometry(town, cellSize);
            var floor = new Geometry(town, cellSize);
            floor.Box(-2.5f, 2.5f, -.5f, 4.5f, .012f, .03f);

            // Continuous battlement ring, with precisely one full-cell gateway at side=0, depth=0.
            for (int depth = 0; depth < GridTown.Size; depth++)
            for (int side = -GridTown.Size / 2; side <= GridTown.Size / 2; side++)
            {
                if (side == 0 && depth == 0) continue;
                if (depth != 0 && depth != GridTown.Size - 1 && Mathf.Abs(side) != GridTown.Size / 2) continue;
                bool corner = Mathf.Abs(side) == 2 && (depth == 0 || depth == 4);
                float halfX = depth == 0 || depth == 4 ? .5f : .25f;
                float halfY = Mathf.Abs(side) == 2 ? .5f : .25f;
                float height = corner ? .85f : .55f;
                walls.Box(side - halfX, side + halfX, depth - halfY, depth + halfY, 0, height);
                if (depth == 0 || depth == 4)
                    for (int crenel = -1; crenel <= 1; crenel++)
                        walls.Box(side + crenel * .34f - .1f, side + crenel * .34f + .1f,
                            depth - halfY, depth + halfY, height, height + .15f);
                else
                    for (int crenel = -1; crenel <= 1; crenel++)
                        walls.Box(side - halfX, side + halfX, depth + crenel * .34f - .1f,
                            depth + crenel * .34f + .1f, height, height + .15f);
            }

            // Gateposts leave the entrance center and its approach unobstructed from above.
            foreach (float side in new[] { -.65f, .65f })
                walls.Box(side - .15f, side + .15f, -.3f, .3f, 0, .85f);
            foreach (var house in new[] { new Vector2(-.85f, 1.3f), new Vector2(.85f, 1.3f), new Vector2(0, 3) })
            {
                walls.Box(house.x - .48f, house.x + .48f, house.y - .45f, house.y + .45f, .03f, .65f);
                roofs.Roof(house.x, house.y, .58f, .55f, .65f, 1.05f);
            }
            floor.Render("Courtyard", root.transform, paving != null ? paving : stone, new Color(.73f, .66f, .49f), ownedMeshes);
            walls.Render("Walls and houses", root.transform, stone, new Color(.66f, .65f, .58f), ownedMeshes);
            roofs.Render("Roofs", root.transform, stone, new Color(.18f, .48f, .3f), ownedMeshes);
        }
    }

    private sealed class Geometry
    {
        private readonly GridTown town;
        private readonly float size;
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();
        private readonly List<Vector2> uv = new();
        public Geometry(GridTown town, float size) { this.town = town; this.size = size; }

        private void Face(params Vector3[] points)
        {
            int first = vertices.Count;
            foreach (var p in points)
            {
                vertices.Add(new Vector3(-town.Inward.Y * p.x + town.Inward.X * p.y,
                    town.Inward.X * p.x + town.Inward.Y * p.y, -p.z) * size);
                uv.Add(new Vector2(p.x + p.y, p.z));
            }
            // Side/depth/height -> game XY/-Z has positive determinant.
            for (int i = 1; i < points.Length - 1; i++)
                triangles.AddRange(new[] { first, first + i, first + i + 1 });
        }

        public void Box(float x0, float x1, float y0, float y1, float bottom, float top)
        {
            var a = new Vector3(x0, y0, bottom); var b = new Vector3(x1, y0, bottom);
            var c = new Vector3(x1, y1, bottom); var d = new Vector3(x0, y1, bottom);
            var e = new Vector3(x0, y0, top); var f = new Vector3(x1, y0, top);
            var g = new Vector3(x1, y1, top); var h = new Vector3(x0, y1, top);
            Face(a, d, c, b); Face(e, f, g, h);
            Face(a, b, f, e); Face(d, h, g, c); Face(a, e, h, d); Face(b, c, g, f);
        }

        public void Roof(float x, float y, float halfX, float halfY, float bottom, float top)
        {
            var a = new Vector3(x - halfX, y - halfY, bottom);
            var b = new Vector3(x + halfX, y - halfY, bottom);
            var c = new Vector3(x + halfX, y + halfY, bottom);
            var d = new Vector3(x - halfX, y + halfY, bottom);
            var e = new Vector3(x, y - halfY, top); var f = new Vector3(x, y + halfY, top);
            Face(a, b, e); Face(d, f, c); Face(a, e, f, d); Face(b, c, f, e); Face(a, d, c, b);
        }

        public void Render(string name, Transform parent, Material material, Color tint, List<Mesh> ownedMeshes)
        {
            var mesh = new Mesh { name = "Town " + name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", tint); properties.SetColor("_BaseColor", tint);
            renderer.SetPropertyBlock(properties);
        }
    }
}