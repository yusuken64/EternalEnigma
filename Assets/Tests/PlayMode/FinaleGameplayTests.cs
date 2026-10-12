#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    public sealed class FinaleGameplayTests
    {
        GameTestHarness harness;
        DungeonAnimationMode? oldAnimations;
        [UnitySetUp] public IEnumerator Setup()
        { harness = new GameTestHarness { TimeoutSeconds = 240 }; oldAnimations = DungeonPreferences.AnimationOverride; DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); DungeonPreferences.AnimationOverride = oldAnimations; }
        IEnumerator LoadFinale()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Dungeon);
            common.GameSaveData.IsSandbox = false;
            SaveSystem.SaveData(common.GameSaveData);
            common.Travel.Continue();
            int coveredPlanningFrames = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + harness.TimeoutSeconds;
            while (harness.Game?.IsReady != true)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Finale initialization timed out.");
                AssertCovered(common.ScreenTransition);
                var title = common.ScreenTransition.ShutterScreen.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (harness.Game != null && harness.Game.Finale == null && title.text.Contains("Floor 1/5"))
                {
                    coveredPlanningFrames++;
                    if (coveredPlanningFrames == 1)
                    {
                        System.IO.Directory.CreateDirectory("Temp/FinaleDebug");
                        ScreenCapture.CaptureScreenshot("Temp/FinaleDebug/loading-floor-1.png");
                    }
                }
                yield return null;
            }
            Assert.That(coveredPlanningFrames, Is.GreaterThan(0),
                "The labeled loading screen must get a frame before synchronous five-map planning begins.");
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf, "finale reveal");
            yield return harness.WaitForIdle();
        }
        static void AssertCovered(ScreenTransition transition, int floor = 0)
        {
            Assert.That(transition.BlockScreen.activeInHierarchy, Is.True, "Loading must block game input.");
            Assert.That(transition.ShutterScreen.gameObject.activeInHierarchy, Is.True);
            Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1).Within(.001f));
            var title = transition.ShutterScreen.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(title, Is.Not.Null);
            Assert.That(title.gameObject.activeInHierarchy && title.alpha > .99f, Is.True);
            Assert.That(title.text, Does.StartWith("Loading..."));
            if (floor > 0) Assert.That(title.text, Does.Contain($"Floor {floor}/5"));
        }
        static void Resolve(Character actor, GameAction action)
        {
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                foreach (var child in next.ExecuteImmediate(actor) ?? new()) queue.Enqueue(child);
                next.UpdateDisplayedStats();
            }
        }
        void Reveal(Enemy enemy)
        {
            var game = harness.Game;
            var cell = game.CurrentDungeon.GetWalkableNeighborhoodTiles(enemy.TilemapPosition)
                .First(p => game.Finale.Session.CanReveal(enemy.GetComponent<FinaleActor>().Id, p.ToGridPoint()) &&
                    game.CurrentDungeon.OverlapsAnyOtherCharacter(harness.Ally, Character.ToBounds(p)) == null);
            harness.Ally.SetPosition(cell);
            Resolve(harness.Ally, new RevealMimicAction(enemy));
        }
        void Kill(Enemy enemy)
        {
            Resolve(harness.Ally, new TakeDamageAction(harness.Ally, enemy, int.MaxValue / 8) { Environmental = true, AwardExperience = false });
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator MimicsIgnoreDamageUntilAdjacentEntryAndSkipTheirRevealTurn()
        {
            yield return LoadFinale();
            var game = harness.Game; var session = game.Finale.Session;
            Assert.That(session.CurrentFloor, Is.EqualTo(1)); Assert.That(session.Unlocked.Count, Is.EqualTo(1));
            Assert.That(session.Defeated, Is.Empty); Assert.That(session.Revealed, Is.Empty);
            Assert.That(game.CurrentDungeon.Floor.Overworld, Is.SameAs(Common.Instance.CampaignContext.Grid));
            var mimic = game.Enemies.OfType<Enemy>().First(e => e.GetComponent<FinaleActor>().Hidden);
            Assert.That(mimic.FootPrint, Is.EqualTo(FootPrint.Size1x1));
            int hp = mimic.Vitals.HP;
            Kill(mimic); Assert.That(mimic.Vitals.HP, Is.EqualTo(hp)); Assert.That(session.Defeated, Is.Empty);
            Assert.That(game.Finale.CanAct(mimic), Is.False);
            Reveal(mimic);
            Assert.That(mimic.GetComponent<FinaleActor>().Hidden, Is.False);
            Assert.That(game.Finale.CanAct(mimic), Is.False);
            game.Finale.EndTurn(); Assert.That(game.Finale.CanAct(mimic), Is.True);
            Assert.That(session.Revealed, Does.Contain(mimic.GetComponent<FinaleActor>().Id));
            var closed = session.Floor.Regions[1].Enemies[0].Position.ToCell();
            Assert.That(SkillMovement.CanOccupy(harness.Ally, closed), Is.False);
            var before = harness.Ally.TilemapPosition; SkillMovement.Place(harness.Ally, closed);
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(before));
            Assert.That(game.Finale.RequestExit(), Is.False);
            Assert.That(Common.Instance.Travel.FinishDungeon(true, game.PlayerController), Is.False);
            for (int roll = 0; roll < 10000; roll++)
                Assert.That(FinaleRoster.IsReserved(game.EnemyManager.GetEnemyPrefab(38, roll).name), Is.False);
            var source = game.Enemies.OfType<Enemy>().First(e => !session.Floor.Enemies.Any(p => p.IsMimic && p.Id == e.GetComponent<FinaleActor>().Id));
            var sourceIdentity = source.GetComponent<FinaleActor>();
            Assert.That(EncounterActor.Required(source), Is.True, "Required enemies cannot be converted.");
            var spawnCell = game.CurrentDungeon.GetWalkableNeighborhoodTiles(source.TilemapPosition)
                .First(p => FinaleActor.AllowsPosition(source, p) && SkillMovement.CanOccupy(source, p));
            var spawn = new SpawnEnemyAction(source, spawnCell, source.VisualParent.transform.position, source);
            Resolve(harness.Ally, spawn); yield return spawn.ExecuteRoutine(harness.Ally, true);
            var child = spawn.SpawnedEnemy;
            Assert.That(child, Is.Not.Null);
            var childIdentity = child.GetComponent<FinaleActor>();
            Assert.That(childIdentity.Id, Is.EqualTo(sourceIdentity.Id + "/summon-1"));
            Assert.That(childIdentity.RegionId, Is.EqualTo(sourceIdentity.RegionId));
            Assert.That(session.EnemyRegions[childIdentity.Id], Is.EqualTo(session.EnemyRegions[sourceIdentity.Id]));
            foreach (var enemy in game.Enemies.OfType<Enemy>().Where(e => e != child).ToArray())
            {
                if (enemy.GetComponent<FinaleActor>().Hidden) Reveal(enemy);
                Kill(enemy);
            }
            game.Finale.EndTurn();
            Assert.That(session.Unlocked.Count, Is.EqualTo(1), "A living summon still owns the region.");
            Kill(child); game.Finale.EndTurn(); Assert.That(session.Unlocked.Count, Is.EqualTo(2));
            game.Finale.EndTurn(); Assert.That(session.Unlocked.Count, Is.EqualTo(2), "The boundary opens once.");
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator AllFiveStairsPreservePartyAndWakeAtHomeWithoutSaving()
        {
            yield return LoadFinale();
            var common = Common.Instance; var context = common.CampaignContext; var game = harness.Game;
            var session = game.Finale.Session; var originalGrid = context.Grid;
            int writes = harness.Store.Writes;
            string checkpoint = harness.Store.Json;
            var roster = common.GameSaveData.Roster.Select(a => a.AllyId).ToArray();
            var inventory = game.PlayerController.Inventory.InventoryItems.ToArray();
            game.PlayerController.Gold = 4321;
            for (int floor = 1; floor <= 5; floor++)
            {
                var oldTerrain = game.CurrentDungeon;
                for (int sweep = 0; !session.FloorCleared; sweep++)
                {
                    Assert.That(sweep, Is.LessThan(100), "Finale clearance must make progress.");
                    foreach (var enemy in game.Enemies.OfType<Enemy>().ToArray())
                    {
                        if (enemy.GetComponent<FinaleActor>().Hidden) Reveal(enemy);
                        Kill(enemy);
                    }
                    game.Finale.EndTurn();
                }
                Assert.That(session.CurrentFloor, Is.EqualTo(floor));
                Assert.That(context.State.Finished, Is.False);
                Assert.That(session.Plan.Floors.Count, Is.EqualTo(5));
                harness.Ally.SetPosition(session.Floor.Stairs.ToCell());
                harness.Ally.Vitals.SP = 1; harness.Ally.SyncDisplayedStats();
                DungeonPreferences.AnimationOverride = floor % 2 == 1
                    ? DungeonAnimationMode.Normal : DungeonAnimationMode.NoAnimations;
                Assert.That(game.Finale.RequestExit(), Is.True);
                if (floor < 5)
                {
                    int loadingFrames = 0;
                    double deadline = Time.realtimeSinceStartupAsDouble + harness.TimeoutSeconds;
                    while (!game.IsReady || game.Finale.IsTransitioning)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Next finale floor timed out.");
                        AssertCovered(common.ScreenTransition, floor + 1);
                        if (loadingFrames++ == 0)
                            ScreenCapture.CaptureScreenshot($"Temp/FinaleDebug/loading-floor-{floor + 1}.png");
                        yield return null;
                    }
                    Assert.That(loadingFrames, Is.GreaterThan(0), "Each floor must load behind a rendered cover.");
                    yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf, "next floor reveal");
                    Assert.That(oldTerrain == null, Is.True, "Previous terrain must be released.");
                    Assert.That(game.PlayerController.Gold, Is.EqualTo(4321));
                    Assert.That(game.PlayerController.Inventory.InventoryItems, Is.EquivalentTo(inventory));
                    Assert.That(harness.Ally.Vitals.SP, Is.LessThan(harness.Ally.FinalStats.SPMax), "Ascending must not give a free rest.");
                }
            }
            yield return harness.WaitUntil(() => common.Travel.HomecomingCompleted, "homecoming wake and restored control");
            var town = Object.FindFirstObjectByType<Town>(); var bed = town.GetComponentInChildren<HomeBed>();
            Assert.That(town.Configuration.Id, Is.EqualTo(context.Campaign.StartLocationId));
            Assert.That(town.TownPlayer.ControllingTownAlly.Id, Is.EqualTo(common.GameSaveData.ProtagonistId));
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(bed.Tile - Vector3Int.up));
            Assert.That(town.TownPlayer.CutsceneLocked || bed.IsSleeping || common.ScreenTransition.BlockScreen.activeSelf, Is.False);
            Assert.That(town.TownPlayer.CameraController.enabled, Is.True);
            Assert.That(town.TownPlayer.RecruitedAllies.All(a => a.Hp == -1 && a.Sp == -1), Is.True);
            Assert.That(context.Grid, Is.SameAs(originalGrid)); Assert.That(context.State.Finished, Is.True);
            Assert.That(common.GameSaveData.Roster.Select(a => a.AllyId), Is.EquivalentTo(roster));
            Assert.That(town.TownPlayer.Gold, Is.EqualTo(4321));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes)); Assert.That(harness.Store.Json, Is.EqualTo(checkpoint));
            Assert.That(Object.FindFirstObjectByType<GameOverScreen>(), Is.Null);
            Assert.That(CampaignSaving.Commit(common, "town-0/home", bed.Tile - Vector3Int.up, Facing.Down, out var error), Is.True, error);
            common.Travel.Continue();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true && !common.Travel.IsTransitioning, "completed save continues");
            Assert.That(common.CampaignContext.State.Finished, Is.True);
            common.CampaignContext.Position = common.CampaignContext.Grid.Locations[context.Campaign.FinalLocationId];
            Assert.That(common.CampaignContext.BeginDungeon(), Is.False);
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator RetreatResetsFinaleButRetainsLootAndDoesNotOverwriteCheckpoint()
        {
            yield return LoadFinale();
            var common = Common.Instance; var session = harness.Game.Finale.Session;
            string checkpoint = harness.Store.Json; int writes = harness.Store.Writes;
            var enemy = harness.Game.Enemies.OfType<Enemy>().First(e => !e.GetComponent<FinaleActor>().Hidden);
            Kill(enemy); harness.Game.Finale.EndTurn(); Assert.That(session.Defeated, Is.Not.Empty);
            harness.Game.PlayerController.Gold = 8765;
            GameOverScreen.Retreat(harness.Game.PlayerController);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "retreat town");
            Assert.That(common.CampaignContext.Finale, Is.Null); Assert.That(common.CampaignContext.State.Finished, Is.False);
            Assert.That(common.GameSaveData.TownSaveData.Gold, Is.EqualTo(8765));
            Assert.That(harness.Store.Json, Is.EqualTo(checkpoint)); Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }
    }
}
#endif
