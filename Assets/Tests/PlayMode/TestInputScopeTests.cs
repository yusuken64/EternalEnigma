#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace EternalEnigma.Tests
{
    public sealed class TestInputScopeTests
    {
        [Test]
        public void AbandonedNestedScopesRestoreDevicesAndSettings()
        {
            var settings = InputSystem.settings;
            var existingMouse = InputSystem.AddDevice<Mouse>();
            var disabledPad = InputSystem.AddDevice<Gamepad>();
            InputSystem.DisableDevice(disabledPad);
            try
            {
                var outer = new TestInputScope();
                var testMouse = InputSystem.AddDevice<Mouse>();
                var inner = new TestInputScope();
                var testKeyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(existingMouse.enabled, Is.False);
                Assert.That(testMouse.enabled, Is.False);

                TestInputScope.RestoreAll();

                Assert.That(InputSystem.settings, Is.SameAs(settings));
                Assert.That(existingMouse.added && existingMouse.enabled, Is.True,
                    "The original mouse must receive input after an interrupted test.");
                Assert.That(disabledPad.added, Is.True);
                Assert.That(disabledPad.enabled, Is.False, "Preserve devices disabled before the tests.");
                Assert.That(testMouse.added || testKeyboard.added, Is.False,
                    "Simulated devices must not survive into normal Play Mode.");
                Assert.DoesNotThrow(() => { inner.Dispose(); outer.Dispose(); },
                    "Normal teardown may still run after emergency cleanup.");
            }
            finally
            {
                TestInputScope.RestoreAll();
                InputSystem.RemoveDevice(existingMouse);
                InputSystem.RemoveDevice(disabledPad);
            }
        }
    }
}
#endif
