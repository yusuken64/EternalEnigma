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
            harness = new GameTestHarness { TimeoutSeconds = 180 };
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
        public IEnumerator StairFootprintIsCenteredInItsInteractionCellOnConsecutiveFloors()
        {
            for (int floor = 0; floor < 2; floor++)
            {
                var dungeon = harness.Game.CurrentDungeon;
                var stairs = dungeon.Interactables.OfType<Stairs>().Single();
                var cell = dungeon.GetStairPosition();
                var center = dungeon.CellCenterToWorld(cell);
                var bounds = stairs.GetComponentInChildren<MeshRenderer>(true).bounds;
                Assert.That(stairs.Position, Is.EqualTo(cell));
                Assert.That(stairs.transform.position, Is.EqualTo(dungeon.CellToWorld(cell)));
                Assert.That(Vector2.Distance(bounds.center, center), Is.LessThan(.001f));
                Assert.That(bounds.max.z, Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.001f));
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(dungeon.CellSize));
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(dungeon.CellSize));
                foreach (var collider in stairs.GetComponentsInChildren<BoxCollider>(true))
                    Assert.That(Vector2.Distance(collider.bounds.center, center), Is.LessThan(.01f));

                harness.PlaceBesideStairs();
                yield return null;
                Capture("stairs-" + floor + "-top", center, true);
                Capture("stairs-" + floor + "-game", center, false);
                if (floor == 0)
                {
                    harness.Game.AdvanceFloor();
                    yield return harness.WaitForIdle();
                }
            }
        }

        [Test]
        public void TargetSquaresAndPickingAgreeAcrossTheEntireCell()
        {
            var game = harness.Game;
            var dungeon = game.CurrentDungeon;
            var held = game.PlayerController.TargetIndicator;
            var target = MenuManager.Instance.TargetDialog;
            var ally = harness.Ally;
            var center = Center(dungeon, ally.TilemapPosition);
            held.SetTargetingPosition(ally.TilemapPosition);
            Assert.That(Vector2.Distance(held.SquareIndicator.transform.position, center), Is.LessThan(.001f));
            target.Setup(ally, new System.Collections.Generic.List<Character> { ally }, (_, _) => null);
            Assert.That(Vector2.Distance(target.TargetIndicator.GetComponentInChildren<Renderer>(true).bounds.center, center), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(MenuManager.Instance.TargetArrow.transform.position, center), Is.LessThan(.001f));
            float half = dungeon.CellToWorld(Vector3Int.right).x * .49f;
            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    Assert.That(dungeon.GroundPointToCell(center + new Vector3(x * half, y * half)), Is.EqualTo(ally.TilemapPosition));
        }

        [Test]
        public void EnemySpawnCentersAnOffsetMeshAroundTheFootprintWhenTurning()
        {
            var dungeon = harness.Game.CurrentDungeon;
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
            var enemy = Object.Instantiate(prefab, dungeon.transform);
            try
            {
                var cell = DungeonPlacement.OpenCells(dungeon, enemy).First();
                enemy.SetPosition(cell);
                enemy.SetFacing(Facing.Up);
                var model = enemy.Animator.transform;
                while (model.parent != enemy.VisualParent.transform) model = model.parent;
                model.position += new Vector3(2, -1, 0);
                var expected = Center(dungeon, cell);
                var before = CombatEffectPlayer.ModelBounds(model).Value;
                Assert.That(Vector2.Distance(before.center, expected), Is.GreaterThan(.5f));
                enemy.InitializeSpawn();
                foreach (var facing in new[] { Facing.Up, Facing.Right, Facing.Down, Facing.Left })
                {
                    enemy.SetFacing(facing);
                    var center = CombatEffectPlayer.ModelBounds(model).Value.center;
                    Assert.That(Vector2.Distance(center, expected), Is.LessThan(.01f), facing.ToString());
                    Assert.That(center.z, Is.EqualTo(before.center.z).Within(.01f));
                }
            }
            finally { Object.DestroyImmediate(enemy.gameObject); }
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator EveryThreeByThreeMonsterCentersItsVisibleMeshOnItsNineCells()
        {
            var dungeon = harness.Game.CurrentDungeon;
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies", "Assets/Resources/Bosses", "Assets/Resources/Finale" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);
            using var footprintCamera = new MonsterSilhouetteCamera();
            var rows = new StringBuilder("prefab,facing,x_error_in_tiles,y_error_in_tiles,clip,model_x,model_y\n");
            var errors = new System.Collections.Generic.List<string>();
            int tested = 0;
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
                bool normalSlime = path == "Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab";
                if (prefab == null || (!normalSlime && prefab.FootPrint != FootPrint.Size3x3)) continue;
                var enemy = Object.Instantiate(prefab, dungeon.transform);
                try
                {
                    var cell = DungeonPlacement.OpenCells(dungeon, enemy)
                        .OrderBy(c => (c - harness.Ally.TilemapPosition).sqrMagnitude)
                        .First(c => DungeonPlacement.CanStep(dungeon, enemy, c, c + Vector3Int.right));
                    enemy.UpdateCachedStats();
                    enemy.InitialzeVitalsFromStats();
                    enemy.TilemapPosition = cell;
                    enemy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var skin in enemy.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    enemy.InitializeSpawn();
                    harness.Game.Enemies.Add(enemy);
                    float speed = enemy.Animator.speed;
                    enemy.PlayIdleAnimation();
                    enemy.Animator.speed = 0;
                    yield return null;
                    AssertCentered(enemy, dungeon);
                    var spawned = (footprintCamera.Center(enemy) - (Vector2)dungeon.CellCenterToWorld(cell)) / dungeon.CellSize;
                    string IdleClip() => enemy.Animator.GetCurrentAnimatorClipInfo(0).FirstOrDefault().clip?.name ?? "";
                    void Record(string state, Vector2 offset) => rows.AppendLine($"{prefab.name},{state},{offset.x:F4},{offset.y:F4},{IdleClip()},{enemy.ModelRoot.localPosition.x:F4},{enemy.ModelRoot.localPosition.y:F4}");
                    Record("Spawn", spawned);
                    if (!normalSlime && spawned.magnitude > .02f) errors.Add($"{prefab.name} spawned {spawned.magnitude:F3} tiles off center");
                    enemy.Animator.speed = speed;
                    var move = new MovementAction(enemy, cell, cell + Vector3Int.right);
                    move.ExecuteImmediate(enemy);
                    yield return move.ExecuteRoutine(enemy);
                    Assert.That(enemy.TilemapPosition, Is.EqualTo(cell + Vector3Int.right), path);
                    enemy.PlayIdleAnimation();
                    enemy.Animator.speed = 0;
                    yield return null;
                    var tiles = enemy.ToBounds().Cells().ToArray();
                    int width = normalSlime ? 1 : 3;
                    Assert.That(tiles.Length, Is.EqualTo(width * width), path);
                    Assert.That(enemy.ModelRoot.localScale, Is.EqualTo(prefab.ModelRoot.localScale), path);
                    Assert.That(enemy.VisualParent.transform.localScale, Is.EqualTo(prefab.VisualParent.transform.localScale), path);
                    Vector3 center = tiles.Select(dungeon.CellCenterToWorld).Aggregate(Vector3.zero, (a, b) => a + b) / tiles.Length;
                    foreach (Facing facing in Enum.GetValues(typeof(Facing)))
                    {
                        enemy.SetFacing(facing);
                        AssertCentered(enemy, dungeon);
                        var offset = (footprintCamera.Center(enemy) - (Vector2)center) / dungeon.CellSize;
                        Record(facing.ToString(), offset);
                        if (!normalSlime && offset.magnitude > .02f)
                            errors.Add($"{prefab.name} facing {facing} is {offset.magnitude:F3} tiles off center");
                        if (facing == Facing.Down)
                        {
                            string snapshot = prefab.name switch
                            {
                                "Enemy_Slime" => "slime-normal", "Enemy_Slime_Big" => "slime-big",
                                "Slime" => "slime-boss", "CrabMonster" => "crab-footprint", _ => null
                            };
                            if (snapshot != null) Capture(snapshot, center, true, dungeon.CellSize, width);
                        }
                    }
                    if (!normalSlime) tested++;
                }
                finally
                {
                    harness.Game.Enemies.Remove(enemy);
                    Object.DestroyImmediate(enemy.gameObject);
                }
            }
            File.WriteAllText(Folder + "/large-monster-footprints.csv", rows.ToString());
            Assert.That(tested, Is.GreaterThanOrEqualTo(33), "Large variants and the original Big Slime must all be covered.");
            Assert.That(errors, Is.Empty, string.Join("\n", errors.Take(12)));
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

        static void Capture(string name, Vector3 center, bool topDown, float footprintCellSize = 0, int footprintWidth = 3)
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
                if (footprintCellSize > 0)
                {
                    // Mark the actual occupied tile edges in the top-down comparison.
                    float step = image.height * footprintCellSize / (2 * camera.orthographicSize);
                    int X(float cell) => Mathf.RoundToInt(image.width * .5f + cell * step);
                    int Y(float cell) => Mathf.RoundToInt(image.height * .5f + cell * step);
                    var ink = new Color(.48f, 1f, .84f);
                    void Mark(int x, int y, float alpha) => image.SetPixel(x, y, Color.Lerp(image.GetPixel(x, y), ink, alpha));
                    float half = footprintWidth * .5f;
                    for (int i = 0; i <= footprintWidth; i++)
                    {
                        float alpha = i == 0 || i == footprintWidth ? .95f : .42f;
                        for (int x = X(-half); x <= X(half); x++) Mark(x, Y(i - half), alpha);
                        for (int y = Y(-half); y <= Y(half); y++) Mark(X(i - half), y, alpha);
                    }
                }
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
