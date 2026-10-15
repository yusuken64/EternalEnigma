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
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.StartButton),
                "Closing Options restores the opener after the main-menu root becomes active again.");
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

        [UnityTest] public IEnumerator MainMenuCursorClearsButtonEdgesAndCampaignsRestoreTheirOpener()
        {
            using var inputScope = new TestInputScope();
            using var preferences = GamePreferences.Isolate();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenuDirect();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var developer = menu.GetComponent<MainMenuDeveloperControls>();
            var arrow = menu.NavigationHandler.selectionArrow;
            Canvas.ForceUpdateCanvases();
            foreach (var label in menu.StartButton.GetComponentInParent<Canvas>().rootCanvas.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, label.name + ": " + label.text);
            }
            Assert.That(menu.StartButton.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == "Options"), Is.True);
            foreach (var button in new[] { menu.StartButton.GetComponent<Button>(), developer.Toggle })
            {
                button.Select();
                yield return new WaitForSecondsRealtime(.12f);
                Canvas.ForceUpdateCanvases();
                var buttonCorners = new Vector3[4];
                var arrowCorners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(buttonCorners);
                arrow.GetWorldCorners(arrowCorners);
                Assert.That(arrow.gameObject.activeInHierarchy, Is.True);
                Assert.That(arrowCorners.Max(corner => corner.x), Is.LessThan(buttonCorners.Min(corner => corner.x)),
                    "The selection arrow must sit outside " + button.name + " even when button widths differ.");
            }

            menu.ContinueButton.GetComponent<Button>().Select();
            menu.Continue_Clicked();
            yield return null; yield return null;
            var slots = Object.FindFirstObjectByType<CampaignSlots>();
            Assert.That(arrow.gameObject.activeSelf, Is.False, "The main-menu arrow must not follow a campaign-dialog selection.");
            slots.BackButton.onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.ContinueButton),
                "Back from Campaigns must restore Campaigns, not New Journey.");
            menu.StartGame_Clicked(); yield return null;
            slots.BackButton.onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.StartButton));
        }

        [UnityTest] public IEnumerator MainMenuShortcutsOpenOptionsAndCancelDeveloperControls()
        {
            using var inputScope = new TestInputScope();
            using var preferences = GamePreferences.Isolate();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenuDirect();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                var menu = Object.FindFirstObjectByType<MainMenu>();
                var developer = menu.GetComponent<MainMenuDeveloperControls>();
                MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, gamepad };
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True, "Escape opens Options from the main menu.");
                yield return null;
                Common.Instance.GlobalSettings.Exit_Clicked();
                yield return null;

                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True, "Start opens Options from the main menu.");
                yield return null;
                Common.Instance.GlobalSettings.Exit_Clicked();
                yield return null;

                foreach (bool useGamepad in new[] { false, true })
                {
                    developer.Toggle.onClick.Invoke();
                    Assert.That(developer.FirstControl.gameObject.activeInHierarchy, Is.True);
                    Assert.That(developer.Toggle.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("Close developer"));
                    yield return null;
                    if (useGamepad) InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
                    else InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Assert.That(developer.Controls.All(control => !control.activeSelf), Is.True);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(developer.Toggle.gameObject));
                    Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False, "Cancel closes the drawer before opening Options.");
                    yield return null;
                }
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(keyboard);
            }
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

                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject));
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
