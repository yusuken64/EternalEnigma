#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class CameraBackdropTests
    {
        private const string Output = "Temp/CameraBackdrops";
        private GameTestHarness harness;
        private TestInputScope inputs;
        private IDisposable preferences;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            preferences = GamePreferences.Isolate();
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            Directory.CreateDirectory(Output);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally { inputs?.Dispose(); preferences?.Dispose(); }
        }

        private static readonly Vector2Int[] Resolutions = { new(640, 400), new(960, 540), new(960, 270) };

        private static Texture2D Render(Camera camera, Vector2Int resolution, string capture = null)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(resolution.x, resolution.y, 24);
            Texture2D image = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
                image.Apply();
                if (capture != null) File.WriteAllBytes(Output + "/" + capture + ".png", image.EncodeToPNG());
                return image;
            }
            catch { if (image != null) Object.Destroy(image); throw; }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }

        private static Vector3 Project(Camera camera, Vector2 viewport, float z)
        {
            var ray = camera.ViewportPointToRay(viewport);
            return ray.GetPoint((z - ray.origin.z) / ray.direction.z);
        }

        private static void Frame(Camera camera, CameraController controller, Vector3 target, Vector2Int resolution)
        {
            camera.aspect = (float)resolution.x / resolution.y;
            camera.orthographicSize = CameraZoom.OverworldSize;
            camera.transform.SetPositionAndRotation(target + controller.CameraOffset, Quaternion.LookRotation(-controller.CameraOffset, Vector3.up));
        }

        [UnityTest] public IEnumerator DungeonFogCoversEveryCornerAtWideZoomWithoutChangingSight()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var controller = harness.Game.PlayerController.CameraController;
            controller.enabled = false;
            var camera = controller.Camera;
            var fog = FogOverlay.Instance;
            var renderer = fog.fogOverlayQuad.GetComponent<Renderer>();
            var origin = renderer.sharedMaterial.GetVector("_FogWorldOrigin");
            var size = renderer.sharedMaterial.GetVector("_FogWorldSize");
            var originalPixels = fog.VisibilityTexture.GetPixels32();
            float height = Mathf.Abs(fog.fogOverlayQuad.transform.position.z);
            var scenery = new Bounds(new Vector3(origin.x + size.x * .5f, origin.y + size.y * .5f, -height * .5f),
                new Vector3(size.x, size.y, height));
            foreach (var resolution in Resolutions)
            for (int corner = 0; corner < 4; corner++)
            {
                var target = new Vector3((corner & 1) * size.x, (corner >> 1) * size.y, 0);
                Frame(camera, controller, target, resolution);
                var image = Render(camera, resolution, corner == 0 ? "dungeon-" + resolution.x + "x" + resolution.y : null);
                try
                {
                    int outside = 0;
                    for (int y = 0; y < resolution.y; y += 19)
                    for (int x = 0; x < resolution.x; x += 19)
                    {
                        var point = Project(camera, new Vector2((x + .5f) / resolution.x, (y + .5f) / resolution.y), fog.fogOverlayQuad.transform.position.z);
                        var bounds = renderer.bounds;
                        Assert.That(point.x, Is.InRange(bounds.min.x, bounds.max.x));
                        Assert.That(point.y, Is.InRange(bounds.min.y, bounds.max.y));
                        // Surface-based fog follows wall/actor height. Only rays missing
                        // the whole scenery volume are unambiguously outside the map.
                        if (scenery.IntersectRay(camera.ViewportPointToRay(new Vector3((x + .5f) / resolution.x, (y + .5f) / resolution.y)))) continue;
                        outside++;
                        var pixel = image.GetPixel(x, y);
                        Assert.That(Mathf.Max(pixel.r, pixel.g, pixel.b), Is.LessThan(.02f),
                            $"Fog must cover the background at {resolution}, corner {corner}, pixel {x},{y}.");
                    }
                    Assert.That(outside, Is.GreaterThan(20));
                }
                finally { Object.Destroy(image); }
            }
            CollectionAssert.AreEqual(originalPixels, fog.VisibilityTexture.GetPixels32(), "Backdrop coverage must not reveal or hide dungeon cells.");
            Assert.That(renderer.sharedMaterial.GetVector("_FogWorldOrigin"), Is.EqualTo(origin));
            Assert.That(renderer.sharedMaterial.GetVector("_FogWorldSize"), Is.EqualTo(size));
        }

        [UnityTest] public IEnumerator TownGroundAndOverworldSceneryCoverWideViewsAtEveryCorner()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var creator = town.WalkableMap.TileWorldCreator;
            var controller = town.TownPlayer.CameraController;
            controller.enabled = false;
            var camera = controller.Camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            var ground = creator.worldObject.GetComponentInChildren<PaintedGroundOutput>();
            var surroundings = creator.worldObject.GetComponentsInChildren<EnvironmentMeshOwner>().Single(o => o.name == "Town surroundings");
            Assert.That(surroundings.PropCount, Is.GreaterThan(0));
            Assert.That(surroundings.TriangleCount, Is.LessThanOrEqualTo(OverworldCosmetics.TriangleBudget));
            Assert.That(surroundings.GetComponentsInChildren<Collider>(), Is.Empty);
            float cellSize = creator.twcAsset.cellSize;
            foreach (var resolution in Resolutions)
            for (int corner = 0; corner < 4; corner++)
            {
                var target = new Vector3((corner & 1) * town.Plan.Width, (corner >> 1) * town.Plan.Height, 0) * cellSize;
                Frame(camera, controller, target, resolution);
                var image = Render(camera, resolution, corner == 0 ? "town-" + resolution.x + "x" + resolution.y : null);
                try
                {
                    int gaps = image.GetPixels32().Count(pixel => pixel.r > 245 && pixel.g < 10 && pixel.b > 245);
                    Assert.That(gaps, Is.Zero, $"Exposed background at {resolution}, town corner {corner}.");
                    for (int edge = 0; edge < 4; edge++)
                    {
                        var point = Project(camera, new Vector2(edge & 1, edge >> 1), PaintedGroundMesh.DryDepth);
                        Assert.That(point.x, Is.InRange(ground.OriginX * cellSize, (ground.OriginX + ground.Width) * cellSize));
                        Assert.That(point.y, Is.InRange(ground.OriginY * cellSize, (ground.OriginY + ground.Height) * cellSize));
                    }
                }
                finally { Object.Destroy(image); }
            }
        }

        [UnityTest] public IEnumerator TownSurroundingsReuseEveryBiomeAndRebuildDeterministically()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var kit = EnvironmentKit.Load();
            var diorama = DioramaCatalog.Load();
            float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
            int margin = TownSurroundings.MarginCells(size);
            var parent = new GameObject("Surroundings rebuild test");
            try
            {
                foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
                {
                    (int props, int triangles) first = default;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        TownSurroundings.Build(parent.transform, town.Plan, kit, kit.TreeModels, diorama, biome, size, 12345, margin, true);
                        var owner = parent.GetComponentInChildren<EnvironmentMeshOwner>();
                        var counts = (owner.PropCount, owner.TriangleCount);
                        if (pass == 0) first = counts;
                        else Assert.That(counts, Is.EqualTo(first), biome.ToString());
                        Assert.That(counts.PropCount, Is.GreaterThan(0));
                        Assert.That(counts.TriangleCount, Is.InRange(1, OverworldCosmetics.TriangleBudget));
                        Assert.That(parent.GetComponentsInChildren<Collider>(), Is.Empty);
                        Object.Destroy(owner.gameObject);
                        yield return null;
                    }
                }
            }
            finally { Object.Destroy(parent); }
        }
    }
}
#endif
