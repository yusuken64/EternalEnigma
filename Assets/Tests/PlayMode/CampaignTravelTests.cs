#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class CampaignTravelTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness { TimeoutSeconds = 100 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();
        private IEnumerator WaitTown() => harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "campaign town");
        private IEnumerator WaitWorld() => harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true, "shared overworld");
        private IEnumerator StartCampaign()
        {
            yield return harness.LoadMainMenu(null);
            Object.FindFirstObjectByType<MainMenu>().StartGame_Clicked();
            yield return WaitTown();
        }
        [UnityTest]
        public IEnumerator OverworldBuildsOnFirstExitAndReusesTerrainAcrossTownAndDungeonTravel()
        {
            yield return StartCampaign();
            var common = Common.Instance;
            var context = common.CampaignContext;
            Assert.That(context.IsGridGenerated, Is.False);
            Assert.That(common.OverworldTerrain.IsBuilt, Is.False);
            Assert.That(context.BeginTownDungeon("story-0"), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            Assert.That(context.IsGridGenerated, Is.False);
            Assert.That(common.Travel.ExitTown(Object.FindFirstObjectByType<Town>()), Is.True);
            yield return WaitWorld();
            var cache = common.OverworldTerrain;
            var root = cache.Root;
            var grid = context.Grid;
            var biomeMesh = root.GetComponentsInChildren<MeshFilter>().First(m => m.sharedMesh.name.EndsWith(" floor")).sharedMesh;
            Assert.That(cache.BuildCount, Is.EqualTo(1));
            Assert.That(common.Travel.EnterLocation(), Is.True);
            yield return WaitTown();
            Assert.That(root.activeSelf, Is.False);
            Assert.That(biomeMesh != null, Is.True);
            Assert.That(common.Travel.ExitTown(Object.FindFirstObjectByType<Town>()), Is.True);
            yield return WaitWorld();
            Assert.That(cache.Root, Is.SameAs(root));
            Assert.That(root.activeSelf, Is.True);
            Assert.That(context.Grid, Is.SameAs(grid));
            Assert.That(cache.BuildCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh == biomeMesh), Is.True);
            context.Position = grid.Locations["repeatable-0"];
            Assert.That(common.Travel.EnterLocation(), Is.True);
            yield return harness.WaitForIdle();
            Assert.That(root.activeSelf, Is.False);
            Assert.That(common.Travel.FinishDungeon(true, Game.Instance.PlayerController), Is.True);
            yield return WaitWorld();
            Assert.That(cache.Root, Is.SameAs(root));
            Assert.That(cache.BuildCount, Is.EqualTo(1));
            Assert.That(context.Keys, Contains.Item("Town area key"));
            var world = Object.FindFirstObjectByType<OverworldScene>();
            Assert.That(world.Context.Gates, Is.SameAs(context.Gates));
            Assert.That(common.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null, "menu before changing campaigns");
            common.BeginSandbox(99);
            Assert.That(cache.IsBuilt, Is.False);
            yield return null;
            Assert.That(root == null, Is.True);
            Assert.That(biomeMesh == null, Is.True);
            Assert.That(common.CampaignContext.IsGridGenerated, Is.False);
            common.EndSandbox();
        }
        [UnityTest]
        public IEnumerator TownEntranceVictoryAndDuplicateCallbacksPersistKeyAndReturnInsideTown()
        {
            yield return StartCampaign();
            var common = Common.Instance; var context = common.CampaignContext;
            var town = Object.FindFirstObjectByType<Town>();
            Assert.That(context.IsSandbox, Is.False);
            Assert.That(town.Configuration.Id, Is.EqualTo("town-0"));
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(new Vector3Int(10, 2, 0)));
            for (int y = 0; y < town.WalkableMap.TileWorldCreator.twcAsset.mapHeight / 2; y++)
                Assert.That(town.WalkableMap.CanWalkTo(new Vector3Int(10, y, 0), new Vector3Int(10, y + 1, 0)), Is.True);
            Assert.That(common.Travel.ExitTown(town), Is.False);
            Common.Instance.MessageDialog.CloseDialog();
            var target = context.Grid.Locations["town-0"];
            Assert.That(common.Travel.EnterTownDungeon(town, "story-1"), Is.False);
            town.TownBuildings.First(b => b.Definition.DialogId == "entrance").Interact(town.TownPlayer, null);
            yield return null; // RePopulateObjects retires the legacy rows at the end of the frame.
            var entrance = (EntranceDialog)Object.FindFirstObjectByType<TownMenuManager>().CurrentDialog;
            var choices = entrance.Container.GetComponentsInChildren<DungeonTierItem>();
            Assert.That(choices.Length, Is.EqualTo(1));
            choices[0].OnClick();
            Assert.That(context.State.PendingDungeon, Is.EqualTo("story-0"));
            Assert.That(common.Travel.EnterTownDungeon(town, "story-0"), Is.False);
            yield return harness.WaitForIdle();
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlockScreen.activeSelf,
                "dungeon entry screen reveal");
            Assert.That(common.ScreenTransition.ShutterScreen.gameObject.activeSelf, Is.False);
            Assert.That(common.GameSaveData.DungeonSaveData.StartFloor, Is.EqualTo(1));
            Assert.That(common.GameSaveData.DungeonSaveData.EndFloor, Is.EqualTo(5));
            Assert.That(Game.Instance.Allies.Select(a => a.TownAllyId), Is.EqualTo(common.GameSaveData.TownSaveData.RecruitedAlliesData.Select(a => a.AllyId)));
            Game.Instance.PlayerController.Gold = 123;
            Assert.That(common.Travel.FinishDungeon(true, Game.Instance.PlayerController), Is.True);
            Assert.That(common.Travel.FinishDungeon(true, Game.Instance.PlayerController), Is.False);
            yield return WaitTown();
            Assert.That(context.Position, Is.EqualTo(target));
            Assert.That(context.State.Scene, Is.EqualTo("Town"));
            Assert.That(context.Keys, Contains.Item("Town gate key"));
            Assert.That(context.Resolved, Does.Not.Contain("starter-exit"));
            var saved = SaveSystem.LoadData();
            Assert.That(saved.Campaign.Keys, Contains.Item("Town gate key"));
            Assert.That(saved.TownSaveData.Gold, Is.EqualTo(223));
            var restored = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), saved.Campaign);
            Assert.That(restored.Position, Is.EqualTo(target));
            Assert.That(context.Keys, Does.Not.Contain("Town area key"));
            Assert.That(common.Travel.ExitTown(Object.FindFirstObjectByType<Town>()), Is.True);
            yield return WaitWorld();
            context.Position = context.Grid.Locations["repeatable-0"];
            Assert.That(common.Travel.EnterLocation(), Is.True);
            yield return harness.WaitForIdle();
            Assert.That(common.Travel.FinishDungeon(true, Game.Instance.PlayerController), Is.True);
            yield return WaitWorld();
            Assert.That(context.Keys, Contains.Item("Town area key"));
            Assert.That(context.Resolved, Does.Not.Contain("starter-exit"));
            Assert.That(context.State.Scene, Is.EqualTo("Overworld"));
        }
        [UnityTest]
        public IEnumerator InterruptedDungeonRestoresEntranceAndPreRunInventoryWhileAbandonmentUsesLossRules()
        {
            yield return StartCampaign();
            var common = Common.Instance; var context = common.CampaignContext;
            common.Travel.EnterTownDungeon(Object.FindFirstObjectByType<Town>(), "story-0"); yield return harness.WaitForIdle();
            string before = common.GameSaveData.PreRunTownJson;
            var generator = Game.Instance.DungeonGenerator;
            var layout = generator.TileWorldCreator.GetMapOutputFromBlueprintLayer(generator.FloorLayerName).Cast<bool>().ToArray();
            common.GameSaveData = SaveSystem.LoadData(); // Simulate process restart using disk snapshot.
            common.Travel.Continue(); yield return WaitTown();
            Assert.That(JsonUtility.ToJson(common.GameSaveData.TownSaveData), Is.EqualTo(before));
            Assert.That(common.CampaignContext.Position, Is.EqualTo(context.Grid.Locations["story-0"]));
            Assert.That(common.CampaignContext.Keys, Is.Empty);
            Assert.That(common.Travel.EnterTownDungeon(Object.FindFirstObjectByType<Town>(), "story-0"), Is.True); yield return harness.WaitForIdle();
            generator = Game.Instance.DungeonGenerator;
            Assert.That(generator.TileWorldCreator.GetMapOutputFromBlueprintLayer(generator.FloorLayerName).Cast<bool>(), Is.EqualTo(layout));
            GameOverScreen.CommitAbandonedRun(Game.Instance.PlayerController);
            Assert.That(common.GameSaveData.TownSaveData.InventoryItems, Is.Empty);
            Assert.That(common.CampaignContext.State.Scene, Is.EqualTo("Town"));
            Assert.That(common.CampaignContext.State.LocationId, Is.EqualTo("town-0"));
            Assert.That(common.CampaignContext.Keys, Is.Empty);
            common.Travel.Continue(); yield return WaitTown();
            Assert.That(Object.FindFirstObjectByType<Town>().Configuration.Id, Is.EqualTo("town-0"));
        }
        [UnityTest]
        public IEnumerator RosterAndTownLayoutSurviveBenchingAndRoundTrips()
        {
            yield return StartCampaign();
            var common = Common.Instance; var context = common.CampaignContext;
            var town = Object.FindFirstObjectByType<Town>();
            var floor = town.WalkableMap.TileWorldCreator.GetMapOutputFromBlueprintLayer("Houses").Cast<bool>().ToArray();
            int seed = common.GameSaveData.TownSaveData.TownSeed;
            town.TownPlayer.Gold = 5000;
            var recruit = town.TownAllies.First(); string id = recruit.Id;
            Assert.That(town.Services.Recruit(recruit, out _), Is.True);
            Assert.That(context.Active, Contains.Item(id));
            Assert.That(context.Held.Count, Is.EqualTo(0), "Paid ordinary recruits grant no traversal capabilities.");
            recruit.Skills.Add("Persistent bench record");
            Assert.That(town.Services.Dismiss(recruit, out _), Is.True);
            yield return null;
            Assert.That(context.Roster, Contains.Item(id)); Assert.That(context.Active, Does.Not.Contain(id));
            Assert.That(common.GameSaveData.Roster.Single(a => a.AllyId == id).Skills, Contains.Item("Persistent bench record"));
            Assert.That(context.SetParty(id), Is.True); town.RefreshCampaignParty(); yield return null;
            Assert.That(town.TownPlayer.RecruitedAllies.Single(a => a.Id == id).Skills, Contains.Item("Persistent bench record"));
            Assert.That(town.Services.Dismiss(town.TownPlayer.RecruitedAllies.Single(a => a.Id == common.GameSaveData.ProtagonistId), out _), Is.False);
            Assert.That(context.BeginTownDungeon("story-0"), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            common.Travel.ExitTown(town); yield return WaitWorld();
            common.Travel.EnterLocation(); yield return WaitTown();
            town = Object.FindFirstObjectByType<Town>();
            Assert.That(common.GameSaveData.TownSaveData.TownSeed, Is.EqualTo(seed));
            Assert.That(town.WalkableMap.TileWorldCreator.GetMapOutputFromBlueprintLayer("Houses").Cast<bool>(), Is.EqualTo(floor));
            Assert.That(town.TownPlayer.RecruitedAllies.Select(a => a.Id), Contains.Item(id));
            Assert.That(SaveSystem.LoadData().Roster.Single(a => a.AllyId == id).Skills, Contains.Item("Persistent bench record"));
        }
        [UnityTest]
        public IEnumerator SandboxClaimsCompletionAndExitNeverWriteThePlayerSave()
        {
            yield return harness.LoadCommon();
            string before = harness.Store.Json; var original = Common.Instance.GameSaveData;
            yield return SceneManager.LoadSceneAsync("Overworld"); yield return WaitWorld();
            var world = Object.FindFirstObjectByType<OverworldScene>();
            Assert.That(world.Context.IsSandbox, Is.True);
            Assert.That(world.GetComponent<OverworldSandboxControls>().enabled, Is.True);
            world.Context.Position = world.Context.Grid.Locations["story-0"];
            world.ClaimRewards(); Assert.That(world.CollectedKeys, Is.Empty);
            Assert.That(world.SimulateDungeonVictory(), Is.True);
            Assert.That(world.CollectedKeys, Contains.Item("Town gate key"));
            SaveSystem.SaveData(Common.Instance.GameSaveData); SaveSystem.ClearData();
            Assert.That(harness.Store.Json, Is.EqualTo(before));
            Assert.That(Common.Instance.Travel.ReturnToMenu(), Is.True);
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(original));
            Assert.That(harness.Store.Json, Is.EqualTo(before));
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null, "sandbox exit");
            Assert.That(harness.Store.Json, Is.EqualTo(before));
        }
    }
}
#endif
