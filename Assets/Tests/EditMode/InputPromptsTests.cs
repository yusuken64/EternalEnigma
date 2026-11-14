using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

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
            Assert.That(gamepad, Does.Contain("Left stick").And.Contain("D-pad").And.Contain(" | A | A | B | "));
            Assert.That(gamepad, Does.Contain("RS | {Unknown}"));
            Assert.That(gamepad, Does.Not.Contain("Enter").And.Not.Contain("Escape"));
            Assert.That(InputPrompts.Inventory, Is.EqualTo("X"));
            Assert.That(InputPrompts.Skills, Is.EqualTo("B"));
            Assert.That(InputPrompts.NextHero, Is.EqualTo("RB"));
            Assert.That(InputPrompts.PreviousHero, Is.EqualTo("LT"));
            Assert.That(InputPrompts.PresentationKey, Is.EqualTo("View"));
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

        [Test]
        public void InteractionButtonUsesOneGameplayBindingAndRestoresSavedOverrides()
        {
            Assert.That(InputPrompts.InteractionButton, Is.EqualTo("Space"));
            var action = ControlBindings.DisplayAsset.FindAction("Player/Attack", true);
            int binding = Enumerable.Range(0, action.bindings.Count).Single(i => action.bindings[i].path == "<Keyboard>/space");
            ControlBindings.ApplyBinding("Player/Attack", binding, "<Keyboard>/k");
            Assert.That(InputPrompts.InteractionButton, Is.EqualTo("K"));
            Assert.That(InputPrompts.Confirm, Does.Contain("Space"), "Menu confirmation is a separate action.");
            ControlBindings.Reload();
            Assert.That(InputPrompts.InteractionButton, Is.EqualTo("K"));
            ControlBindings.ApplyBinding("Player/Attack", binding, "<Mouse>/rightButton");
            Assert.That(InputPrompts.InteractionButton, Does.Contain("Right").Or.Contain("RMB"));
            ControlBindings.ResetAll();
            Assert.That(InputPrompts.InteractionButton, Is.EqualTo("Space"));
        }

        [Test]
        public void InteractionButtonUsesCurrentControllerLabelsAndReboundButtons()
        {
            var previous = Gamepad.current;
            var xbox = InputSystem.AddDevice<Gamepad>();
            var playstation = InputSystem.AddDevice<DualShockGamepad>();
            try
            {
                Set(true);
                xbox.MakeCurrent();
                Assert.That(InputPrompts.InteractionButton, Is.EqualTo("A"));
                playstation.MakeCurrent();
                Assert.That(InputPrompts.InteractionButton, Is.EqualTo("Cross"));
                var action = ControlBindings.DisplayAsset.FindAction("Player/Attack", true);
                int binding = Enumerable.Range(0, action.bindings.Count).Single(i => action.bindings[i].path == "<Gamepad>/buttonSouth");
                ControlBindings.ApplyBinding("Player/Attack", binding, "<Gamepad>/rightShoulder");
                Assert.That(InputPrompts.InteractionButton, Is.EqualTo("R1"));
                xbox.MakeCurrent();
                Assert.That(InputPrompts.InteractionButton, Is.EqualTo("RB"));
                Set(false);
                Assert.That(InputPrompts.InteractionButton, Is.EqualTo("Space"));
            }
            finally
            {
                InputSystem.RemoveDevice(playstation);
                InputSystem.RemoveDevice(xbox);
                previous?.MakeCurrent();
            }
        }
    }
}
