#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EternalEnigma.Core.World;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class AutoplayLootSweepTests
    {
        GameTestHarness harness;
        TestInputScope input;
        [UnitySetUp] public IEnumerator Setup() { input = new TestInputScope(); harness = new GameTestHarness { TimeoutSeconds = 120 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (AutoplayRunner.Active != null)
            {
                AutoplayRunner.Active.ExitDemo();
                yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "restore isolated sweep");
            }
            yield return harness.Cleanup();
            input?.Dispose();
        }
        static AutoplayOptions Options(int count = 3) { var o = AutoplayOptions.LootSweep(); o.RunCount = count; return o; }

        [UnityTest]
        public IEnumerator FourteenGeneratedFloorsFinalizeAndResumeWithoutChangingPlayerSave()
        {
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            var original = Common.Instance.GameSaveData; string json = harness.Store.Json;
            float fixedStep = Time.fixedDeltaTime;
            AutoplayRunner.WatchDemo(Options(14)); var runner = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => !runner.SessionRunning, "legacy and biome floor sweep");
            Assert.That(runner.Batch.State, Is.EqualTo("Completed"), runner.Status);
            Assert.That(runner.Batch.Runs.Count, Is.EqualTo(14)); Assert.That(runner.Batch.AllFloorsPassed, Is.True, runner.Status);
            Assert.That(runner.Report.CollectedLoot, Is.GreaterThan(0));
            foreach (var row in runner.Batch.Runs)
            {
                Assert.That(row.ArtifactsComplete, Is.True); Assert.That(row.ExpectedLoot, Is.EqualTo(row.CollectedLoot));
                Assert.That(row.RecoveryEvents, Is.Zero);
                var report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(Path.Combine(runner.BatchDirectoryPath, row.Directory, "report.json")));
                var floor = runner.Manifest.Cases[row.RunNumber - 1];
                Assert.That(report.GeneratedSeed, Is.EqualTo(floor.Seed));
                Assert.That(report.GeneratedWidth, Is.EqualTo(floor.Width)); Assert.That(report.GeneratedHeight, Is.EqualTo(floor.Height));
                Assert.That(report.GeneratedThrone, Is.EqualTo(floor.Throne));
            }
            string manifest = Path.Combine(runner.BatchDirectoryPath, "manifest.json");
            runner.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "sweep exit");
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(original)); Assert.That(harness.Store.Json, Is.EqualTo(json));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            var resumed = Options(14); resumed.ResumeManifestPath = manifest;
            AutoplayRunner.WatchDemo(resumed); runner = AutoplayRunner.Active;
            Assert.That(runner.SessionRunning, Is.False); Assert.That(runner.Batch.Runs.Count, Is.EqualTo(14));
            Assert.That(runner.Batch.AllFloorsPassed, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseDuringGenerationStopAndInterruptedResumePreserveAttempts()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(Options(2)); var runner = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => runner.Report.Actions == 1, "generation begins");
            runner.SetPaused(true); runner.Stop();
            yield return harness.WaitUntil(() => !runner.Finalizing, "stopped generation artifacts");
            Assert.That(runner.RunNumber, Is.EqualTo(1)); Assert.That(runner.Report.Outcome, Is.EqualTo("Stopped"));
            string first = runner.DirectoryPath, manifest = Path.Combine(runner.BatchDirectoryPath, "manifest.json");
            runner.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "stop cleanup");
            var options = Options(2); options.ResumeManifestPath = manifest;
            AutoplayRunner.WatchDemo(options); runner = AutoplayRunner.Active; runner.SetPaused(true);
            Assert.That(runner.RunNumber, Is.EqualTo(1)); Assert.That(runner.DirectoryPath, Is.Not.EqualTo(first));
            Assert.That(File.Exists(Path.Combine(first, "report.json")), Is.True);
            yield return null; Assert.That(runner.Report.Actions, Is.Zero);
            runner.Stop(); yield return harness.WaitUntil(() => !runner.Finalizing, "paused stop");
        }

        [UnityTest]
        public IEnumerator ReceiptsRequireResourceDeltaAndCountCompanionStackedPickupsOnce()
        {
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese" } });
            AutoplayRunner.WatchDemo(Options(1)); var runner = AutoplayRunner.Active; runner.SetPaused(true);
            var dungeon = harness.Game.CurrentDungeon; var ally = harness.Ally;
            var definition = Common.Instance.ItemManager.ItemDefinitions.First(i => i != null && i.AsInventoryItem(7).StackStock.HasValue);
            var item = dungeon.SetDroppedItem(ally.TilemapPosition, definition, 7);
            harness.Game.PlayerController.Inventory.MaxItems = 0;
            // Submit through the real turn pipeline while suspending policy ticks only.
            Time.timeScale = 32;
            yield return harness.ExecuteAction(new InteractAction(item));
            Assert.That(runner.Report.CollectedLoot, Is.EqualTo(1));
            Assert.That(runner.Report.Loot.Single().After - runner.Report.Loot.Single().Before, Is.EqualTo(item.InventoryItem.StackStock ?? 1));
            Assert.That(harness.Game.PlayerController.Inventory.MaxItems, Is.GreaterThan(0));
            var companion = harness.Game.Allies.Last();
            dungeon.SetGoldAmount(companion.TilemapPosition, 19); var gold = dungeon.Interactables.OfType<Gold>().Last();
            companion.SetAction(new InteractAction(gold));
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(runner.Report.Loot.Count(r => r.Collected && r.Kind == "Gold"), Is.EqualTo(1));
            var missing = dungeon.SetDroppedItem(ally.TilemapPosition, definition);
            dungeon.RemoveInteractable(missing); runner.LootLedger.Audit();
            Assert.That(runner.LootLedger.Failed, Is.True); Assert.That(runner.Report.UnresolvedLoot.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InvalidLogicalAndVisualPlacementsFailBeforeRecovery()
        {
            yield return harness.LoadDungeon(new TestScenario());
            AutoplayRunner.WatchDemo(Options(1)); var runner = AutoplayRunner.Active; runner.SetPaused(true);
            var dungeon = harness.Game.CurrentDungeon;
            var definition = Common.Instance.ItemManager.GetRandomDrop(123, 2);
            var wall = dungeon.SetDroppedItem(new Vector3Int(-5, -5), definition);
            var offset = dungeon.SetDroppedItem(harness.Ally.TilemapPosition, definition);
            offset.transform.position += new Vector3(20, 20, 0);
            runner.LootLedger.InspectPlacement(dungeon);
            Assert.That(runner.Report.PlacementIssues.Any(s => s.Contains("blockers")), Is.True);
            Assert.That(runner.Report.PlacementIssues.Any(s => s.Contains("align")), Is.True);
            Assert.That(wall.Position, Is.EqualTo(new Vector3Int(-5, -5)), "Inspection cannot silently relocate seeded loot.");
            DungeonPlacement.Recover(dungeon);
            Assert.That(runner.Report.RecoveryEvents, Is.GreaterThan(0)); Assert.That(runner.LootLedger.Failed, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContainerKeysLockedDoorAndDestructibleRewardsUseProductionTurns()
        {
            yield return harness.LoadDungeon(new TestScenario());
            AutoplayRunner.WatchDemo(Options(1)); var runner = AutoplayRunner.Active; runner.SetPaused(true);
            var dungeon = harness.Game.CurrentDungeon; var origin = harness.Ally.TilemapPosition;
            var cell = origin + Vector3Int.right;
            Assert.That(dungeon.IsWalkable(cell), Is.True);
            var theme = Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog").Get(new DungeonVisualSelection());
            var container = DungeonProp.Create(dungeon, new DungeonScenery(cell.ToGridPoint(), DungeonSceneryKind.Container, 0, 42, SceneryReward.Gold, true), theme);
            runner.LootLedger.Observe(container);
            Assert.That(runner.Report.ExpectedLoot, Is.EqualTo(2));
            Time.timeScale = 32;
            for (int i = 0; i < 30 && runner.Report.CollectedLoot < 2; i++)
            {
                runner.SweepDungeonTick(harness.Game); yield return null; yield return harness.WaitForIdle();
            }
            Assert.That(runner.Report.CollectedLoot, Is.EqualTo(2), runner.Status);
            Assert.That(SmallKeys.Count, Is.EqualTo(1));
            var doorCell = harness.Ally.TilemapPosition + Vector3Int.right;
            var door = DungeonProp.Create(dungeon, new DungeonScenery(doorCell.ToGridPoint(), DungeonSceneryKind.Door, 10, 0, SceneryReward.None), theme);
            yield return harness.ExecuteAction(new InteractAction(door));
            Assert.That(SmallKeys.Count, Is.Zero); Assert.That(dungeon.IsWalkable(doorCell), Is.True);
            var prop = DungeonProp.Create(dungeon, new DungeonScenery(doorCell.ToGridPoint(), DungeonSceneryKind.Destructible, 1, 45, SceneryReward.Item), theme);
            runner.LootLedger.Observe(prop);
            for (int i = 0; i < 30 && runner.Report.CollectedLoot < 3; i++)
            { runner.SweepDungeonTick(harness.Game); yield return null; yield return harness.WaitForIdle(); }
            runner.LootLedger.Audit();
            Assert.That(runner.Report.ExpectedLoot, Is.EqualTo(3)); Assert.That(runner.Report.CollectedLoot, Is.EqualTo(3));
            Assert.That(runner.Report.UnresolvedLoot, Is.Empty); Assert.That(runner.LootLedger.Failed, Is.False);
        }

        [UnityTest]
        public IEnumerator LockedVaultIsBashedThroughTurnsAndDisconnectedLootFails()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var dungeon = harness.Game.CurrentDungeon;
            var mask = new bool[dungeon.dungeonWidth, dungeon.dungeonHeight];
            // A one-cell corridor is the only entrance to this tiny vault.
            for (int x = 2; x <= 8; x++) mask[x, 4] = true;
            mask[10, 4] = true; // A separate, valid floor cell with no connecting route.
            typeof(TileWorldDungeon).GetField("floorMask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(dungeon, mask);
            dungeon.DoorsChanged();
            harness.PlaceAlly(new Vector3Int(2, 4));
            AutoplayRunner.WatchDemo(Options(1)); var runner = AutoplayRunner.Active; runner.SetPaused(true);
            var door = DungeonProp.Create(dungeon, new DungeonScenery(new GridPoint(5, 4), DungeonSceneryKind.Door, 1, 0, SceneryReward.None), null);
            dungeon.SetGoldAmount(new Vector3Int(8, 4), 21);
            Assert.That(SmallKeys.Count, Is.Zero);
            Time.timeScale = 32;
            for (int i = 0; i < 20 && runner.Report.CollectedLoot == 0; i++)
            { runner.SweepDungeonTick(harness.Game); yield return null; yield return harness.WaitForIdle(); }
            Assert.That(door == null || door.Opened, Is.True, runner.Status);
            Assert.That(runner.Report.CollectedLoot, Is.EqualTo(1));
            Assert.That(runner.Report.RecoveryEvents, Is.Zero);
            dungeon.SetGoldAmount(new Vector3Int(10, 4), 9);
            runner.SweepDungeonTick(harness.Game);
            Assert.That(runner.Report.Outcome, Is.EqualTo("Failed"));
            Assert.That(runner.Report.Reason, Does.Contain("No legal route"));
            Assert.That(runner.Report.UnresolvedLoot.Count, Is.EqualTo(1));
            yield return harness.WaitUntil(() => !runner.Finalizing, "unreachable pickup artifacts");
        }

        [UnityTest]
        public IEnumerator ReportingFailureStopsSweepAndCannotBeResumedAsFinalized()
        {
            yield return harness.LoadMainMenu(null);
            AutoplayRunner.WatchDemo(Options(2)); var runner = AutoplayRunner.Active; runner.SetPaused(true);
            var writer = (StreamWriter)typeof(AutoplayRunner).GetField("actions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(runner);
            writer.Dispose(); runner.Finish("Failed", "Synthetic reporting failure");
            yield return null;
            Assert.That(runner.SessionRunning, Is.False); Assert.That(runner.Batch.State, Is.EqualTo("Error"));
            Assert.That(runner.Report.ArtifactsComplete, Is.False); Assert.That(runner.RunNumber, Is.EqualTo(1));
            Assert.That(runner.Manifest.FinalizedCases(runner.BatchDirectoryPath, runner.Report.BuildId, runner.Report.ContentId), Is.Empty);
        }
    }
}
#endif
