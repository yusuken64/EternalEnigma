#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class OverworldSaveTests
    {
        private const string Folder = "Temp/OverworldSaving";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameTestHarness harness;
        private TestInputScope input;
        private IDisposable preferences;
        private DungeonAnimationMode? animation;
        private UnityEditor.EditorWindow view;
        private int oldSize;
        private Mouse mouse;
        private Gamepad pad;
        private string[] originalSlots;
        private string expectedRegion;
        private Common Common => global::Common.Instance;
        private OverworldScene World => Object.FindFirstObjectByType<OverworldScene>();
        private GlobalSettings Settings => Common.GlobalSettings;
        private CampaignChoice Choice => Object.FindFirstObjectByType<CampaignChoice>();

        [UnitySetUp] public IEnumerator Setup()
        {
            input = new TestInputScope();
            preferences = GamePreferences.Isolate();
            animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            TerminalMode.SetRequested(false);
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            Directory.CreateDirectory(Folder);
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();
            yield return Resolution(1280, 720);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                DungeonPreferences.AnimationOverride = animation;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, oldSize);
                input?.Dispose();
                preferences?.Dispose();
            }
        }

        private IEnumerator StartWorld()
        {
            yield return harness.LoadMainMenu(null);
            var main = Object.FindFirstObjectByType<MainMenu>();
            var save = main.CreateNewSave(42);
            Common.GameSaveData = save;
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
            Common.CampaignContext = context;
            context.State.Scene = "Overworld";
            var home = context.Campaign.Locations.Single(l => l.Id == context.State.LocationId);
            var destination = context.Campaign.Locations.First(l => l.Kind == LocationKind.Town && l.RegionId != home.RegionId);
            context.Position = context.Grid.Locations[destination.Id];
            expectedRegion = context.Campaign.Regions.Single(r => r.Id == destination.RegionId).DisplayName;
            // A progressed fixture retains its last-town ID while travelling in another region.
            foreach (var prefab in TownSceneLoader.Default.AllyCatalog.Where(p => p.Id != save.ProtagonistId).Take(3))
            {
                save.TownSaveData.RecruitedAlliesData.Add(HeroClassBinding.FromPrefab(prefab,
                    new TownAllyData { AllyId = prefab.Id, AllyName = prefab.Name, Skills = prefab.Skills.ToList() }));
                context.Active.Add(prefab.Id);
            }
            var hero = save.TownSaveData.RecruitedAlliesData[0];
            hero.Level = 4; hero.Experience = 77; hero.Hp = 3; hero.Mana = 1;
            save.TownSaveData.Gold = 321;
            save.TownSaveData.InventoryItems.Add(ItemSaveData.From(Common.ItemManager.ItemDefinitions.First(i => i.ItemName == "Bread").AsInventoryItem(null)));
            CampaignParty.Capture(Common);
            SaveSystem.Capture(Common);
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++) SaveSystem.SaveData(slot, save);
            originalSlots = Enumerable.Range(0, SaveSystem.SlotCount).Select(harness.Store.Read).ToArray();
            SaveSystem.ActiveSlot = 2;
            Common.Travel.Continue();
            yield return Ready();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { mouse, pad };
            yield return MoveOnce();
        }

        private IEnumerator Ready() => harness.WaitUntil(() => World?.IsReady == true && !Common.Travel.IsTransitioning, "overworld save ready");

        private IEnumerator MoveOnce()
        {
            foreach (var offset in new[] { Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right })
                if (World.TryMove(offset.x, offset.y))
                { yield return harness.WaitUntil(() => !World.IsMoving, "overworld step"); yield break; }
            Assert.Fail("The save fixture needs a walkable neighbouring tile.");
        }

        private IEnumerator Click(Button button)
        {
            button.Select();
            yield return null;
            var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
        }

        private IEnumerator Resolution(int width, int height)
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view,
                new object[] { new Vector2(width, height), $"Overworld save {width}x{height}" });
            view.Repaint(); yield return null; yield return null;
        }

        private IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); yield return null;
            ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
            yield return new WaitForSecondsRealtime(.15f);
        }

        private void OtherSlotsUnchanged()
        {
            Assert.That(SaveSystem.ActiveSlot, Is.EqualTo(2));
            Assert.That(harness.Store.Read(0), Is.EqualTo(originalSlots[0]));
            Assert.That(harness.Store.Read(1), Is.EqualTo(originalSlots[1]));
            Assert.That(Object.FindFirstObjectByType<CampaignSlots>(), Is.Null, "Saving never opens a slot picker.");
        }

        private void AssertRestored(GameSaveData saved)
        {
            Assert.That(World.Position, Is.EqualTo(new GridPoint(saved.Campaign.X, saved.Campaign.Y)));
            Assert.That(JsonUtility.ToJson(Common.GameSaveData.TownSaveData), Is.EqualTo(JsonUtility.ToJson(saved.TownSaveData)));
            Assert.That(World.Player.Hp, Is.EqualTo(3));
            Assert.That(World.Player.Mana, Is.EqualTo(1));
            Assert.That(World.Player.Level, Is.EqualTo(4));
            CollectionAssert.AreEqual(saved.Campaign.Active, Common.CampaignContext.Active);
            CollectionAssert.AreEquivalent(saved.Campaign.Keys, Common.CampaignContext.Keys);
            OtherSlotsUnchanged();
        }

        [UnityTest] public IEnumerator SaveSequenceUsesOriginalSlotAndRestoresOnContinueAndDefeat()
        {
            yield return StartWorld();
            Common.GameSaveData.TownSaveData.Gold = 432;
            var position = World.Position;
            yield return Capture("01-overworld");
            yield return Click(Object.FindFirstObjectByType<PartyMenuLauncher>().OptionsButton);
            Assert.That(Settings.IsOpen, Is.True);
            Assert.That(Settings.SaveButton.gameObject.activeInHierarchy, Is.True);
            yield return Capture("02-options");
            int writes = harness.Store.Writes;
            yield return Click(Settings.SaveButton);
            Assert.That(Choice.Prompt.text, Is.EqualTo("Save your progress to Slot 3?"));
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
            OtherSlotsUnchanged();
            yield return Capture("03-confirm");
            yield return Click(Choice.Confirm);
            Assert.That(harness.Store.Writes, Is.EqualTo(writes + 1));
            Assert.That(Common.MessageDialog.PromptText.text, Is.EqualTo("Game saved.\nSlot 3"));
            var saved = SaveSystem.LoadData();
            Assert.That(saved.SavePointId, Is.EqualTo("overworld"));
            Assert.That(saved.Summary.Town, Is.EqualTo("Overworld"));
            Assert.That(saved.Summary.Region, Is.EqualTo(expectedRegion));
            Assert.That(saved.Campaign.X, Is.EqualTo(position.X));
            Assert.That(saved.Campaign.Y, Is.EqualTo(position.Y));
            Assert.That(saved.HasArrival, Is.False);
            OtherSlotsUnchanged();
            yield return Capture("04-saved");
            yield return Click(Common.MessageDialog.OkButton);
            yield return MoveOnce();
            Common.GameSaveData.TownSaveData.Gold = 1;
            Common.GameSaveData.TownSaveData.RecruitedAlliesData[0].Hp = 1;
            Assert.That(Common.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true && !Common.Travel.IsTransitioning, "continue menu");
            Object.FindFirstObjectByType<MainMenu>().Continue_Clicked(); yield return null;
            Object.FindFirstObjectByType<CampaignSlots>().Slots[2].Button.onClick.Invoke();
            yield return Ready();
            AssertRestored(saved);
            yield return Capture("05-reloaded");
            yield return MoveOnce();
            Common.GameSaveData.TownSaveData.Gold = 0;
            Assert.That(InnCheckpoint.TryRestore(Common), Is.True, "Game Over's Load last save uses this recovery path.");
            yield return Ready();
            AssertRestored(saved);
            File.WriteAllText(Folder + "/verification.txt", $"Saved and restored Slot 3 at ({position.X}, {position.Y}).\n" +
                "Continue and defeat recovery restored party, vitals, inventory and gold; Slots 1 and 2 were unchanged.\n");
        }

        [UnityTest] public IEnumerator ControllerCancelFailedWriteAndRetryPreserveTheOriginalSlot()
        {
            yield return StartWorld();
            Settings.ShowDialog(); yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Settings.FirstSelected));
            yield return Press(GamepadButton.DpadUp);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Settings.SaveButton.gameObject));
            yield return Press(GamepadButton.South);
            Assert.That(Choice, Is.Not.Null);
            yield return Press(GamepadButton.East);
            Assert.That(Choice, Is.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Settings.SaveButton.gameObject));
            Assert.That(harness.Store.Read(2), Is.EqualTo(originalSlots[2]));
            harness.Store.FailWrites = true;
            yield return Press(GamepadButton.South);
            yield return Press(GamepadButton.DpadLeft);
            yield return Press(GamepadButton.South);
            Assert.That(Common.MessageDialog.PromptText.text, Does.Contain("Saving failed"));
            Assert.That(harness.Store.Read(2), Is.EqualTo(originalSlots[2]));
            OtherSlotsUnchanged();
            yield return Capture("failure-preserves-save");
            harness.Store.FailWrites = false;
            yield return Press(GamepadButton.South);
            Settings.ShowDialog(); yield return null;
            foreach (var size in new[] { new Vector2Int(960, 600), new Vector2Int(1920, 1080) })
            {
                yield return Resolution(size.x, size.y);
                Canvas.ForceUpdateCanvases();
                var label = Settings.SaveButton.GetComponentInChildren<TMP_Text>(); label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False);
                yield return Capture($"options-{size.x}x{size.y}");
            }
            yield return Press(GamepadButton.DpadUp);
            yield return Press(GamepadButton.South);
            yield return Press(GamepadButton.DpadLeft);
            yield return Press(GamepadButton.South);
            Assert.That(Common.MessageDialog.PromptText.text, Does.StartWith("Game saved."));
            OtherSlotsUnchanged();
        }

        [UnityTest] public IEnumerator MainMenuAndDungeonHaveNoSaveAction()
        {
            yield return harness.LoadMainMenu(null);
            Settings.ShowDialog(); yield return null;
            Assert.That(Settings.SaveButton.gameObject.activeSelf, Is.False);
            Assert.That(CampaignSaving.SaveOverworld(Common, out _), Is.False);
            Settings.Exit_Clicked();
            yield return harness.Cleanup();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            Settings.ShowDialog(); yield return null;
            Assert.That(Settings.SaveButton.gameObject.activeSelf, Is.False);
            int writes = harness.Store.Writes;
            Settings.SaveGame_Clicked();
            Assert.That(Choice, Is.Null);
            Assert.That(CampaignSaving.SaveOverworld(Common, out _), Is.False);
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
        }
    }
}
#endif
