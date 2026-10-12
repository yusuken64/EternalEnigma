#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FinaleLifecycleTests
    {
        GameTestHarness harness;
        DungeonAnimationMode? animation;
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness { TimeoutSeconds = 240 };
            animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); DungeonPreferences.AnimationOverride = animation; }

        [UnityTest, Timeout(900000)]
        public IEnumerator CompletedSaveSupportsOrdinaryDungeonReturnDefeatAndQuit()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            var save = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Town);
            save.IsSandbox = false; save.Campaign.Finished = true;
            save.Campaign.Completed = save.Campaign.Completed.Append("final-dungeon").Distinct().ToArray();
            save.Campaign.LocationId = save.Campaign.LastTownId = "town-0";
            common.GameSaveData = save; SaveSystem.SaveData(save); common.Travel.Continue();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "completed town");
            var town = Object.FindFirstObjectByType<Town>();
            var bed = town.GetComponentInChildren<HomeBed>();
            town.TownPlayer.Gold = 321; town.WriteSaveData();
            Assert.That(CampaignSaving.Commit(common, "town-0/home", bed.Tile - Vector3Int.up, Facing.Down, out var error), Is.True, error);
            string checkpoint = harness.Store.Json; int writes = harness.Store.Writes;
            Assert.That(common.Travel.EnterTownDungeon(town, "story-0"), Is.True);
            yield return harness.WaitUntil(() => Game.Instance?.IsReady == true, "ordinary dungeon after completion");
            var game = Game.Instance;
            Assert.That(game.Finale, Is.Null); Assert.That(common.CampaignContext.State.Finished, Is.True);
            // Fixture advances the displayed floor; the production return path still enforces its exit objective.
            game.PlayerController.Floor = common.GameSaveData.DungeonSaveData.EndFloor;
            game.PlayerController.Gold = 8765;
            Assert.That(common.Travel.FinishDungeon(true, game.PlayerController), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "ordinary return");
            Assert.That(common.CampaignContext.State.Finished, Is.True);
            Assert.That(common.Travel.HomecomingPending || common.Travel.HomecomingCompleted, Is.False);
            Assert.That(Object.FindFirstObjectByType<GameOverScreen>(), Is.Null);
            Assert.That(common.GameSaveData.TownSaveData.Gold, Is.EqualTo(8765));
            town = Object.FindFirstObjectByType<Town>();
            Assert.That(InnCheckpoint.TryRestore(common), Is.True, "Completed slots remain valid defeat checkpoints.");
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>() != town && Object.FindFirstObjectByType<Town>()?.IsReady == true, "completed checkpoint recovery");
            town = Object.FindFirstObjectByType<Town>();
            Assert.That(town.TownPlayer.Gold, Is.EqualTo(321));
            Assert.That(common.CampaignContext.State.Finished, Is.True);
            town.TownPlayer.Gold = 4321; town.WriteSaveData();
            Assert.That(common.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "quit completed adventure");
            Assert.That(common.GameSaveData.TownSaveData.Gold, Is.EqualTo(321));
            Assert.That(common.GameSaveData.Campaign.Finished, Is.True);
            Assert.That(harness.Store.Json, Is.EqualTo(checkpoint)); Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator FinaleCannotSaveAndDefeatRestoresThePreFinaleTown() => RestorePreFinaleCheckpoint(false);

        [UnityTest, Timeout(900000)]
        public IEnumerator QuittingFinaleRestoresTheExplicitSave() => RestorePreFinaleCheckpoint(true);

        IEnumerator RestorePreFinaleCheckpoint(bool quit)
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            var save = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Dungeon);
            save.IsSandbox = false;
            var checkpoint = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save));
            checkpoint.Campaign.Scene = "Town"; checkpoint.Campaign.PendingDungeon = "";
            checkpoint.Campaign.LocationId = checkpoint.Campaign.LastTownId;
            SaveSystem.SaveData(checkpoint); common.GameSaveData = save; common.Travel.Continue();
            string bytes = harness.Store.Json; int writes = harness.Store.Writes;
            yield return harness.WaitUntil(() => Game.Instance?.IsReady == true, "finale");
            var enemy = Game.Instance.Enemies.OfType<Enemy>().First(e => !e.GetComponent<FinaleActor>().Hidden);
            yield return harness.ExecuteAction(new TakeDamageAction(harness.Ally, enemy, int.MaxValue / 8) { Environmental = true });
            Assert.That(common.CampaignContext.Finale.Defeated, Is.Not.Empty);
            Game.Instance.PlayerController.Gold = 7777;
            Assert.That(CampaignSaving.Commit(common, "forbidden-finale", null, Facing.Down, out _), Is.False);
            var game = Game.Instance;
            if (quit)
            {
                game.GameOverScreen.Quit_Clicked();
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "quit finale");
                Assert.That(common.CampaignContext, Is.Null);
            }
            else
            {
                game.ShowGameOver(false);
                game.GameOverScreen.TryAgain_Clicked();
                yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "pre-finale checkpoint");
                Assert.That(common.CampaignContext.Finale, Is.Null);
                Assert.That(common.CampaignContext.State.Finished, Is.False);
            }
            Assert.That(common.GameSaveData.Campaign.Finished, Is.False);
            Assert.That(common.GameSaveData.TownSaveData.Gold, Is.EqualTo(checkpoint.TownSaveData.Gold));
            Assert.That(harness.Store.Json, Is.EqualTo(bytes)); Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }
    }
}
#endif
