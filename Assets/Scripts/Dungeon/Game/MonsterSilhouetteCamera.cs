#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Orthographic ground projection of the monster's visible triangles, including rigid equipment.
internal sealed class MonsterSilhouetteCamera : IDisposable
{
    const int Resolution = 1024;
    const int MeasurementLayer = 31;
    readonly Camera camera = new GameObject("Footprint measurement", typeof(Camera)).GetComponent<Camera>();
    readonly RenderTexture target = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
    readonly Texture2D image = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
    readonly Shader shader = Shader.Find("Unlit/Color");

    internal MonsterSilhouetteCamera()
    {
        camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
        camera.enabled = false;
        camera.orthographic = true;
        camera.aspect = 1;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << MeasurementLayer;
        camera.targetTexture = target;
    }

    internal Vector2 Center(Enemy enemy)
    {
        var renderers = enemy.ModelRoot.GetComponentsInChildren<Renderer>()
            .Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
        if (renderers.Length == 0) throw new InvalidOperationException("No visible monster mesh on " + enemy.name);
        var layers = renderers.Select(r => r.gameObject).Distinct().Select(go => (go, go.layer)).ToArray();
        var hidden = renderers.Where(r => r.forceRenderingOff).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        camera.scene = enemy.gameObject.scene;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) + 1f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10f);
        var active = RenderTexture.active;
        try
        {
            foreach (var (go, _) in layers) go.layer = MeasurementLayer;
            foreach (var r in hidden) r.forceRenderingOff = false;
            // Imported animation bounds sometimes clip the current pose. Frame the whole outline.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                camera.RenderWithShader(shader, "");
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0, false);
                var pixels = image.GetRawTextureData<Color32>();
                int minX = Resolution, minY = Resolution, maxX = -1, maxY = -1;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a < 128) continue;
                    int x = i % Resolution, y = i / Resolution;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (maxX >= minX && minX > 0 && minY > 0 && maxX < Resolution - 1 && maxY < Resolution - 1)
                    return (Vector2)camera.transform.position + camera.orthographicSize * new Vector2(
                        (minX + maxX + 1f) / Resolution - 1f, (minY + maxY + 1f) / Resolution - 1f);
                camera.orthographicSize *= 2;
            }
            throw new InvalidOperationException("Could not frame the complete monster silhouette on " + enemy.name + " facing " + enemy.CurrentFacing);
        }
        finally
        {
            RenderTexture.active = active;
            foreach (var r in hidden) r.forceRenderingOff = true;
            foreach (var (go, layer) in layers) go.layer = layer;
        }
    }

    public void Dispose()
    {
        camera.targetTexture = null;
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(image);
        RenderTexture.ReleaseTemporary(target);
    }
}
#endif
