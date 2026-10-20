#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class BookDungeonTravelTests
    {
        private GameTestHarness harness;
        private DungeonAnimationMode? oldAnimation;
        private bool? oldControl;
        private System.IDisposable preferences;
        private UnityEditor.EditorWindow view;
        private int oldView;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp] public IEnumerator Setup()
        {
            oldAnimation = DungeonPreferences.AnimationOverride;
            oldControl = DungeonPreferences.FullControlOverride;
            preferences = GamePreferences.Isolate();
            TerminalMode.SetRequested(false);
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            DungeonPreferences.FullControlOverride = false;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            oldView = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            Directory.CreateDirectory("Temp/BookTravel");
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();
            DungeonPreferences.AnimationOverride = oldAnimation;
            DungeonPreferences.FullControlOverride = oldControl;
            if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, oldView);
            preferences.Dispose();
        }
        private IEnumerator WaitTown() => harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
            !Common.Instance.Travel.IsTransitioning, "book town ready");
        private IEnumerator Start()
        {
            yield return harness.LoadMainMenu(null);
            Object.FindFirstObjectByType<MainMenu>().StartGame();
            yield return WaitTown();
            var context = Common.Instance.CampaignContext;
            context.ReadOpeningBook(); context.ClaimHomeKey(); context.UnlockHomeDoor();
            harness.PrepareOpening();
            Assert.That(Object.FindFirstObjectByType<Town>().DungeonBook, Is.Not.Null);
        }
        private static Town PositionAtBook(string leaderId = null)
        {
            var town = Object.FindFirstObjectByType<Town>();
            if (leaderId != null) town.TownPlayer.SelectAlly(leaderId);
            Assert.That(town.DungeonBook.TryGetStandCell(out var tile), Is.True);
            var hero = town.TownPlayer.ControllingTownAlly;
            hero.TilemapPosition = tile; hero.transform.position = town.WalkableMap.CellToWorld(tile);
            hero.SetFacing(new TownMovement(town.TownPlayer, tile, town.DungeonBook.Tile).GetFacing(town.DungeonBook.Tile - tile));
            town.TownPlayer.StopHeldWalk(); town.RefreshRoofs(); town.TownPlayer.CameraController.SnapToFollowTarget();
            return town;
        }
        private IEnumerator Capture(string name)
        {
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/BookTravel/" + name + ".png");
            yield return null;
        }
        private IEnumerator Resolution(int width, int height)
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view,
                new object[] { new Vector2(width, height), $"Book travel {width}x{height}" });
            view.Repaint(); yield return null; yield return null;
            Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height));
        }

        [UnityTest] public IEnumerator SlotStylePromptLocksUntilClearAndScalesAtThreeResolutions()
        {
            yield return Start();
            var common = Common.Instance;
            var town = PositionAtBook();
            int writes = harness.Store.Writes;
            Assert.That(common.Travel.EnterBookDungeon(town, town.DungeonBook), Is.False);
            foreach (var size in new[] { new Vector2Int(960,600), new Vector2Int(1920,1080), new Vector2Int(2560,1080) })
            {
                yield return Resolution(size.x,size.y);
                Assert.That(town.DungeonBook.TryInteract(town.DungeonBook.Tile), Is.True);
                var prompt = town.GetComponentInChildren<BookDungeonPrompt>();
                Assert.That(prompt.Enter.interactable, Is.False);
                Assert.That(prompt.Back.interactable, Is.True);
                Assert.That(prompt.Card.Artwork.texture, Is.Not.Null);
                Assert.That(prompt.Card.Label.text, Does.Contain(common.CampaignContext.BookDungeon.Title));
                yield return Capture($"prompt-locked-{size.x}x{size.y}");
                prompt.Close();
                Assert.That(town.TownPlayer.CutsceneLocked, Is.False);
            }
            common.CampaignContext.Completed.Add("story-0");
            Assert.That(town.DungeonBook.TryInteract(town.DungeonBook.Tile), Is.True);
            var available = town.GetComponentInChildren<BookDungeonPrompt>();
            Assert.That(available.Enter.interactable, Is.True);
            yield return Capture("prompt-available");
            available.Back.onClick.Invoke();
            Assert.That(common.CampaignContext.State.PendingDungeon, Is.Empty);
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }

        [UnityTest] public IEnumerator FiveFloorsKeepBiomeLootAndReturnLeaderThenReplayAndRetreat()
        {
            yield return Start();
            var common = Common.Instance; var context = common.CampaignContext;
            context.Completed.Add("story-0");
            var ids = context.Campaign.Companions.Take(3).Select(c => c.Id).ToArray();
            context.Roster.UnionWith(ids); Assert.That(context.SetParty(ids), Is.True);
            Object.FindFirstObjectByType<Town>().RefreshCampaignParty();
            yield return null;
            var town = PositionAtBook(ids.Last());
            var tile = town.TownPlayer.ControllingTownAlly.TilemapPosition;
            var facing = town.TownPlayer.ControllingTownAlly.CurrentFacing;
            var leaderId = town.TownPlayer.ControllingTownAlly.Id;
            int writes = harness.Store.Writes;
            var keys = context.Keys.ToArray(); var baseFingerprint = context.State.Fingerprint;
            yield return Resolution(1920,1080);
            Assert.That(town.DungeonBook.TryInteract(town.DungeonBook.Tile), Is.True);
            var prompt = town.GetComponentInChildren<BookDungeonPrompt>();
            Assert.That(prompt.Enter.interactable, Is.True);
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            prompt.Enter.onClick.Invoke();
            Assert.That(common.Travel.IsTransitioning, Is.True);
            Assert.That(common.Travel.EnterBookDungeon(town,town.DungeonBook), Is.False);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("book-departure");
            yield return harness.WaitForIdle();
            Assert.That(context.State.PendingDungeon, Is.EqualTo(context.BookDungeon.Id));
            Assert.That(harness.Game.Allies.Count, Is.EqualTo(4));
            var run = common.GameSaveData.DungeonSaveData;
            Assert.That(run.VisualSelection.Environment, Is.EqualTo(DungeonEnvironmentKind.Outdoor));
            Assert.That(run.LayoutBiome, Is.EqualTo(context.BookDungeon.Biome));
            yield return Capture("book-dungeon");
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness.Game.PlayerController.Gold += 37;
            harness.AddItem("Bread");
            int gold = harness.Game.PlayerController.Gold;
            int items = harness.Game.PlayerController.Inventory.InventoryItems.Count;
            for (int floor = 1; floor <= 5; floor++)
            {
                Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(floor));
                Assert.That(harness.Game.RunPlan.Floor(floor).Options.Biome, Is.EqualTo(context.BookDungeon.Biome));
                Assert.That(harness.Game.DungeonGenerator.CurrentVisuals.Biome, Is.EqualTo(context.BookDungeon.Biome));
                if (floor == 3) yield return Capture("book-dungeon-floor-3");
                if (floor < 5) { harness.Game.AdvanceFloor(); yield return harness.WaitForIdle(); }
            }
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            Assert.That(common.Travel.FinishDungeon(true,harness.Game.PlayerController), Is.True);
            Assert.That(common.Travel.FinishDungeon(true,harness.Game.PlayerController), Is.False);
            yield return WaitTown();
            town = Object.FindFirstObjectByType<Town>();
            Assert.That(town.TownPlayer.ControllingTownAlly.Id, Is.EqualTo(leaderId));
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(tile));
            Assert.That(town.TownPlayer.ControllingTownAlly.CurrentFacing, Is.EqualTo(facing));
            Assert.That(town.TownPlayer.RecruitedAllies.Select(a => a.TilemapPosition).Distinct().Count(), Is.EqualTo(4));
            Assert.That(town.TownPlayer.Gold, Is.EqualTo(gold));
            Assert.That(town.TownPlayer.Inventory.Count, Is.EqualTo(items));
            Assert.That(context.Keys, Is.EquivalentTo(keys));
            Assert.That(context.State.Fingerprint, Is.EqualTo(baseFingerprint));
            Assert.That(context.Completed, Does.Contain(context.BookDungeon.Id));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
            yield return Capture("book-return");
            Assert.That(CampaignSaving.Commit(common,"town-0/inn",tile,facing,out var error), Is.True,error);
            Assert.That(SaveSystem.LoadData().Campaign.Completed, Does.Contain(context.BookDungeon.Id));
            town = PositionAtBook();
            Assert.That(common.Travel.EnterBookDungeon(town,town.DungeonBook), Is.True);
            yield return harness.WaitForIdle();
            Assert.That(common.Travel.FinishDungeon(false,harness.Game.PlayerController,keepLoot:true), Is.True);
            yield return WaitTown();
            Assert.That(Object.FindFirstObjectByType<Town>().DungeonBook.IsOpen, Is.True);
            Assert.That(context.Completed, Does.Contain(context.BookDungeon.Id));
            // Defeat uses the ordinary checkpoint restore, discarding the unsaved run.
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            town = PositionAtBook();
            Assert.That(common.Travel.EnterBookDungeon(town,town.DungeonBook), Is.True);
            yield return harness.WaitForIdle();
            harness.Game.PlayerController.Gold += 999;
            int savedGold = SaveSystem.LoadData().TownSaveData.Gold;
            writes = harness.Store.Writes;
            Assert.That(InnCheckpoint.TryRestore(common), Is.True);
            yield return WaitTown();
            Assert.That(Object.FindFirstObjectByType<Town>().TownPlayer.Gold, Is.EqualTo(savedGold));
            Assert.That(common.CampaignContext.State.PendingDungeon, Is.Empty);
            Assert.That(common.CampaignContext.Completed, Does.Contain(common.CampaignContext.BookDungeon.Id));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }

        [UnityTest] public IEnumerator SupportedExistingSlotUpgradesInMemoryAndPersistsOnlyOnSave()
        {
            yield return Start();
            var common = Common.Instance;
            var oldSave = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(SaveSystem.LoadData()));
            oldSave.Campaign.BookContentVersion = 0;
            SaveSystem.SaveData(oldSave);
            string stored = harness.Store.Json; int writes = harness.Store.Writes;
            Assert.That(common.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true && !common.Travel.IsTransitioning,"menu");
            Assert.That(SaveSystem.Inspect(SaveSystem.ActiveSlot,out _).Campaign.BookContentVersion, Is.Zero);
            Assert.That(harness.Store.Json, Is.EqualTo(stored));
            common.Travel.Continue(); yield return WaitTown();
            var town = Object.FindFirstObjectByType<Town>();
            Assert.That(common.CampaignContext.State.BookContentVersion, Is.EqualTo(1));
            Assert.That(town.DungeonBook, Is.Not.Null);
            Assert.That(harness.Store.Json, Is.EqualTo(stored)); Assert.That(harness.Store.Writes, Is.EqualTo(writes));
            var hero = town.TownPlayer.ControllingTownAlly;
            town.WriteSaveData();
            Assert.That(CampaignSaving.Commit(common,oldSave.SavePointId,hero.TilemapPosition,hero.CurrentFacing,out var error), Is.True,error);
            Assert.That(SaveSystem.LoadData().Campaign.BookContentVersion, Is.EqualTo(1));
            Assert.That(SaveSystem.LoadData().Campaign.Fingerprint, Is.EqualTo(oldSave.Campaign.Fingerprint));
        }

        [UnityTest] public IEnumerator TerminalPreferenceKeepsBookPromptAndCoveredTravelUsable()
        {
            yield return Start();
            var common = Common.Instance;
            common.CampaignContext.Completed.Add("story-0");
            var town = PositionAtBook();
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(town.DungeonBook.TryInteract(town.DungeonBook.Tile), Is.True);
            var prompt = town.GetComponentInChildren<BookDungeonPrompt>();
            Assert.That(prompt.Enter.interactable, Is.True);
            yield return Capture("prompt-terminal");
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            prompt.Enter.onClick.Invoke();
            Assert.That(common.ScreenTransition.HasTravelPresentation, Is.False);
            yield return harness.WaitForIdle();
            Assert.That(TerminalMode.Requested, Is.True);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(common.Travel.FinishDungeon(false,harness.Game.PlayerController,keepLoot:true), Is.True);
            yield return WaitTown();
            Assert.That(TerminalMode.Requested, Is.True);
            Assert.That(Object.FindFirstObjectByType<Town>().DungeonBook.IsOpen, Is.True);
        }
    }
}
#endif
