#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class DungeonFloorTransitionTests
    {
        private GameTestHarness harness;
        private DungeonAnimationMode? previousAnimation;
        private float previousTimeScale;
        private UnityEditor.EditorWindow gameView;
        private int previousViewSize;
        private const System.Reflection.BindingFlags ViewFlags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Setup()
        {
            previousAnimation = DungeonPreferences.AnimationOverride;
            previousTimeScale = Time.timeScale;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            Time.timeScale = 1;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            previousViewSize = (int)gameView.GetType().GetProperty("selectedSizeIndex", ViewFlags).GetValue(gameView);
            Directory.CreateDirectory("Temp/FloorTransitions");
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = previousTimeScale;
            DungeonPreferences.AnimationOverride = previousAnimation;
            gameView.GetType().GetProperty("selectedSizeIndex", ViewFlags).SetValue(gameView, previousViewSize);
            yield return harness.Cleanup();
        }

        [UnityTest] public IEnumerator AdvanceWaitsForAnOpaqueFrameBeforeReplacingTheFloor()
        {
            yield return harness.LoadDungeon(new TestScenario());
            yield return harness.WaitUntil(() => !Common.Instance.ScreenTransition.BlocksInput, "initial dungeon reveal");
            foreach (var mode in new[] { DungeonAnimationMode.Normal, DungeonAnimationMode.NoAnimations })
            {
                yield return ConfigureView(mode);
                var game = harness.Game;
                int floor = game.PlayerController.Floor;
                Assert.That(game.CanExitFloor, Is.True);
                yield return ObserveTransition("advance-" + mode, () => {
                    game.AdvanceFloor();
                    game.AdvanceFloor(); // Repeated activation must not skip a floor or start a second generator.
                });
                Assert.That(game.PlayerController.Floor, Is.EqualTo(floor + 1));
            }
        }

        [UnityTest] public IEnumerator FloorTwoIsForgivingAndFloorThreeKeepsNormalStats()
        {
            yield return harness.LoadDungeon(new TestScenario { Seed = 42 });
            var game = harness.Game;
            game.AdvanceFloor();
            yield return harness.WaitForIdle();
            Assert.That(game.PlayerController.Floor, Is.EqualTo(2));
            Assert.That(game.CurrentDungeon.Floor.Enemies.Count, Is.EqualTo(6));
            Assert.That(game.Enemies.Count, Is.InRange(1, 6), "Large enemy footprints may exclude a seeded spawn.");
            Assert.That(game.CurrentDungeon.Floor.Traps.Count, Is.EqualTo(1));
            Assert.That(game.CurrentDungeon.Floor.Gold.Count, Is.EqualTo(5));
            Assert.That(game.CurrentDungeon.Floor.Items.Count, Is.EqualTo(5));
            foreach (var enemy in game.Enemies.Cast<Enemy>()) CheckStats(enemy, true);

            // Splitting and other later spawns receive the same reduction, without altering the prefab.
            var mage = game.EnemyManager.EnemyPrefabs.Single(e => e.name == "Enemy_EvilMage");
            int authoredHP = mage.StartingStats.HPMax;
            var cell = game.CurrentDungeon.GetPositionWith(game.CurrentDungeon.Floor.Start.ToCell(), node =>
                !game.AllCharacters.Any(c => c != null && c.TilemapPosition == new Vector3Int(node.X, node.Y)));
            var spawn = new SpawnEnemyAction(mage, cell, game.CurrentDungeon.CellToWorld(cell));
            spawn.ExecuteImmediate(harness.Ally);
            Assert.That(spawn.SpawnedEnemy, Is.Not.Null);
            CheckStats(spawn.SpawnedEnemy, true);
            spawn.SpawnedEnemy.InitializeSpawn();
            CheckStats(spawn.SpawnedEnemy, true);
            Assert.That(mage.StartingStats.HPMax, Is.EqualTo(authoredHP));

            game.AdvanceFloor();
            yield return harness.WaitForIdle();
            Assert.That(game.PlayerController.Floor, Is.EqualTo(3));
            Assert.That(game.CurrentDungeon.Floor.Enemies.Count, Is.EqualTo(10));
            Assert.That(game.CurrentDungeon.Floor.Traps.Count, Is.EqualTo(5));
            foreach (var enemy in game.Enemies.Cast<Enemy>()) CheckStats(enemy, false);

            static void CheckStats(Enemy enemy, bool reduced)
            {
                var authored = enemy.StartingStats;
                Assert.That(enemy.FinalStats.HPMax, Is.EqualTo(reduced ? Mathf.Max(1, Mathf.RoundToInt(authored.HPMax * .6f)) : authored.HPMax), enemy.name);
                Assert.That(enemy.Vitals.HP, Is.EqualTo(enemy.FinalStats.HPMax), enemy.name);
                Assert.That(enemy.FinalStats.Strength, Is.EqualTo(reduced ? Mathf.Max(1, authored.Strength / 2) : authored.Strength), enemy.name);
                Assert.That(enemy.FinalStats.Defense, Is.EqualTo(reduced ? authored.Defense / 2 : authored.Defense), enemy.name);
                Assert.That(enemy.FinalStats.EXPOnKill, Is.EqualTo(authored.EXPOnKill), enemy.name);
            }
        }

        [UnityTest] public IEnumerator ResetWaitsForAnOpaqueFrameAndRestoresTheSamePuzzle()
        {
            yield return harness.LoadMainMenu(null);
            Object.FindFirstObjectByType<MainMenu>().StartGame();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.ScreenTransition.BlocksInput, "initial town reveal");
            var common = Common.Instance;
            var context = common.CampaignContext;
            context.Completed.Add("repeatable-0");
            context.Position = context.Grid.Locations[context.Campaign.Locations.First(l =>
                l.Encounter?.Kind == EncounterKind.LightPattern).Id];
            context.State.Scene = "Overworld";
            Assert.That(common.Travel.EnterLocation(), Is.True);
            yield return harness.WaitForIdle();
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlocksInput, "puzzle reveal");
            string fingerprint = DungeonFloorFingerprint.Compute(harness.Game.CurrentDungeon.Floor);
            int floor = harness.Game.PlayerController.Floor;
            int gold = harness.Game.PlayerController.Gold;
            var inventory = harness.Game.PlayerController.Inventory.InventoryItems.ToArray();
            foreach (var mode in new[] { DungeonAnimationMode.Normal, DungeonAnimationMode.NoAnimations })
            {
                yield return ConfigureView(mode);
                harness.Game.PlayerController.Gold += 517;
                harness.Game.PlayerController.Inventory.Clear();
                yield return ObserveTransition("reset-" + mode, () => {
                    Assert.That(harness.Game.ResetFloor(), Is.True);
                    Assert.That(harness.Game.ResetFloor(), Is.False, "A pending reset cannot be entered twice.");
                });
                Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(floor));
                Assert.That(DungeonFloorFingerprint.Compute(harness.Game.CurrentDungeon.Floor), Is.EqualTo(fingerprint));
                Assert.That(harness.Game.PlayerController.Gold, Is.EqualTo(gold));
                CollectionAssert.AreEqual(inventory, harness.Game.PlayerController.Inventory.InventoryItems);
            }
        }

        private IEnumerator ConfigureView(DungeonAnimationMode mode)
        {
            DungeonPreferences.AnimationOverride = mode;
            // Covering, generation and revealing must all finish while the game is paused.
            Time.timeScale = 0;
            var size = mode == DungeonAnimationMode.Normal ? new Vector2(960, 600) : new Vector2(2560, 1080);
            gameView.GetType().GetMethod("SetCustomResolution", ViewFlags)
                .Invoke(gameView, new object[] { size, $"Floor transition {size.x}x{size.y}" });
            gameView.Repaint();
            yield return null;
            yield return null;
            Assert.That(Screen.width, Is.EqualTo((int)size.x));
            Assert.That(Screen.height, Is.EqualTo((int)size.y));
        }

        private IEnumerator ObserveTransition(string name, Action begin)
        {
            var game = harness.Game;
            var oldDungeon = game.CurrentDungeon;
            var oldEncounter = game.Encounter;
            var oldEnemies = game.Enemies.ToArray();
            var oldPositions = game.Allies.ToDictionary(a => a, a => a.TilemapPosition);
            int oldFloor = game.PlayerController.Floor;
            var message = game.NewFloorMessage;
            var trace = new List<string> { "frame,unscaledTime,alpha,overlayActive,inputBlocked,ready,oldFloorAlive,worldMutated" };
            int coveredFrame = -1;
            bool mutationSeen = false;
            float deadline = Time.realtimeSinceStartup + harness.TimeoutSeconds;
            try
            {
                begin();
                Assert.That(game.IsReady, Is.False);
                Assert.That(game.CurrentDungeon, Is.SameAs(oldDungeon), "Starting a transition must leave the old world intact.");
                Assert.That(game.Encounter, Is.SameAs(oldEncounter));
                Assert.That(oldEnemies.All(e => e != null), Is.True, "Enemies cannot disappear before the cover renders.");
                do
                {
                    bool active = message.gameObject.activeInHierarchy;
                    float alpha = message.BackgroundColor.color.a * message.BackgroundColor.canvasRenderer.GetAlpha();
                    bool opaque = active && alpha >= .999f;
                    bool changed = oldDungeon == null || game.CurrentDungeon != oldDungeon ||
                        !ReferenceEquals(game.Encounter, oldEncounter) || game.PlayerController.Floor != oldFloor ||
                        oldEnemies.Any(e => e == null) || oldPositions.Any(p => p.Key == null || p.Key.TilemapPosition != p.Value);
                    trace.Add(FormattableString.Invariant($"{Time.frameCount},{Time.unscaledTime:F3},{alpha:F4},{active},{Common.Instance.ScreenTransition.BlocksInput},{game.IsReady},{oldDungeon != null},{changed}"));
                    if (opaque && !changed && coveredFrame < 0)
                    {
                        coveredFrame = Time.frameCount;
                        AssertCoverFillsViewport(message);
                        ScreenCapture.CaptureScreenshot($"Temp/FloorTransitions/{name}-covered.png");
                    }
                    if (changed && !mutationSeen)
                    {
                        mutationSeen = true;
                        Assert.That(coveredFrame, Is.GreaterThanOrEqualTo(0), "A fully opaque frame must precede any world mutation.");
                        Assert.That(Time.frameCount, Is.GreaterThan(coveredFrame), "Generation cannot start in the first covered frame.");
                        Assert.That(opaque, Is.True, "The first removed actor or terrain must remain covered.");
                    }
                    if (changed && !game.IsReady)
                        Assert.That(opaque, Is.True, "Generation and party restoration must stay completely covered.");
                    if (active)
                    {
                        Assert.That(Common.Instance.ScreenTransition.BlocksInput, Is.True);
                        Assert.That(game.PlayerController.CanOpenMenu(), Is.False, "Hidden menus must not open behind a floor fade.");
                    }
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "A paused game must not stall the transition.");
                    yield return null;
                } while (!game.IsReady || message.gameObject.activeInHierarchy);
                Assert.That(mutationSeen, Is.True);
                Assert.That(oldDungeon == null, Is.True);
                Assert.That(Common.Instance.ScreenTransition.BlocksInput, Is.False);
                Assert.That(game.PlayerController.CanOpenMenu(), Is.True);
            }
            finally { File.WriteAllLines($"Temp/FloorTransitions/{name}.csv", trace); }
        }

        private static void AssertCoverFillsViewport(NewFloorMessage message)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            message.BackgroundColor.rectTransform.GetWorldCorners(corners);
            Assert.That(corners.Min(p => p.x), Is.LessThanOrEqualTo(.5f));
            Assert.That(corners.Min(p => p.y), Is.LessThanOrEqualTo(.5f));
            Assert.That(corners.Max(p => p.x), Is.GreaterThanOrEqualTo(Screen.width - .5f));
            Assert.That(corners.Max(p => p.y), Is.GreaterThanOrEqualTo(Screen.height - .5f));
            Assert.That(message.BackgroundColor.color, Is.EqualTo(Color.black));
            Assert.That(message.BackgroundColor.raycastTarget, Is.True);
            Assert.That(message.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(2100), "The cover must also hide Settings and confirmation dialogs.");
            message.FloorMessage.ForceMeshUpdate();
            Assert.That(message.FloorMessage.isTextOverflowing, Is.False);
        }
    }
}
#endif
