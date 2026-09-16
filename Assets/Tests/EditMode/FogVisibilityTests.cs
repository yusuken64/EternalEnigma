using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FogVisibilityTests
{
    [Test]
    public void ExploredTerrainRemainsDimWhileLiveObjectsDisappearAndReappear()
    {
        var fogObject = new GameObject("Fog test");
        var subject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var texture = new Texture2D(3, 1, TextureFormat.Alpha8, false);
        try
        {
            var fog = fogObject.AddComponent<FogOverlay>();
            typeof(FogOverlay).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fog, null);
            typeof(FogOverlay).GetField("fogTexture", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fog, texture);
            typeof(FogOverlay).GetField("cellSize", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fog, 2f);
            var map = new Minimap.MinimapTileData[3, 1];
            map[0, 0] = new() { visibility = Minimap.MinimapTileVisibility.Unseen };
            map[1, 0] = new() { visibility = Minimap.MinimapTileVisibility.Explored };
            map[2, 0] = new() { visibility = Minimap.MinimapTileVisibility.Visible };
            fog.UpdateFog(map);
            Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f));
            Assert.That(texture.GetPixel(1, 0).a, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(texture.GetPixel(2, 0).a, Is.EqualTo(1f).Within(0.01f));

            // Use rendered positions so crossing fog during a tween is handled.
            var visual = subject.AddComponent<FogHiddenVisual>();
            var lateUpdate = typeof(FogHiddenVisual).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var renderer = subject.GetComponent<Renderer>();
            foreach (int cell in new[] { 2, 1, 0, 2 })
            {
                subject.transform.position = new Vector3(cell * 2, 0, 0);
                lateUpdate.Invoke(visual, null);
                Assert.That(renderer.forceRenderingOff, Is.EqualTo(cell != 2));
                Assert.That(subject.activeSelf, Is.True, "Fog must not disable simulation.");
            }
            Assert.That(fog.IsCurrentlyVisible(new Vector3(-2, 0, 0)), Is.False);
            Assert.That(fog.IsCurrentlyVisible(new Vector3(6, 0, 0)), Is.False);

            // Newly attached effects must also be hidden, without enabling disabled renderers.
            subject.transform.position = new Vector3(2, 0, 0);
            var effect = GameObject.CreatePrimitive(PrimitiveType.Cube);
            effect.transform.SetParent(subject.transform, false);
            effect.GetComponent<Renderer>().enabled = false;
            lateUpdate.Invoke(visual, null);
            Assert.That(effect.GetComponent<Renderer>().forceRenderingOff, Is.True);
            Assert.That(effect.GetComponent<Renderer>().enabled, Is.False);
        }
        finally
        {
            // EditMode cleanup owns the texture; avoid deferred destruction by OnDestroy.
            typeof(FogOverlay).GetField("fogTexture", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fogObject.GetComponent<FogOverlay>(), null);
            Object.DestroyImmediate(subject);
            Object.DestroyImmediate(fogObject);
            Object.DestroyImmediate(texture);
        }
    }
}
