#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class MenuSceneNavigationTests
    {
        private GameTestHarness harness;
        private Gamepad pad;
        private Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            yield return harness.Cleanup();
        }

        private void UsePad()
        {
            pad = InputSystem.AddDevice<Gamepad>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard };
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsKeepCategoryFocusAndBackReturnsToGameplay()
        {
            yield return harness.LoadOverworld(new TestScenario().CreateSave());
            UsePad();
            var settings = Common.Instance.GlobalSettings;
            settings.ShowDialog();
            yield return null;
            var category = settings.TabGroup.TabContents.First(t =>
                t.Content.GetComponentsInChildren<Selectable>(true).Any(s => s.interactable));
            category.TabButton.Select();
            yield return Press(GamepadButton.South);
            Assert.That(settings.TabGroup.SelectedTab, Is.SameAs(category));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(category.TabButton.gameObject));
            yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(category.Content.transform), Is.True);
            Time.timeScale = 0;
            yield return Press(GamepadButton.East);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(category.TabButton.gameObject));
            yield return Press(GamepadButton.East);
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(Common.Instance.MenuInputHandler.PlayerInput.currentActionMap.name, Is.EqualTo("Player"));
        }

        [UnityTest]
        public IEnumerator InventoryCanOpenAndCloseImmediatelyAndSettingsRestoreItsSelection()
        {
            yield return harness.LoadDungeon(new TestScenario());
            UsePad();
            var definition = Common.Instance.ItemManager.ItemDefinitions.First(d => d is EquipmentItemDefinition);
            harness.AddItem(definition.ItemName);
            yield return Press(GamepadButton.West);
            Assert.That(MenuManager.Instance.Opened, Is.True);
            var inventorySelection = EventSystem.current.currentSelectedGameObject;
            Assert.That(inventorySelection.GetComponent<InventoryMenuItem>(), Is.Not.Null);
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(MenuManager.Instance.Opened, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(inventorySelection));
            yield return Press(GamepadButton.East);
            Assert.That(MenuManager.Instance.Opened, Is.False);
            yield return Press(GamepadButton.West);
            Assert.That(MenuManager.Instance.Opened, Is.True, "Fresh presses must not be discarded by a 200ms cooldown.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False, "Escape is Back inside a menu, not another settings opener.");
        }
    }
}
#endif
