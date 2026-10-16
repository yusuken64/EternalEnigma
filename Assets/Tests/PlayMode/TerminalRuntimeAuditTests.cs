#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalRuntimeAuditTests
    {
        GameTestHarness harness;
        TestInputScope input;
        IDisposable preferences;
        DungeonAnimationMode? animation;
        [UnitySetUp] public IEnumerator Setup()
        {
            preferences = GamePreferences.Isolate(); input = new TestInputScope();
            animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            Directory.CreateDirectory("Temp/TerminalAudit/Transitions");
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            TerminalMode.SetRequested(false);
            try { yield return harness.Cleanup(); }
            finally { input?.Dispose(); preferences?.Dispose(); DungeonPreferences.AnimationOverride = animation; }
        }
        [UnityTest] public IEnumerator SceneCoverDefersModeSwitchAndDestinationRetainsOneTerminalOwner()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlocksInput, "menu reveal");
            TerminalMode.SetRequested(true);
            yield return harness.WaitUntil(() => TerminalMode.Effective, "terminal menu");
            var screen = common.GetComponentInChildren<TerminalScreen>(true);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            common.GameSaveData = menu.CreateNewSave(42);
            common.Travel.NewCampaign(42);
            Assert.That(common.ScreenTransition.BlocksInput, Is.True);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F11)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(TerminalMode.Requested, Is.False);
            Assert.That(TerminalMode.Effective, Is.True, "A mode request cannot flash the 3D world through an active cover.");
            bool opaque = false, partial = false;
            var frames = new List<string> { "seconds,alpha,terminal,requested,animation,scene" };
            float deadline = Time.realtimeSinceStartup + 120;
            while (common.ScreenTransition.BlocksInput)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                Assert.That(TerminalMode.Effective, Is.True);
                Assert.That(common.ScreenTransition.ShutterScreen.canvas.sortingOrder, Is.GreaterThan(screen.GetComponent<Canvas>().sortingOrder));
                float alpha = common.ScreenTransition.ShutterScreen.color.a;
                frames.Add(FormattableString.Invariant($"{Time.realtimeSinceStartup:F3},{alpha:F4},{TerminalMode.Effective},{TerminalMode.Requested},{DungeonPreferences.AnimationMode},{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}"));
                if (!opaque && alpha >= .999f)
                { opaque = true; ScreenCapture.CaptureScreenshot("Temp/TerminalAudit/Transitions/town-covered.png"); }
                if (opaque && !partial && alpha > .1f && alpha < .9f)
                { partial = true; ScreenCapture.CaptureScreenshot("Temp/TerminalAudit/Transitions/town-revealing.png"); }
                yield return null;
            }
            File.WriteAllLines("Temp/TerminalAudit/Transitions/town-frames.csv", frames);
            yield return harness.WaitUntil(() => !TerminalMode.Effective, "deferred 3D view");
            Assert.That(opaque, Is.True, "Travel must present an opaque cover.");
            Assert.That(partial, Is.True, "Animated travel must include a visible reveal.");
            Assert.That(Object.FindFirstObjectByType<Town>().IsReady, Is.True);
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(common.GetComponents<TerminalRuntime>().Length, Is.EqualTo(1));
            Assert.That(common.GetComponentsInChildren<TerminalScreen>(true).Length, Is.EqualTo(1));
            var town = Object.FindFirstObjectByType<Town>();
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().GetParsedText(),
                Does.Contain(TerminalSceneSnapshots.Town(town).Title), "The destination replaces the menu frame with its named town map.");
        }

        [UnityTest] public IEnumerator ReturnToMenuBindsDestinationWhileModeSwitchIsDeferred()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            common.GameSaveData = menu.CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !common.ScreenTransition.BlocksInput, "campaign town reveal");
            TerminalMode.SetRequested(true);
            yield return harness.WaitUntil(() => TerminalMode.Effective, "terminal gameplay");
            var screen = common.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(common.Travel.ReturnToMenu(), Is.True);
            Assert.That(common.ScreenTransition.BlocksInput, Is.True);
            TerminalMode.SetRequested(false);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true,
                "covered return menu");
            menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(common.ScreenTransition.BlocksInput, Is.True, "Inspect destination bindings before the reveal finishes.");
            Assert.That(TerminalMode.Requested, Is.False);
            Assert.That(TerminalMode.Effective, Is.True, "The outgoing presentation remains applied until the cover ends.");
            Assert.That(menu.GetComponent<TerminalMainMenuView>(), Is.Not.Null,
                "A deferred preference cannot skip binding the destination terminal view.");
            Assert.That(menu.StartButton.GetComponentInChildren<TMP_Text>().font, Is.SameAs(screen.FontAsset));
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.True);
            yield return null; // The runtime must safely paint the bound menu while still covered.
            yield return harness.WaitUntil(() => !common.ScreenTransition.BlocksInput && !TerminalMode.Effective,
                "deferred menu presentation switch");
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(menu.StartButton.GetComponentInChildren<TMP_Text>().font, Is.Not.SameAs(screen.FontAsset));
        }

        [UnityTest] public IEnumerator TownFrameClearsHudAndDockAndHistoryShortcutRestoresMap()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.2f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(screen.ContentRect.anchorMax.y, Is.LessThan(.89f));
            int fullColumns = screen.Columns;
            var menus = Object.FindFirstObjectByType<TownMenuManager>();
            menus.OpenPartyMenu(PartyMenuTab.Inventory); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.ContentRect.anchorMax.x, Is.LessThanOrEqualTo(.73f));
            Assert.That(screen.Columns, Is.LessThan(fullColumns));
            menus.PartyMenu.CloseDialog(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.Columns, Is.EqualTo(fullColumns));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(menus.CurrentDialog, Is.TypeOf<EventHistoryDialog>());
            Assert.That(MenuUIInputModule.Active.OwnsFocus(menus.CurrentDialog), Is.True);
            menus.CurrentDialog.CloseDialog(); yield return new WaitForSecondsRealtime(.2f);
            var history = Common.Instance.GetComponentsInChildren<Button>().Single(button => button.name == "History");
            Assert.That(history.gameObject.activeInHierarchy, Is.True);
            var pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select)); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            Assert.That(TerminalMode.Requested, Is.False, "The advertised controller View shortcut switches presentation.");
            Assert.That(history.gameObject.activeSelf, Is.False);
            Assert.That(screen.GetComponent<Canvas>().enabled, Is.False);
        }

        [UnityTest] public IEnumerator FloorCoverDefersPresentationToggleUntilItsTitleHasFinished()
        {
            yield return harness.LoadDungeon(new TestScenario());
            yield return harness.WaitUntil(() => !Common.Instance.ScreenTransition.BlocksInput, "floor entry reveal");
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.2f);
            var oldFloor = harness.Game.CurrentDungeon;
            harness.Game.AdvanceFloor();
            Assert.That(Common.Instance.ScreenTransition.BlocksInput, Is.True);
            TerminalMode.SetRequested(false);
            Assert.That(TerminalMode.Effective, Is.True);
            float deadline = Time.realtimeSinceStartup + 120;
            while (Common.Instance.ScreenTransition.BlocksInput)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                Assert.That(TerminalMode.Effective, Is.True);
                yield return null;
            }
            yield return harness.WaitUntil(() => !TerminalMode.Effective, "terminal floor toggle");
            Assert.That(harness.Game.CurrentDungeon, Is.Not.SameAs(oldFloor));
            Assert.That(harness.Game.IsReady, Is.True);
            TerminalMode.SetRequested(true); yield return new WaitForSecondsRealtime(.2f);
            var screen = Common.Instance.GetComponentInChildren<TerminalScreen>(true);
            Assert.That(screen.GetComponentInChildren<TextMeshProUGUI>().GetParsedText(), Does.Contain("Dungeon floor 2"));
        }
    }
}
#endif
