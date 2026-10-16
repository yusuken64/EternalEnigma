#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalPartyInputTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private System.IDisposable preferences;
        private DungeonAnimationMode? animation;
        private Keyboard keyboard;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); preferences = GamePreferences.Isolate();
            animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            TerminalMode.SetRequested(false);
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems = true });
            yield return harness.WaitUntil(() => !Common.Instance.ScreenTransition.BlocksInput, "party input reveal");
            keyboard = InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard };
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            TerminalMode.SetRequested(false);
            try { yield return harness.Cleanup(); }
            finally
            {
                inputs?.Dispose(); preferences?.Dispose();
                DungeonPreferences.AnimationOverride = animation;
            }
        }

        [UnityTest] public IEnumerator PartyRowsNavigateThroughExternalHudTabsAndSubmitExternalOptions()
        {
            var manager = MenuManager.Instance;
            var optionsLabel = Object.FindFirstObjectByType<DungeonHud>().OptionsButton.GetComponentInChildren<TMP_Text>();
            var originalFont = optionsLabel.font;
            int hunger = harness.Ally.Vitals.HungerAccumulate;
            TerminalMode.SetRequested(true);
            manager.OpenPartyMenu(PartyMenuTab.Inventory);
            yield return new WaitForSecondsRealtime(.2f);
            var menu = manager.PartyMenu;
            var row = menu.EntryButtons[0];
            Assert.That(menu.InventoryTab.transform.IsChildOf(menu.transform), Is.False,
                "The production header stays outside the dialog hierarchy.");
            row.Select();
            yield return Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(menu.InventoryTab.gameObject));
            var close = menu.InventoryTab.navigation.selectOnUp;
            Assert.That(close, Is.Not.Null);
            yield return Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(close.gameObject));
            yield return Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));

            var options = menu.HudTabs.OptionsButton;
            foreach (bool terminal in new[] { false, true })
            {
                TerminalMode.SetRequested(terminal); yield return null;
                options.Select(); yield return null;
                yield return Press(Key.Enter);
                var settings = Common.Instance.GlobalSettings;
                Assert.That(settings.IsOpen, Is.True, "An allowed external control receives submit in either presentation.");
                Assert.That(MenuUIInputModule.Active.OwnsFocus(settings), Is.True);
                settings.ResumeButton.Select(); yield return null;
                yield return Press(Key.Enter);
                Assert.That(settings.IsOpen, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(options.gameObject),
                    "The external opener regains focus after its modal closes.");
            }
            Assert.That(harness.Ally.Vitals.HungerAccumulate, Is.EqualTo(hunger));

            TerminalMode.SetRequested(false);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(optionsLabel.font, Is.SameAs(originalFont), "HUD and dialog styling have separate owners.");
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
    }
}
#endif
