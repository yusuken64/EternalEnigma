using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TWC;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class DungeonPropVisualTests
    {
        [TestCase(true)] [TestCase(false)]
        public void DoorFitsOneCellInBothCorridorDirectionsWithTexturedSurfaces(bool spansX)
        {
            var host = new GameObject("Door visual regression");
            var asset = ScriptableObject.CreateInstance<TileWorldCreatorAsset>(); asset.cellSize = 2;
            try
            {
                var creator = host.AddComponent<TileWorldCreator>(); creator.twcAsset = asset;
                var dungeon = host.AddComponent<TileWorldDungeon>(); dungeon.Interactables = new();
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/TileWorldDungeon.prefab").GetComponent<TileWorldDungeon>();
                dungeon.LockedDoorPrefab = template.LockedDoorPrefab;
                dungeon.Setup(creator, DungeonFloorGenerator.Generate(new DungeonFloorOptions(71)));
                var cells = Enumerable.Range(1, dungeon.Floor.Width - 2).SelectMany(x => Enumerable.Range(1, dungeon.Floor.Height - 2).Select(y => new Vector3Int(x, y)));
                var cell = cells.First(p => dungeon.IsFloorCell(p) &&
                    (!dungeon.IsFloorCell(p + Vector3Int.left) && !dungeon.IsFloorCell(p + Vector3Int.right)) == spansX);
                var prop = DungeonProp.Create(dungeon, new DungeonScenery(cell.ToGridPoint(), DungeonSceneryKind.Door, 10, 0, SceneryReward.None), null);
                var renderers = prop.GetComponentsInChildren<MeshRenderer>();
                var bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var center = dungeon.CellToWorld(cell) + new Vector3(1, 1, 0);
                Assert.That(spansX ? bounds.size.x : bounds.size.y, Is.EqualTo(2).Within(.01f));
                Assert.That(spansX ? bounds.size.y : bounds.size.x, Is.LessThan(.4f), "Door must span its broad face, not its thickness.");
                Assert.That(bounds.size.z, Is.InRange(2f, 3.5f));
                Assert.That(bounds.center.x, Is.EqualTo(center.x).Within(.001f));
                Assert.That(bounds.center.y, Is.EqualTo(center.y).Within(.001f));
                Assert.That(bounds.max.z, Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.001f));
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        Assert.That(material != null && material.mainTexture != null, Is.True, "Every door surface must be textured.");
                Assert.That(prop.IsClosedDoor, Is.True); Assert.That(dungeon.IsWalkable(cell), Is.False);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void EnemyStatueMatchesSouthFacingEnemyGeometryAtAuthoredSize()
        {
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/Enemies/Enemy_BlackKnight.prefab")).GetComponent<Enemy>();
            var points = new List<Vector3>();
            try
            {
                enemy.SetFacing(Facing.Down);
                var position = enemy.Animator.transform.localPosition;
                var rotation = enemy.Animator.transform.localRotation;
                var scale = enemy.Animator.transform.localScale;
                enemy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                enemy.Animator.Rebind();
                var idle = enemy.AnimationStates("IdleNormal").First();
                enemy.Animator.Play(Animator.StringToHash($"{enemy.Animator.GetLayerName(0)}.{idle}"), 0, 0);
                enemy.Animator.Update(0);
                enemy.Animator.transform.SetLocalPositionAndRotation(position, rotation);
                enemy.Animator.transform.localScale = scale;
                foreach (var renderer in enemy.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer))
                {
                    var skin = renderer as SkinnedMeshRenderer;
                    var mesh = skin != null ? new Mesh() : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    try
                    {
                        if (skin != null) skin.BakeMesh(mesh, true);
                        points.AddRange(mesh.vertices.Select(renderer.transform.TransformPoint));
                    }
                    finally { if (skin != null) Object.DestroyImmediate(mesh); }
                }
                var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p);
                var statue = Resources.Load<GameObject>("DungeonProps/StoneHulkStatue");
                var actual = statue.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(Vector3.Distance(actual.bounds.size, bounds.size), Is.LessThan(.002f), "Enemy props retain the enemy's width, depth and height.");
                var offset = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
                var vertices = actual.vertices;
                Assert.That(vertices.Length, Is.EqualTo(points.Count));
                for (int i = 0; i < vertices.Length; i++)
                    Assert.That(Vector3.Distance(vertices[i], points[i] - offset), Is.LessThan(.002f), "Statue must preserve the south-facing pose.");
                Assert.That(statue.GetComponentsInChildren<Animator>(), Is.Empty);
                Assert.That(statue.GetComponentsInChildren<Enemy>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(enemy.gameObject); }
        }
    }
}
