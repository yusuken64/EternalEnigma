using System.Collections.Generic;
using UnityEngine;

public sealed class EnvironmentBatch
{
    private readonly Transform parent;
    private readonly EnvironmentMeshOwner owner;
    private readonly Dictionary<(int, int, Material), List<CombineInstance>> groups = new();
    public EnvironmentBatch(Transform parent)
    {
        this.parent = parent;
        owner = parent.gameObject.AddComponent<EnvironmentMeshOwner>();
    }
    public void Add(Mesh mesh, Material material, Vector3 position, Vector3 scale, float rotation = 0)
    {
        var key = (Mathf.FloorToInt(position.x / 64), Mathf.FloorToInt(position.y / 64), material);
        if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
        list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(0, 0, rotation), scale) });
        owner.PropCount++; owner.TriangleCount += (int)mesh.GetIndexCount(0) / 3;
    }
    public void Finish()
    {
        foreach (var pair in groups)
        {
            var mesh = new Mesh { name = "Environment chunk", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true, false); mesh.RecalculateBounds();
            owner.Meshes.Add(mesh);
            var obj = new GameObject($"Cosmetic {pair.Key.Item1},{pair.Key.Item2} {pair.Key.Item3.name}");
            obj.transform.SetParent(parent, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key.Item3;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
