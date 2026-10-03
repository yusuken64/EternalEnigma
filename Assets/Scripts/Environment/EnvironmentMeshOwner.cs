using System.Collections.Generic;
using UnityEngine;

/// <summary>Lifetime follows the TWC layer, including cached overworld hide/restore.</summary>
public sealed class EnvironmentMeshOwner : MonoBehaviour
{
    public List<Mesh> Meshes = new();
    public List<Material> Materials = new();
    public int PropCount;
    public int TriangleCount;
    private void OnDestroy()
    {
        foreach (var mesh in Meshes)
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        foreach (var material in Materials)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
    }
}
