using System.Reflection;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public sealed class InputPromptsTests
    {
        private static readonly MethodInfo SetDevice = typeof(ControlDeviceState).GetMethod("Set",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[]{typeof(bool)}, null);
        private bool original;
        private System.IDisposable preferences;

        [SetUp] public void SetUp()
        {
            preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            original = ControlDeviceState.Gamepad;
            Set(false);
        }
        [TearDown] public void TearDown()
        {
            Set(original);
            preferences.Dispose();
            ControlBindings.Reload();
        }
        private static void Set(bool value) => SetDevice.Invoke(null, new object[] { value });

        [Test]
        public void NamedPromptsAndKnownTokensUseOnlyTheCurrentDevice()
        {
            const string template = "{Move} | {Interact} | {Confirm} | {Back} | {ToggleFullControl} | {Unknown}";
            var keyboard = InputPrompts.Format(template);
            Assert.That(keyboard, Does.Contain("W / Up").And.Contain("S / Down").And.Contain("A / Left").And.Contain("D / Right"));
            Assert.That(keyboard, Does.Contain("Enter").And.Contain("Escape").And.Contain(" | F | {Unknown}"));
            Assert.That(keyboard, Does.Not.Contain("Cross").And.Not.Contain("stick"));
            Assert.That(InputPrompts.Inventory, Is.EqualTo("Q"));
            Assert.That(InputPrompts.Skills, Is.EqualTo("R"));
            Set(true);
            var gamepad = InputPrompts.Format(template);
            Assert.That(gamepad, Does.Contain("Left stick").And.Contain("D-pad").And.Contain("A / Cross").And.Contain("B / Circle"));
            Assert.That(gamepad, Does.Contain("Right stick press | {Unknown}"));
            Assert.That(gamepad, Does.Not.Contain("Enter").And.Not.Contain("Escape"));
            Assert.That(InputPrompts.Inventory, Is.EqualTo("X / Square"));
            Assert.That(InputPrompts.Skills, Is.EqualTo("B / Circle"));
            Assert.That(InputPrompts.Format(null), Is.Empty);
        }

        [Test]
        public void ChangedFiresOnlyWhenTheDeviceChanges()
        {
            int changes = 0;
            void OnChanged() => changes++;
            ControlDeviceState.Changed += OnChanged;
            try
            {
                Set(false);
                Set(true);
                Set(true);
                Set(false);
                Set(false);
                Assert.That(changes, Is.EqualTo(2));
            }
            finally { ControlDeviceState.Changed -= OnChanged; }
        }
    }
}
