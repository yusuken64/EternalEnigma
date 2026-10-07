#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using System.Linq;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class MainMenuStartupTests
    {
        GameTestHarness harness;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness != null) yield return harness.Cleanup();
        }

        [UnityTest] public IEnumerator DirectMenuStartupLoadsCommonOnceAndOptionsWork()
        {
            // Reproduce direct Play with the editor bootstrap suppressed after a test session.
            harness = new GameTestHarness();
            yield return harness.LoadMainMenuDirect();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(menu.IsReady, Is.True);
            Assert.That(Object.FindObjectsByType<Common>(FindObjectsSortMode.None).Length, Is.EqualTo(1),
                "Menu and music must share a single Common load.");
            Assert.That(Common.Instance.AudioManager, Is.Not.Null);
            menu.Options_Clicked();
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            Common.Instance.GlobalSettings.Exit_Clicked();
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(menu.NavigationHandler.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator KeyboardConfirmActivatesMainMenuAndOptions()
        {
            using var inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenuDirect();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var menu = Object.FindFirstObjectByType<MainMenu>();
                var module = MenuUIInputModule.Active;
                bool performed = false;
                module.UI.Submit.performed += _ => performed = true;
                var options = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b =>
                    MenuUIInputModule.IsUsable(b.gameObject) && Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == "Options_Clicked"));
                foreach (var key in new[] { Key.Enter, Key.Space, Key.NumpadEnter })
                {
                    EventSystem.current.SetSelectedGameObject(options.gameObject);
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True, key + " did not activate the selected Options button. " +
                        $"performed={performed}, enabled={module.UI.Submit.enabled}, sameAction={module.submit.action == module.UI.Submit}, " +
                        $"selected={EventSystem.current.currentSelectedGameObject?.name}, usable={MenuUIInputModule.IsUsable(options.gameObject)}, " +
                        $"consumed={module.InputConsumed}, navigation={EventSystem.current.sendNavigationEvents}, currentModule={EventSystem.current.currentInputModule?.GetType().Name}, " +
                        $"controls={string.Join(",", module.UI.Submit.controls.Select(c => c.path))}");
                    yield return null;
                    Common.Instance.GlobalSettings.Exit_Clicked();
                    yield return null;
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest] public IEnumerator EnterOnInitialNewGameSelectionOpensHeroPicker()
        {
            using var inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenu(null);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "menu ready");
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.StartButton));
            Assert.That(MenuUIInputModule.IsUsable(menu.StartButton), Is.True);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(Object.FindFirstObjectByType<CampaignSlots>(), Is.Not.Null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Not.Null,
                    "Enter must open hero selection after choosing an empty campaign slot.");
                yield return null;
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
#endif
