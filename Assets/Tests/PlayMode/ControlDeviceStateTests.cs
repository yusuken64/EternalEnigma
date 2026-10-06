using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    public sealed class ControlDeviceStateTests
    {
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
