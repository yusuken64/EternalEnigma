#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    public sealed class ChaosInputTests
    {
        ChaosInputDevices devices;
        [TearDown] public void Cleanup() { devices?.Dispose(); devices = null; }
        [UnityTest] public IEnumerator VirtualDevicesDeliverHoldsOpposingInputAndCleanup()
        {
            var settings = InputSystem.settings;
            var physical = InputSystem.AddDevice<Gamepad>(); bool enabled = physical.enabled;
            try
            {
                devices = new ChaosInputDevices();
                devices.Send(new ChaosInput { Keys = new[] { "UpArrow", "DownArrow" }, Buttons = new[] { "South" }, Stick = Vector2.right });
                yield return null; yield return null;
                Assert.That(devices.Keyboard.upArrowKey.isPressed && devices.Keyboard.downArrowKey.isPressed, Is.True);
                Assert.That(devices.Pad.buttonSouth.isPressed, Is.True);
                Assert.That(physical.enabled, Is.False);
                devices.Release(); yield return null; yield return null;
                Assert.That(devices.Keyboard.anyKey.isPressed, Is.False); Assert.That(devices.Pad.buttonSouth.isPressed, Is.False);
                devices.Dispose();
                Assert.That(InputSystem.settings, Is.SameAs(settings)); Assert.That(physical.enabled, Is.EqualTo(enabled));
                Assert.That(devices.Keyboard.added || devices.Mouse.added || devices.Pad.added, Is.False);
            }
            finally { InputSystem.RemoveDevice(physical); }
        }
        [UnityTest] public IEnumerator DeviceSwitchingUsesProductionControlDevicePolling()
        {
            devices = new ChaosInputDevices();
            devices.Send(ChaosInput.Pad(GamepadButton.South)); yield return null; ControlDeviceState.Poll();
            Assert.That(ControlDeviceState.Gamepad, Is.True);
            devices.Release(); yield return null;
            devices.Send(ChaosInput.Key(Key.K)); yield return null; ControlDeviceState.Poll();
            Assert.That(ControlDeviceState.Gamepad, Is.False);
        }
    }
}
#endif
