#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonVisibilityTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        bool asyncShaders;
        const string Folder = "Temp/DungeonVisibility";
        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            harness.Game.AdvanceFloor();
            yield return harness.WaitForIdle();
            Directory.CreateDirectory(Folder);
            foreach (var name in new[] { "Eternal Enigma/Dungeon Boundary Cutaway", "Custom/FogOverlayWebGL", "Hidden/EternalEnigma/GroundSilhouette" })
            {
                var shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name);
                Assert.That(shader.isSupported && !ShaderUtil.ShaderHasError(shader), Is.True, name);
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally { ShaderUtil.allowAsyncCompilation = asyncShaders; inputs?.Dispose(); }
        }

        Vector3Int Corridor(bool horizontal)
        {
            var dungeon = harness.Game.CurrentDungeon;
            var along = horizontal ? Vector3Int.right : Vector3Int.up;
            var across = horizontal ? Vector3Int.up : Vector3Int.right;
            return Enumerable.Range(0, dungeon.dungeonWidth * dungeon.dungeonHeight)
                .Select(i => new Vector3Int(i % dungeon.dungeonWidth, i / dungeon.dungeonWidth, 0))
                .First(c => Enumerable.Range(-1, 3).All(i => dungeon.IsWalkable(c + along * i) &&
                    !dungeon.IsWalkable(c + along * i - across) && !dungeon.IsWalkable(c + along * i + across)) &&
                    !harness.Game.AllCharacters.Any(a => a != harness.Ally && a.ToBounds().Contains(c)));
        }

        void Frame(Vector3 center, float size = 6)
        {
            var controller = harness.Game.PlayerController.CameraController;
            controller.enabled = false;
            var camera = controller.Camera;
            camera.transform.SetPositionAndRotation(center + controller.CameraOffset, Quaternion.LookRotation(-controller.CameraOffset, Vector3.up));
            camera.orthographicSize = size;
            camera.aspect = 1280f / 900;
        }

        static Texture2D Render(Camera camera, string name = null)
        {
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 900, 24);
            var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                image.Apply();
                if (name != null) File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
                return image;
            }
            catch { Object.Destroy(image); throw; }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        [UnityTest]
        public IEnumerator CorridorsKeepTheirGeometryAndSightWhileTheForegroundWallLowers()
        {
            var game = harness.Game;
            var dungeon = game.CurrentDungeon;
            var cutaway = Object.FindFirstObjectByType<DungeonBoundaryCutaway>();
            var materials = cutaway.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial)
                .Where(m => m.HasProperty("_ForegroundWallHeight")).Distinct().ToArray();
            var meshes = cutaway.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
            var vertices = meshes.Select(m => m.vertices).ToArray();
            foreach (bool horizontal in new[] { true, false })
            {
                var cell = Corridor(horizontal);
                harness.PlaceAlly(cell);
                harness.Ally.SetFacing(Facing.Down);
                game.RefreshSight();
                yield return null;
                Frame(dungeon.CellCenterToWorld(cell));
                var pixels = FogOverlay.Instance.VisibilityTexture.GetPixels32();
                string name = horizontal ? "horizontal" : "vertical";
                foreach (var material in materials) material.SetFloat("_ForegroundWallHeight", cutaway.Height);
                var tall = Render(Camera.main, name + "-full-height");
                foreach (var material in materials) material.SetFloat("_ForegroundWallHeight", cutaway.ForegroundHeight);
                var low = Render(Camera.main, name + "-cutaway");
                try
                {
                    int changed = tall.GetPixels32().Zip(low.GetPixels32(), (a, b) => Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b))
                        .Count(d => d > 20);
                    if (horizontal) Assert.That(changed, Is.GreaterThan(500), "The camera-side wall below the character must visibly lower in a horizontal corridor.");
                    CollectionAssert.AreEqual(pixels, FogOverlay.Instance.VisibilityTexture.GetPixels32(), "Rendering must not reveal gameplay cells.");
                }
                finally { Object.Destroy(tall); Object.Destroy(low); }
            }
            for (int i = 0; i < meshes.Length; i++) CollectionAssert.AreEqual(vertices[i], meshes[i].vertices, "Cutaway is shader-only.");
        }

        [UnityTest]
        public IEnumerator FogFollowsVisibleGroundAndTallSurfacesAndKeepsUnseenCellsOpaque()
        {
            var game = harness.Game;
            var dungeon = game.CurrentDungeon;
            var cell = Corridor(true);
            harness.PlaceAlly(cell);
            game.RefreshSight();
            yield return null;
            var fog = FogOverlay.Instance;
            var camera = Camera.main;
            var center = dungeon.CellCenterToWorld(cell);
            var originalPixels = fog.VisibilityTexture.GetPixels32();
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("Standard"));
            material.color = Color.magenta;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.magenta);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            marker.transform.localScale = Vector3.one * .35f;
            harness.Ally.VisualParent.SetActive(false);
            try
            {
                Color Sample()
                {
                    var image = Render(camera);
                    try
                    {
                        var pixel = camera.WorldToViewportPoint(marker.transform.position);
                        return image.GetPixel(Mathf.RoundToInt(pixel.x * image.width), Mathf.RoundToInt(pixel.y * image.height));
                    }
                    finally { Object.Destroy(image); }
                }
                foreach (float zoom in new[] { 4f, 9f })
                foreach (float height in new[] { .03f, 3f })
                foreach (var offset in new[] { new Vector2(-.65f,0), Vector2.zero, new Vector2(.65f,0), new Vector2(0,-1.05f) })
                {
                    Frame(center, zoom);
                    marker.transform.position = center + new Vector3(offset.x, offset.y, -height);
                    fog.fogOverlayQuad.SetActive(false);
                    var clear = Sample();
                    Assert.That(clear.r + clear.b, Is.GreaterThan(1.5f), "Probe must be in front of visible geometry.");
                    fog.fogOverlayQuad.SetActive(true);
                    var visible = Sample();
                    Assert.That(Vector4.Distance(clear, visible), Is.LessThan(.06f), $"Visible surface fogged at height {height}, offset {offset}, zoom {zoom}.");
                }
                var map = game.PlayerController.Minimap.dungeonMap;
                var unseen = Enumerable.Range(0, dungeon.dungeonWidth * dungeon.dungeonHeight)
                    .Select(i => new Vector3Int(i % dungeon.dungeonWidth, i / dungeon.dungeonWidth, 0))
                    .First(c => dungeon.IsWalkable(c) && map[c.x,c.y].visibility == Minimap.MinimapTileVisibility.Unseen);
                center = dungeon.CellCenterToWorld(unseen);
                camera.transform.SetPositionAndRotation(center + Vector3.back * 20, Quaternion.identity);
                marker.transform.position = center + Vector3.back * .03f;
                var hidden = Sample();
                Assert.That(Mathf.Max(hidden.r, hidden.g, hidden.b), Is.LessThan(.02f), "Unseen terrain remains opaque even with a clear view from above.");
                fog.fogOverlayQuad.SetActive(false);
                var revealed = Sample();
                Assert.That(revealed.r + revealed.b, Is.GreaterThan(1.5f), "The opaque result must come from fog, not a blocked probe.");
                CollectionAssert.AreEqual(originalPixels, fog.VisibilityTexture.GetPixels32());
            }
            finally
            {
                harness.Ally.VisualParent.SetActive(true);
                fog.fogOverlayQuad.SetActive(true);
                Object.Destroy(marker); Object.Destroy(material);
            }
        }
    }
}
#endif
