using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ControlDeviceStateTests
    {
        private GameTestHarness harness;
        private TestInputScope schemeInputScope;

        [UnityTearDown]
        public IEnumerator CleanupSchemeTest()
        {
            if (harness != null)
            {
                yield return harness.Cleanup();
                harness = null;
            }
            schemeInputScope?.Dispose();
            schemeInputScope = null;
        }

        [UnityTest]
        public IEnumerator UnsupportedDevicesCannotTakeOverKeyboardOrGamepadControls()
        {
            schemeInputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadCommon();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var pad = InputSystem.AddDevice<Gamepad>();
            var touch = InputSystem.AddDevice<Touchscreen>();
            var joystick = InputSystem.AddDevice<Joystick>();
            // Use a concrete XR layout with a non-noisy trigger so this really exercises
            // PlayerInput's unpaired-device listener, unlike noisy tracked-pose changes.
            var xr = InputSystem.AddDevice("OculusTouchController");
            var player = Common.Instance.MenuInputHandler.PlayerInput;
            Assert.That(PlayerInput.all.Count, Is.EqualTo(1));
            Assert.That(player.neverAutoSwitchControlSchemes, Is.False,
                "Supported keyboard/gamepad switching must remain automatic.");
            player.SwitchCurrentActionMap("Player");
            player.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            yield return RejectUnsupportedActivity(player, "Keyboard&Mouse", touch, joystick, xr);
            Assert.That(player.devices, Does.Contain(keyboard));

            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(player.currentControlScheme, Is.EqualTo("Gamepad"));
            Assert.That(player.devices, Does.Contain(pad));
            Assert.That(player.actions["Player/Attack"].ReadValue<float>(), Is.GreaterThan(.5f),
                "A fresh gamepad action must still work after unsupported device activity.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;

            yield return RejectUnsupportedActivity(player, "Gamepad", touch, joystick, xr);
            Assert.That(player.devices, Does.Contain(pad));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            Assert.That(player.currentControlScheme, Is.EqualTo("Keyboard&Mouse"));
            Assert.That(player.actions["Player/Attack"].ReadValue<float>(), Is.GreaterThan(.5f),
                "A fresh keyboard action must automatically regain control from gamepad.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            var supported = new[] { "Keyboard&Mouse", "Gamepad" };
            Assert.That(player.actions.controlSchemes.Select(scheme => scheme.name), Is.EqualTo(supported));
            using (var generated = new DungeonControls())
                Assert.That(generated.controlSchemes.Select(scheme => scheme.name), Is.EqualTo(supported),
                    "The generated wrapper must match the authored action asset.");
        }

        private static IEnumerator RejectUnsupportedActivity(PlayerInput player, string expectedScheme,
            Touchscreen touch, Joystick joystick, InputDevice xr)
        {
            InputSystem.QueueStateEvent(touch, new TouchState
            {
                touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = new Vector2(200, 200)
            });
            yield return null;
            Assert.That(touch.primaryTouch.press.isPressed, Is.True);
            Assert.That(player.currentControlScheme, Is.EqualTo(expectedScheme), "Touch cannot take over input.");
            Assert.That(player.devices, Has.No.Member(touch));
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended });
            yield return null;

            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.right);
            yield return null;
            Assert.That(joystick.stick.ReadValue().x, Is.GreaterThan(.5f));
            Assert.That(player.currentControlScheme, Is.EqualTo(expectedScheme), "Generic joystick cannot take over input.");
            Assert.That(player.devices, Has.No.Member(joystick));
            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.zero);
            yield return null;

            var trigger = (AxisControl)xr["trigger"];
            InputSystem.QueueDeltaStateEvent(trigger, 1f);
            yield return null;
            Assert.That(trigger.ReadValue(), Is.GreaterThan(.5f));
            Assert.That(player.currentControlScheme, Is.EqualTo(expectedScheme), "XR cannot take over input.");
            Assert.That(player.devices, Has.No.Member(xr));
            InputSystem.QueueDeltaStateEvent(trigger, 0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CursorAndPromptsFollowDeliberateInputWithKeyboardMousePriority()
        {
            var scope = new TestInputScope();
            var owner = new GameObject("Control device tracker");
            owner.AddComponent<CursorManager>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Gamepad pad = null;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False);
                Assert.That(Cursor.visible, Is.True);

                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.5f, 0) });
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False, "A newly connected offset stick is its baseline.");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.53f, 0) });
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False, "Small drift does not switch prompts.");

                foreach (float offset in new[] { .58f, .64f, .70f })
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(offset, 0) });
                    yield return null;
                }
                Assert.That(ControlDeviceState.Gamepad, Is.True, "A deliberate gradual stick sweep must switch prompts even when each frame moves less than the drift threshold.");
                Assert.That(Cursor.visible, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.L));
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.53f, 0) });
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.53f, 0) }.WithButton(GamepadButton.South));
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.True);
                Assert.That(Cursor.visible, Is.False);
                Assert.That(InputPrompts.Interact, Is.EqualTo("A / Cross"));
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.53f, 0) });
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.True, "An unchanged held stick does not create new activity.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False, "Keyboard wins simultaneous activity.");
                Assert.That(Cursor.visible, Is.True);
                Assert.That(InputPrompts.Interact, Is.EqualTo("Enter"));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(20, 0) });
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False);
                Assert.That(Cursor.visible, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.True);
                InputSystem.RemoveDevice(pad);
                pad = null;
                yield return null;
                Assert.That(ControlDeviceState.Gamepad, Is.False, "Disconnecting the controller restores pointer visibility and keyboard prompts.");
                Assert.That(Cursor.visible, Is.True);
            }
            finally
            {
                Object.Destroy(owner);
                if (pad != null) InputSystem.RemoveDevice(pad);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                scope.Dispose();
            }
        }
    }
}
