using System.Reflection;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public sealed class InputPromptsTests
    {
        private static readonly MethodInfo SetDevice = typeof(ControlDeviceState).GetMethod("Set",
            BindingFlags.Static | BindingFlags.NonPublic);
        private bool original;

        [SetUp] public void SetUp() { original = ControlDeviceState.Gamepad; Set(false); }
        [TearDown] public void TearDown() => Set(original);
        private static void Set(bool value) => SetDevice.Invoke(null, new object[] { value });

        [Test]
        public void NamedPromptsAndKnownTokensUseOnlyTheCurrentDevice()
        {
            const string template = "{Move} | {Interact} | {Confirm} | {Back} | {ToggleFullControl} | {Unknown}";
            Assert.That(InputPrompts.Format(template), Is.EqualTo(
                "WASD / arrows | Enter | Enter / Space | Escape | F | {Unknown}"));
            Assert.That(InputPrompts.Inventory, Is.EqualTo("Q"));
            Assert.That(InputPrompts.Skills, Is.EqualTo("R"));
            Set(true);
            Assert.That(InputPrompts.Format(template), Is.EqualTo(
                "Left stick / D-pad | A / Cross | A / Cross | B / Circle | Right stick (press) | {Unknown}"));
            Assert.That(InputPrompts.Inventory, Is.EqualTo("X / Square"));
            Assert.That(InputPrompts.Skills, Is.EqualTo("LB / L1"));
            Assert.That(InputPrompts.Format(null), Is.Null);
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
