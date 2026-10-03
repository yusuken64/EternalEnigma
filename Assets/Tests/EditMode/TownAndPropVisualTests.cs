using System.IO;
using System.Linq;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class TownAndPropVisualTests
    {
        [Test]
        public void FloorObjectsTouchGroundWithoutMovingTheirLogicalRoot()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/DroppedItems" })
                .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] {
                    "Assets/Prefabs/Dungeon/Interactables/Gold.prefab",
                    "Assets/Prefabs/Dungeon/Interactables/Stairs.prefab",
                    "Assets/Prefabs/Dungeon/Traps/DamageTrap.prefab",
                    "Assets/Prefabs/Dungeon/Traps/BumpTrap.prefab" });
            foreach (var path in paths)
            {
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    instance.transform.position = new Vector3(14, 22, 0);
                    DungeonPresentation.GroundFloorObject(instance.transform);
                    Assert.That(instance.transform.position, Is.EqualTo(new Vector3(14, 22, 0)), path);
                    Assert.That(instance.GetComponentsInChildren<MeshRenderer>(true).Max(r => r.bounds.max.z),
                        Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.0001f), path);
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        [TestCase("DamageTrap", "SpikeTrap")]
        [TestCase("BumpTrap", "BumpTrap")]
        public void TrapPrefabsUseImportedModelsAndCanHideTheirEntireVisual(string prefab, string model)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/Traps/" + prefab + ".prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                var trap = instance.GetComponent<Trap>();
                Assert.That(trap.VisualObject.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(Resources.Load<Mesh>("DungeonProps/" + model)));
                Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
                trap.VisualObject.SetActive(false);
                Assert.That(instance.GetComponentsInChildren<Renderer>(), Is.Empty);
                trap.VisualObject.SetActive(true);
                Assert.That(instance.GetComponentsInChildren<Renderer>(), Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ImportedPropsFitCellsAndRender()
        {
            var root = new GameObject("Prop preview");
            try
            {
                string[] ids = { "Chest", "Crate", "Urn", "CrystalOre", "HazardPool", "Herbs", "Mushrooms", "Caltrops", "SpikeTrap", "BumpTrap" };
                for (int i = 0; i < ids.Length; i++)
                {
                    var slot = new GameObject(ids[i]); slot.transform.SetParent(root.transform);
                    slot.transform.localPosition = new Vector3(i % 4 * 2.5f, i / 4 * 3, 0);
                    var model = DungeonPropModels.Create(ids[i], slot.transform, 2);
                    var bounds = model.GetComponent<Renderer>().bounds;
                    Assert.That(bounds.size.x, Is.LessThanOrEqualTo(1.45f));
                    Assert.That(bounds.size.y, Is.LessThanOrEqualTo(1.45f));
                    Assert.That(bounds.max.z, Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.0001f),"Prop base must touch the rendered floor.");
                    Assert.That(model.GetComponentsInChildren<Collider>(), Is.Empty);
                }
                Capture(root, new Vector3(4.75f, 4, 0), 10.5f, "DungeonProps");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TownHasExpandedEnclosureAndReachableGate()
        {
            var host = new GameObject("Town visual test");
            var creator = host.AddComponent<TileWorldCreator>();
            var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/TileWorldCreator/VillageLSystemAsset.asset"));
            creator.twcAsset = asset;
            try
            {
                CoreTownLayerGenerator.Configure(asset, TownSceneLoader.Default);
                creator.SetCustomRandomSeed(42); creator.ExecuteAllBlueprintLayers();
                CoreLayoutCache.ClearResultFlags(asset); creator.ExecuteAllBuildLayers(true);
                Assert.That(CoreLayoutCache.TryGetTown(creator, out var plan), Is.True);
                var walkable = plan.Layers[TownLayers.Walkable].ToArray();
                Assert.That(walkable[plan.Exit.X, plan.Exit.Y], Is.True);
                Assert.That(GridMovement.IsWalkable(walkable, new Vector3Int(-1, 0, 0)), Is.False);
                Assert.That(GridMovement.IsWalkable(walkable, new Vector3Int(plan.Width, 0, 0)), Is.False);
                Assert.That(creator.worldObject.GetComponentsInChildren<Transform>().Any(t => t.name == "Town gate"), Is.True);
                float size = asset.cellSize;
                var bounds = creator.worldObject.GetComponentsInChildren<Renderer>().First().bounds;
                foreach (var r in creator.worldObject.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                Assert.That(bounds.min.x, Is.LessThan(-3 * size));
                Assert.That(bounds.max.x, Is.GreaterThan((plan.Width + 3) * size));
                Capture(creator.worldObject, new Vector3(plan.Width, plan.Height, 0) * size * .5f,
                    (Mathf.Max(plan.Width, plan.Height) + 10) * size, "TownEnclosure");
            }
            finally
            {
                if (creator.worldObject != null) Object.DestroyImmediate(creator.worldObject);
                Object.DestroyImmediate(host); Object.DestroyImmediate(asset);
            }
        }

        internal static void Capture(GameObject root, Vector3 center, float span, string name)
        {
            var cameraObject = new GameObject("Capture camera"); var camera = cameraObject.AddComponent<Camera>();
            var lightObject = new GameObject("Capture light"); var light = lightObject.AddComponent<Light>();
            var target = new RenderTexture(1200, 1000, 24); var image = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var ambient = RenderSettings.ambientLight;
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
                light.type = LightType.Directional; light.intensity = 1.2f; light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(25, -30, 0); RenderSettings.ambientLight = Color.gray;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .07f, .09f); camera.orthographic = true; camera.orthographicSize = span * .5f;
                camera.transform.position = center + new Vector3(0, -span * .45f, -span * 2);
                camera.transform.LookAt(center, Vector3.up); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); image.Apply();
                Directory.CreateDirectory("Docs/Art/Previews"); File.WriteAllBytes("Docs/Art/Previews/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; RenderSettings.ambientLight = ambient; camera.targetTexture = null;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
            }
        }
    }
}
