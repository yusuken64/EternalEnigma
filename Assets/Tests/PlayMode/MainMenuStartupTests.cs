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

        [UnityTest] public IEnumerator MouseClicksReachMainMenuButtonsAndDialogs()
        {
            using var inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenuDirect();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var menu = Object.FindFirstObjectByType<MainMenu>();
                yield return Click(mouse, menu.StartButton.GetComponent<Button>());
                var slots = Object.FindFirstObjectByType<CampaignSlots>();
                Assert.That(slots, Is.Not.Null, "Clicking the initially selected New Journey button must open campaign slots.");
                yield return Click(mouse, slots.BackButton);
                Assert.That(menu.NavigationHandler.gameObject.activeInHierarchy, Is.True);

                var options = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b =>
                    MenuUIInputModule.IsUsable(b.gameObject) && Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i =>
                        b.onClick.GetPersistentMethodName(i) == nameof(MainMenu.Options_Clicked)));
                yield return Click(mouse, options);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject),
                    "The first click must select Options.");
                yield return Click(mouse, options);
                Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True, "Clicking selected Options must open settings.");
                Common.Instance.GlobalSettings.Exit_Clicked();
                yield return null;

                yield return Click(mouse, menu.ContinueButton.GetComponent<Button>());
                yield return Click(mouse, menu.ContinueButton.GetComponent<Button>());
                Assert.That(Object.FindFirstObjectByType<CampaignSlots>(), Is.Not.Null,
                    "Campaigns must remain clickable after returning from settings.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " has no pointer hit target at " + position +
                $" (screen {Screen.width}x{Screen.height}, active={button.gameObject.activeInHierarchy}, rect={rect.rect}, scale={rect.lossyScale}).");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                button.name + " is blocked by " + hits[0].gameObject.name);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
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
