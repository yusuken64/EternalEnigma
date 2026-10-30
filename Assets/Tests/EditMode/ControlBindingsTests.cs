using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace EternalEnigma.Tests
{
    public sealed class ControlBindingsTests
    {
        private IDisposable preferences;
        private DungeonControls live;

        [SetUp] public void SetUp()
        {
            preferences = GamePreferences.Isolate();
            ControlBindings.Reload();
            live = new DungeonControls();
            ControlBindings.ConfigureAdditionalActions(live.asset);
            ControlBindings.Register(live.asset);
        }

        [TearDown] public void TearDown()
        {
            ControlBindings.Unregister(live.asset);
            live.Disable();
            UnityEngine.Object.DestroyImmediate(live.asset);
            preferences.Dispose();
            ControlBindings.Reload();
        }

        private static int Binding(InputAction action, string path) =>
            Enumerable.Range(0, action.bindings.Count).Single(i => action.bindings[i].path == path);

        [Test] public void OverrideSynchronizesEnabledAssetsAndLinkedMenuActions()
        {
            live.Enable();
            var display = ControlBindings.DisplayAsset.FindAction("Player/Menu", true);
            int index = Binding(display, "<Keyboard>/q");
            ControlBindings.ApplyBinding("Player/Menu", index, "<Keyboard>/k");
            Assert.That(live.Player.Menu.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/k"));
            Assert.That(live.UI.OpenMenu.bindings[Binding(live.UI.OpenMenu, "<Keyboard>/q")].effectivePath,
                Is.EqualTo("<Keyboard>/k"), "Opening and closing the same menu must use the new binding.");
            Assert.That(live.Player.Menu.enabled, Is.True);
            Assert.That(live.UI.OpenMenu.enabled, Is.True);
            Assert.That(ControlBindings.BindingLabel(display, index), Does.Contain("K"));
        }

        [Test] public void CompositePartOverrideKeepsOtherDirectionsAndAlternateKeys()
        {
            var action = ControlBindings.DisplayAsset.FindAction("Player/Move", true);
            int up = Binding(action, "<Keyboard>/w");
            ControlBindings.ApplyBinding("Player/Move", up, "<Keyboard>/i");
            Assert.That(live.Player.Move.bindings[up].effectivePath, Is.EqualTo("<Keyboard>/i"));
            Assert.That(live.Player.Move.bindings[up].isPartOfComposite, Is.True);
            Assert.That(live.Player.Move.bindings[Binding(live.Player.Move, "<Keyboard>/upArrow")].effectivePath,
                Is.EqualTo("<Keyboard>/upArrow"));
            Assert.That(live.Player.Move.bindings[Binding(live.Player.Move, "<Keyboard>/s")].effectivePath,
                Is.EqualTo("<Keyboard>/s"));
        }

        [Test] public void SavedOverridesSurviveReloadAndNewDynamicActionIds()
        {
            var action = ControlBindings.DisplayAsset.FindAction("UI/OpenEquipment", true);
            ControlBindings.ApplyBinding("UI/OpenEquipment", Binding(action, "<Keyboard>/i"), "<Keyboard>/u");
            ControlBindings.Reload();
            var next = new DungeonControls();
            try
            {
                ControlBindings.ConfigureAdditionalActions(next.asset);
                ControlBindings.Register(next.asset);
                var restored = next.asset.FindAction("UI/OpenEquipment", true);
                Assert.That(restored.bindings[Binding(restored, "<Keyboard>/i")].effectivePath, Is.EqualTo("<Keyboard>/u"));
                Assert.That(GamePreferences.HasKey(ControlBindings.PreferenceKey), Is.True);
            }
            finally
            {
                ControlBindings.Unregister(next.asset);
                next.Disable();
                UnityEngine.Object.DestroyImmediate(next.asset);
            }
        }

        [Test] public void RestoreDefaultsClearsKeyboardGamepadAndLinkedOverrides()
        {
            var menu = ControlBindings.DisplayAsset.FindAction("Player/Menu", true);
            ControlBindings.ApplyBinding("Player/Menu", Binding(menu, "<Keyboard>/q"), "<Keyboard>/k");
            ControlBindings.ApplyBinding("Player/Menu", Binding(menu, "<Gamepad>/buttonWest"), "<Gamepad>/rightTrigger");
            ControlBindings.ResetAll();
            ControlBindings.Reload();
            foreach (var action in live.asset)
                Assert.That(action.bindings.Any(binding => binding.hasOverrides), Is.False, action.name);
            foreach (var action in ControlBindings.DisplayAsset)
                Assert.That(action.bindings.Any(binding => binding.hasOverrides), Is.False, action.name);
        }

        [Test] public void ChangedNotificationTracksBindingChangesAndPromptsUseTheNewKey()
        {
            int notifications = 0;
            bool originalDevice = ControlDeviceState.Gamepad;
            var setDevice = typeof(ControlDeviceState).GetMethod("Set",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            void Changed() => notifications++;
            ControlBindings.Changed += Changed;
            try
            {
                setDevice.Invoke(null, new object[] { false });
                var action = ControlBindings.DisplayAsset.FindAction("Player/Menu", true);
                ControlBindings.ApplyBinding("Player/Menu", Binding(action, "<Keyboard>/q"), "<Keyboard>/k");
                Assert.That(notifications, Is.GreaterThanOrEqualTo(1));
                Assert.That(InputPrompts.Inventory, Does.Contain("K"));
                Assert.That(InputPrompts.Inventory, Does.Not.Contain("Q"));
            }
            finally
            {
                ControlBindings.Changed -= Changed;
                setDevice.Invoke(null, new object[] { originalDevice });
            }
        }
    }
}
