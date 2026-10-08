#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalPlayModeTests
    {
        private GameTestHarness harness;
        private bool hadPreference;
        private int oldPreference;
        [UnitySetUp] public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            hadPreference = PlayerPrefs.HasKey(TerminalMode.PreferenceKey);
            oldPreference = PlayerPrefs.GetInt(TerminalMode.PreferenceKey);
            TerminalMode.SetRequested(false);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var result = TestContext.CurrentContext.Result;
            System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalPlayModeResults.txt"),
                TestContext.CurrentContext.Test.Name + ": " + result.Outcome.Status + " " + (result.Message ?? "").Split('\n')[0] + "\n");
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();
            if (hadPreference) PlayerPrefs.SetInt(TerminalMode.PreferenceKey, oldPreference);
            else PlayerPrefs.DeleteKey(TerminalMode.PreferenceKey);
            PlayerPrefs.Save();
        }
        [UnityTest] public IEnumerator MainMenuToggleRestylesAndRestoresMenu()
        {
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var button = menu.StartButton.GetComponent<UnityEngine.UI.Button>();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var originalText = label.text;
            var originalPosition = ((RectTransform)button.transform.parent).anchorMin;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var view = menu.GetComponent<TerminalMainMenuView>();
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(view, Is.Not.Null);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(label.font, Is.SameAs(screen.FontAsset));
            Assert.That(label.text, Does.Contain("START"));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("#####"));
            Assert.That(((RectTransform)button.transform.parent).anchorMin.x, Is.EqualTo(.31f).Within(.001f));
            yield return new WaitForSecondsRealtime(1.35f);
            int completeTitleLength = screen.GetComponentInChildren<TextMeshProUGUI>().text.Length;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalMainMenu.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.1f);
            var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left, pointerId = -1 };
            var menuButton = (SelectToActivateButton)button;
            menuButton.OnPointerDown(pointer);
            menuButton.OnPointerClick(pointer);
            yield return null;
            var slots = Object.FindFirstObjectByType<CampaignSlots>(FindObjectsInactive.Include);
            Assert.That(slots.gameObject.activeSelf, Is.True, "One click opens the selected menu action.");
            slots.BackButton.onClick.Invoke();
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(label.text, Is.EqualTo(originalText));
            Assert.That(((RectTransform)button.transform.parent).anchorMin, Is.EqualTo(originalPosition));
            TerminalMode.SetRequested(true);
            yield return null;
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text.Length, Is.LessThan(completeTitleLength));
            TerminalMode.SetRequested(false);
        }
        [UnityTest] public IEnumerator TownToggleKeepsControlledCellAndShowsAscii()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var before = town.TownPlayer.ControllingTownAlly.TilemapPosition;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(screen, Is.Not.Null);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalTown.png"));
            yield return null;
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DungeonTogglePreservesAnimationPreferenceAndKeepsTurn()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var before = harness.Ally.TilemapPosition;
            var savedOverride = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            try
            {
                TerminalMode.SetRequested(true);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(TerminalMode.Effective, Is.True);
                Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
                var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
                Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalDungeon.png"));
                yield return null;
                TerminalMode.SetRequested(false);
                yield return null;
                Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(before));
            }
            finally { DungeonPreferences.AnimationOverride = savedOverride; }
        }
        [UnityTest] public IEnumerator DungeonTerminalHidesDuplicateUiButKeepsMapAndHistory()
        {
            yield return harness.LoadDungeon(new TestScenario());
            GameMessages.Post("Before terminal mode");
            yield return null;
            var minimap = Object.FindFirstObjectByType<Minimap>();
            var messages = Object.FindFirstObjectByType<GameMessages>();
            Assert.That(minimap, Is.Not.Null);
            Assert.That(messages, Is.Not.Null);
            var messageCanvas = messages.GetComponentInChildren<Canvas>();
            Assert.That(messageCanvas, Is.Not.Null);
            Assert.That(minimap.minimapImage, Is.Not.Null);
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.True);
            Assert.That(messageCanvas.enabled, Is.True);

            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.False);
            if (minimap.Frame != null) Assert.That(minimap.Frame.enabled, Is.False);
            Assert.That(minimap.dungeonMap, Is.Not.Null);
            Assert.That(messageCanvas.enabled, Is.False);
            GameMessages.Post("Terminal history remains live");
            Assert.That(messages.History, Does.Contain("Terminal history remains live"));

            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(minimap.minimapImage.gameObject.activeSelf, Is.True);
            if (minimap.Frame != null) Assert.That(minimap.Frame.enabled, Is.True);
            Assert.That(messageCanvas.enabled, Is.True);
        }
        [UnityTest] public IEnumerator OverworldToggleUsesLiveCampaignPosition()
        {
            yield return harness.LoadCommon();
            Common.Instance.BeginSandbox(42);
            yield return SceneManager.LoadSceneAsync("Overworld", LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName("Overworld");
            var world = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OverworldScene>()).Single();
            yield return harness.WaitUntil(() => world.IsReady, "terminal overworld");
            var before = world.Position;
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(TerminalMode.Effective, Is.True);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "../Temp/TerminalOverworld.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.1f);
            TerminalMode.SetRequested(false);
            Assert.That(world.Position, Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator DungeonFloorReplacementRefreshesTerminal()
        {
            yield return harness.LoadDungeon(new TestScenario());
            TerminalMode.SetRequested(true);
            yield return new WaitForSecondsRealtime(.25f);
            var oldFloor = harness.Game.CurrentDungeon;
            int number = harness.Game.PlayerController.Floor;
            harness.Game.AdvanceFloor();
            yield return harness.WaitUntil(() => harness.Game.IsReady && harness.Game.CurrentDungeon != oldFloor,
                "terminal next floor");
            yield return new WaitForSecondsRealtime(.25f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(number + 1));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("Dungeon floor " + (number + 1)));
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("@"));
        }
    }
}
#endif
