#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonGridAlignmentTests
    {
        GameTestHarness harness;
        const string Folder = "Temp/DungeonGridAlignment";
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            Directory.CreateDirectory(Folder);
        }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        static Vector3 Center(TileWorldDungeon dungeon, Vector3Int cell)
        {
            float size = dungeon.CellToWorld(Vector3Int.right).x;
            return new Vector3((cell.x + .5f) * size, (cell.y + .5f) * size, 0);
        }

        static void AssertCentered(Character actor, TileWorldDungeon dungeon)
        {
            var expected = Center(dungeon, actor.TilemapPosition);
            Assert.That(Vector2.Distance(actor.VisualParent.transform.position, expected), Is.LessThan(.001f),
                actor.name + " model pivot must be at the occupied cell's center.");
            var rings = actor.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.name == "Circle" || r.name == "Turn ring").ToArray();
            Assert.That(rings, Is.Not.Empty, actor.name);
            foreach (var ring in rings)
                Assert.That(Vector2.Distance(ring.bounds.center, expected), Is.LessThan(.001f), actor.name + "/" + ring.name);
            Assert.That(Vector3.Distance(actor.transform.position, dungeon.CellToWorld(actor.TilemapPosition)), Is.LessThan(.001f),
                "Logical roots stay at the cell origin for movement and occupancy.");
        }

        [UnityTest]
        public IEnumerator HeroAndRingsHaveEqualWallClearanceInBothCorridorDirections()
        {
            var game = harness.Game;
            // The entry floor is a throne room; audit the generated corridors on the next floor.
            game.AdvanceFloor();
            yield return harness.WaitForIdle();
            var dungeon = game.CurrentDungeon;
            var ally = harness.Ally;
            var boundaries = Object.FindObjectsByType<BiomeDecorationSurfaceSet>(FindObjectsSortMode.None)
                .SelectMany(s => s.SolidBounds.Select(b => BiomeDecorationSurfaceSet.TransformBounds(b, s.transform.localToWorldMatrix)))
                .Where(b => b.size.z > .1f).ToArray();
            var report = new StringBuilder("corridor,cell,center_error_x,center_error_y,negative_wall,positive_wall,negative_ring_gap,positive_ring_gap\n");
            foreach (var across in new[] { Vector3Int.right, Vector3Int.up })
            {
                var along = new Vector3Int(across.y, across.x, 0);
                var cells = Enumerable.Range(0, dungeon.dungeonWidth * dungeon.dungeonHeight)
                    .Select(i => new Vector3Int(i % dungeon.dungeonWidth, i / dungeon.dungeonWidth, 0))
                    .Where(c => Enumerable.Range(-1, 3).All(i => dungeon.IsWalkable(c + along * i) &&
                        !dungeon.IsWalkable(c + along * i - across) && !dungeon.IsWalkable(c + along * i + across)))
                    .Where(c => !game.AllCharacters.Any(a => a != ally && a.ToBounds().Contains(c))).ToArray();
                Assert.That(cells, Is.Not.Empty, "Fixture needs a straight one-cell corridor in both directions.");
                var cell = cells[0];
                harness.PlaceAlly(cell);
                game.RefreshSight();
                yield return null;
                var expected = Center(dungeon, cell);
                bool xAxis = across.x != 0;
                float Axis(Vector3 v) => xAxis ? v.x : v.y;
                float Cross(Vector3 v) => xAxis ? v.y : v.x;
                var walls = boundaries.Where(b => Cross(b.min) <= Cross(expected) && Cross(b.max) >= Cross(expected)).ToArray();
                float negative = walls.Where(b => Axis(b.max) < Axis(expected)).Max(b => Axis(b.max));
                float positive = walls.Where(b => Axis(b.min) > Axis(expected)).Min(b => Axis(b.min));
                var marker = ally.CirlcleRenderer;
                // Renderer.bounds encloses the rotated square sprite, including transparent
                // corners. The visible circle keeps the same radius at diagonal facings.
                float radius = marker.sprite.bounds.extents.x * marker.transform.lossyScale.x;
                float ringCenter = Axis(marker.bounds.center);
                float negativeGap = ringCenter - radius - negative, positiveGap = positive - ringCenter - radius;
                var error = ally.VisualParent.transform.position - expected;
                string direction = xAxis ? "vertical" : "horizontal";
                report.AppendLine($"{direction},{cell.x}:{cell.y},{error.x:F4},{error.y:F4},{negative:F4},{positive:F4},{negativeGap:F4},{positiveGap:F4}");
                string stage = ((Vector2)error).magnitude > .001f ? "before" : "after";
                File.WriteAllText(Folder + "/" + stage + "-clearance.csv", report.ToString());
                Capture(direction + "-" + stage, expected, false);
                Capture(direction + "-" + stage + "-top", expected, true);
                Assert.That(Axis(expected) - negative, Is.EqualTo(positive - Axis(expected)).Within(.02f),
                    "Opposing rendered wall envelopes should agree with the corridor's grid center.");
                Assert.That(negativeGap, Is.EqualTo(positiveGap).Within(.02f), "Selection ring must have equal clearance to both walls.");
                foreach (Facing facing in Enum.GetValues(typeof(Facing)))
                {
                    ally.SetFacing(facing);
                    AssertCentered(ally, dungeon);
                }
                var destination = cell + along;
                if (dungeon.GetCharacterAtPosition(destination) == null && dungeon.GetInteractable(destination) == null)
                {
                    var move = new MovementAction(ally, cell, destination);
                    move.ExecuteImmediate(ally);
                    yield return move.ExecuteRoutine(ally);
                    Assert.That(ally.TilemapPosition, Is.EqualTo(destination));
                    AssertCentered(ally, dungeon);
                    var back = new MovementAction(ally, destination, cell);
                    back.ExecuteImmediate(ally);
                    yield return back.ExecuteRoutine(ally, true);
                    AssertCentered(ally, dungeon);
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryEnemyPrefabAndSpawnAnimationEndsAtItsCellCenter()
        {
            var dungeon = harness.Game.CurrentDungeon;
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            int count = 0;
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
                if (prefab == null) continue;
                var cell = DungeonPlacement.OpenCells(dungeon, prefab).First();
                var spawn = new SpawnEnemyAction(prefab, cell, dungeon.CellToWorld(cell));
                spawn.ExecuteImmediate(harness.Ally);
                Assert.That(spawn.SpawnedEnemy, Is.Not.Null, path);
                var enemy = spawn.SpawnedEnemy;
                yield return spawn.ExecuteRoutine(harness.Ally, count != 0);
                yield return null;
                foreach (Facing facing in Enum.GetValues(typeof(Facing)))
                {
                    enemy.SetFacing(facing);
                    AssertCentered(enemy, dungeon);
                }
                harness.Game.Enemies.Remove(enemy);
                Object.Destroy(enemy.gameObject);
                count++;
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(30));
        }

        [UnityTest]
        public IEnumerator FloorTransitionUsesTheNewTileSizeWithoutRescalingCharacters()
        {
            var game = harness.Game;
            var scale = harness.Ally.VisualParent.transform.localScale;
            var generator = Object.FindFirstObjectByType<TileWorldDungeonGenerator>();
            // Each floor reclones its source template. Replace that source with an owned
            // copy for this test, rather than changing the discarded runtime copy or asset.
            var field = typeof(TileWorldDungeonGenerator).GetField("throneTemplate", BindingFlags.Instance | BindingFlags.NonPublic);
            var original = (TWC.TileWorldCreatorAsset)field.GetValue(generator);
            var template = DungeonPresentation.CloneTemplate(original);
            template.cellSize = 2.5f;
            field.SetValue(generator, template);
            try
            {
                Common.Instance.GameSaveData.DungeonSaveData.EndFloor = 2;
                game.AdvanceFloor();
                yield return harness.WaitForIdle();
                yield return null;
                Assert.That(game.CurrentDungeon.IsThroneFloor, Is.True);
                Assert.That(game.CurrentDungeon.CellToWorld(Vector3Int.right).x, Is.EqualTo(2.5f));
                foreach (var character in game.AllCharacters) AssertCentered(character, game.CurrentDungeon);
                Assert.That(harness.Ally.VisualParent.transform.localScale, Is.EqualTo(scale));
            }
            finally
            {
                field.SetValue(generator, original);
                DungeonPresentation.ReleaseTemplate(template);
            }
        }

        static void Capture(string name, Vector3 center, bool topDown)
        {
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var fog = Object.FindFirstObjectByType<FogOverlay>()?.fogOverlayQuad;
            bool fogActive = fog != null && fog.activeSelf;
            var target = RenderTexture.GetTemporary(1280, 900, 24);
            var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            try
            {
                if (topDown)
                {
                    camera.transform.rotation = Quaternion.identity;
                    if (fog != null) fog.SetActive(false);
                }
                camera.transform.position = center - camera.transform.forward * 30;
                camera.orthographicSize = 6;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
                if (fog != null) fog.SetActive(fogActive);
                Object.Destroy(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
