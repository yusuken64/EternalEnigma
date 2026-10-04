using System.Collections.Generic;
using UnityEngine;

/// <summary>Lifetime follows the TWC layer, including cached overworld hide/restore.</summary>
public sealed class EnvironmentMeshOwner : MonoBehaviour
{
    public List<Mesh> Meshes = new();
    public List<Material> Materials = new();
    public List<Texture2D> Textures = new();
    public int PropCount;
    public int TriangleCount;
    private static void ReleaseOwned(Object asset)
    {
        if (asset == null) return;
#if UNITY_EDITOR
        // A baked preview may still reference a project mesh or material. The
        // owner only releases generated instances, never imported assets.
        if (UnityEditor.EditorUtility.IsPersistent(asset)) return;
#endif
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
    private void OnDestroy()
    {
        foreach (var mesh in Meshes) ReleaseOwned(mesh);
        foreach (var material in Materials) ReleaseOwned(material);
        foreach (var texture in Textures) ReleaseOwned(texture);
    }
}
