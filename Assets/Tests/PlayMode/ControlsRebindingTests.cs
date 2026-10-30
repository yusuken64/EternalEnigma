#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ControlsRebindingTests
    {
        private GameTestHarness harness;
        private TestInputScope inputs;
        private IDisposable preferences;
        private Keyboard keyboard;
        private Gamepad pad;
        private Mouse mouse;
        private GlobalSettings settings;
        private ControlsRebindingScreen screen;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            harness = new GameTestHarness();
            yield return harness.LoadMainMenu(null);
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };
            settings = Common.Instance.GlobalSettings;
            settings.ShowDialog();
            var tab = settings.TabGroup.TabContents.Single(t => t.Content.name == "Controls");
            tab.TabButton.Select();
            yield return null;
            screen = tab.Content.GetComponent<ControlsRebindingScreen>();
            Assert.That(screen, Is.Not.Null);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try
            {
                if (screen != null) screen.CancelRebind();
                yield return harness.Cleanup();
            }
            finally
            {
                Time.timeScale = 1;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                inputs?.Dispose();
                preferences?.Dispose();
                ControlBindings.Reload();
            }
        }

        private static int Binding(InputAction action, string path) =>
            Enumerable.Range(0, action.bindings.Count).Single(i => action.bindings[i].path == path);

        private IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.2f);
        }

        [UnityTest] public IEnumerator CapturedKeyboardInputAppliesToLivePlayerAndSurvivesReopening()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
            int index = Binding(action, "<Keyboard>/period");
            Time.timeScale = 0;
            screen.BeginRebind("Player/Wait", index);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(screen.IsRebinding, Is.True);
            yield return KeyPress(Key.K);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/k"));
            var player = Common.Instance.MenuInputHandler.PlayerInput.actions.FindAction("Player/Wait", true);
            Assert.That(player.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/k"));
            Assert.That(settings.IsOpen, Is.True);
            settings.Exit_Clicked();
            yield return null;
            Common.Instance.MenuInputHandler.PlayerInput.actions.devices = new InputDevice[] { keyboard, mouse };
            Assert.That(player.enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Period));
            yield return null;
            Assert.That(player.IsPressed(), Is.False, "The original key no longer triggers the live action.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            yield return null;
            Assert.That(player.IsPressed(), Is.True, "The rebound key triggers the actual PlayerInput action.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            settings.ShowDialog();
            yield return null;
            Assert.That(player.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/k"));
            Assert.That(GamePreferences.HasKey(ControlBindings.PreferenceKey), Is.True);
        }

        [UnityTest] public IEnumerator EscapeCancelsCaptureWithoutChangingBindingOrClosingSettings()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
            int index = Binding(action, "<Keyboard>/period");
            screen.BeginRebind("Player/Wait", index);
            yield return new WaitForSecondsRealtime(.3f);
            System.IO.Directory.CreateDirectory("Temp/ControlsRebinding");
            ScreenCapture.CaptureScreenshot("Temp/ControlsRebinding/capture-keyboard.png");
            yield return new WaitForSecondsRealtime(.2f);
            yield return KeyPress(Key.Escape);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/period"));
            Assert.That(settings.IsOpen, Is.True, "Capture cancellation must consume Escape before Settings handles it.");
        }

        [UnityTest] public IEnumerator GamepadCaptureIgnoresKeyboardAndAcceptsAnotherGamepadButton()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
            int index = Binding(action, "<Gamepad>/leftStickPress");
            screen.BeginRebind("Player/Wait", index);
            yield return new WaitForSecondsRealtime(.3f);
            yield return KeyPress(Key.K);
            Assert.That(screen.IsRebinding, Is.True, "Keyboard input must not replace a controller binding.");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Gamepad>/rightShoulder"));
            Assert.That(settings.IsOpen, Is.True);
        }

        [UnityTest] public IEnumerator MovementCompositePartCapturesAButtonAndUpdatesMenuNavigation()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Move", true);
            int index = Binding(action, "<Keyboard>/w");
            screen.BeginRebind("Player/Move", index);
            yield return new WaitForSecondsRealtime(.3f);
            yield return KeyPress(Key.I);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/i"));
            Assert.That(action.bindings[index].isPartOfComposite, Is.True);
            var navigate = MenuUIInputModule.Active.actionsAsset.FindAction("UI/Navigate", true);
            Assert.That(navigate.bindings[Binding(navigate, "<Keyboard>/w")].effectivePath, Is.EqualTo("<Keyboard>/i"));
        }

        [UnityTest] public IEnumerator VectorMovementBindingCapturesAnEntireStick()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Move", true);
            int index = Binding(action, "<Gamepad>/leftStick");
            screen.BeginRebind("Player/Move", index);
            yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up });
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Gamepad>/rightStick"));
            Assert.That(settings.IsOpen, Is.True);
        }

        [UnityTest] public IEnumerator ClosingSettingsCancelsPendingCaptureAndRestoresMenuInput()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
            int index = Binding(action, "<Keyboard>/period");
            screen.BeginRebind("Player/Wait", index);
            yield return new WaitForSecondsRealtime(.3f);
            settings.Exit_Clicked();
            yield return KeyPress(Key.K);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/period"));
            settings.ShowDialog();
            settings.ResumeButton.Select();
            yield return null;
            yield return KeyPress(Key.Escape);
            Assert.That(settings.IsOpen, Is.False, "UI actions work again after capture cleanup.");
        }

        [UnityTest] public IEnumerator VisibleCancelButtonRemainsClickableDuringCapture()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Wait", true);
            int index = Binding(action, "<Keyboard>/period");
            screen.BeginRebind("Player/Wait", index);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)screen.CancelButton.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.IsRebinding, Is.False);
            Assert.That(action.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/period"),
                "The Cancel click must not become the action's new binding.");
            Assert.That(settings.IsOpen, Is.True);
        }
    }
}
#endif
