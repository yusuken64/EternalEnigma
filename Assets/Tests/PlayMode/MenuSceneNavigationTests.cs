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
        private TestInputScope inputScope;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
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
            inputScope.Dispose();
        }

        private void UsePad()
        {
            pad = InputSystem.AddDevice<Gamepad>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard };
        }

        [UnityTest]
        public IEnumerator TestDungeonStartsWithoutASave() => CheckTestDungeon(null);

        [UnityTest]
        public IEnumerator TestDungeonStartsWithoutOverwritingExistingSave() =>
            CheckTestDungeon(new TestScenario { Gold = 731, StartFloor = 6, EndFloor = 10 }.CreateSave());

        private IEnumerator CheckTestDungeon(GameSaveData save)
        {
            yield return harness.LoadMainMenu(save);
            var savedJson = harness.Store.Read();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var names = menu.DebugAllies.Select(ally => ally.AllyName).ToArray();
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i =>
                    b.onClick.GetPersistentTarget(i) == menu &&
                    b.onClick.GetPersistentMethodName(i) == nameof(MainMenu.TestDungeon_Clicked)));
            button.onClick.Invoke();
            yield return harness.WaitForIdle();
            Assert.That(harness.Game.Allies.Select(ally => ally.CharacterName), Is.EqualTo(names));
            Assert.That(harness.Ally, Is.Not.Null);
            Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(1));
            Assert.That(Common.Instance.GameSaveData.DungeonSaveData.EndFloor, Is.EqualTo(5));
            Assert.That(harness.Store.Read(), Is.EqualTo(savedJson));
            var loadout = DemoDungeonLoadout.Load();
            Assert.That(loadout, Is.Not.Null);
            Assert.That(loadout.Skills.Count, Is.EqualTo(9));
            Assert.That(loadout.Skills.Select(s => s.Targeting).Distinct(),
                Is.EquivalentTo(System.Enum.GetValues(typeof(SkillTargeting))));
            Assert.That(loadout.Items.OfType<UsableItemDefinition>().Select(i => i.Targeting).Distinct(),
                Is.EquivalentTo(System.Enum.GetValues(typeof(SkillTargeting))));
            Assert.That(harness.Game.Enemies.Count, Is.EqualTo(3));
            Assert.That(harness.Game.Enemies.All(e => e.Vitals.HP == 500), Is.True);
            Assert.That(harness.Game.Enemies.Cast<Enemy>().All(e => e.Policies.Count == 0), Is.True);
            Assert.That(harness.Game.Allies.All(a => a.Vitals.HP < a.FinalStats.HPMax && a.Vitals.SP >= 50), Is.True);
            var inventory = harness.Game.PlayerController.Inventory;
            Assert.That(inventory.Count(), Is.EqualTo(loadout.Items.Count));
            foreach (var definition in loadout.Items)
                Assert.That(Common.Instance.ItemManager.GetAsInventoryItemByName(definition.ItemName).ItemDefinition, Is.SameAs(definition));
            foreach (var skill in loadout.Skills)
                Assert.That(Common.Instance.SkillManager.GetSkillByName(skill.SkillName), Is.SameAs(skill));
            if (save == null)
            {
                var weapon = inventory.InventoryItems.OfType<EquipableInventoryItem>().First();
                foreach (var skill in harness.Ally.Skills.Where(s => loadout.Skills.Any(d => d.SkillName == s.SkillName)))
                {
                    Assert.That(harness.Ally.CanCast(skill, out var reason), Is.True, skill.SkillName + ": " + reason);
                    var target = skill.TargetSelector.Team == TargetTeam.Enemies ? harness.Game.Enemies[0] : harness.Ally;
                    var action = skill.Targeting == SkillTargeting.InventoryItem ? SkillAction.ForInventoryItem(harness.Ally, skill, weapon) :
                        skill.Targeting == SkillTargeting.Missile ? SkillAction.ForMissile(harness.Ally, skill, Vector3Int.right) :
                        new SkillAction(harness.Ally, skill, target);
                    yield return harness.ExecuteAction(action);
                }
                foreach (var item in inventory.InventoryItems.OfType<UsableInventoryItem>().ToArray())
                {
                    var definition = (UsableItemDefinition)item.ItemDefinition;
                    var target = definition.TargetSelector.Team == TargetTeam.Enemies ? harness.Game.Enemies[0] : harness.Ally;
                    var action = new UseInventoryItemAction(inventory, harness.Ally, item).WithTarget(target)
                        .WithItem(weapon).WithDirection(Vector3Int.right);
                    Assert.That(action.IsValid(harness.Ally), Is.True, item.ItemName);
                    yield return harness.ExecuteAction(action);
                    Assert.That(item.StackStock, Is.EqualTo(19), item.ItemName);
                }
                Assert.That(harness.Store.Read(), Is.EqualTo(savedJson));
            }
            if (save != null) Assert.That(save.OverworldSaveData.Gold, Is.EqualTo(731));
            harness.Game.AdvanceFloor();
            yield return harness.WaitForIdle();
            Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(2));
            Assert.That(harness.Game.CurrentDungeon.IsThroneFloor, Is.False);
            Assert.That(harness.Game.Enemies, Is.Not.Empty);
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContinueKeepsOverworldCoveredUntilHeroCameraIsReady()
        {
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            Object.FindFirstObjectByType<MainMenu>().Continue_Clicked();
            yield return CheckOverworldReveal();
        }

        [UnityTest]
        public IEnumerator DungeonReturnKeepsOverworldCoveredUntilHeroCameraIsReady()
        {
            yield return harness.LoadDungeon(new TestScenario());
            GameOverScreen.GoBackToOverworld(false, harness.Game.PlayerController);
            yield return CheckOverworldReveal();
        }

        private IEnumerator CheckOverworldReveal()
        {
            var transition = Common.Instance.ScreenTransition;
            bool sawGeneration = false;
            float deadline = Time.realtimeSinceStartup + 60;
            Overworld world;
            while ((world = Object.FindFirstObjectByType<Overworld>()) == null || !world.IsReady)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Overworld did not become ready.");
                if (world != null)
                {
                    sawGeneration = true;
                    Assert.That(transition.BlockScreen.activeSelf, Is.True);
                    Assert.That(transition.ShutterScreen.gameObject.activeSelf, Is.True);
                    Assert.That(transition.ShutterScreen.color.a, Is.EqualTo(1).Within(0.001f));
                }
                yield return null;
            }
            Assert.That(sawGeneration, Is.True);
            var camera = world.OverworldPlayer.CameraController;
            Assert.That(camera._followTarget, Is.SameAs(world.OverworldPlayer.ControllingOverworldAlly.CirlcleRenderer.transform));
            Assert.That(Vector3.Distance(camera.Camera.transform.position, camera._followTarget.position + camera.CameraOffset), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(camera.Camera.transform.forward,
                (camera._followTarget.position - camera.Camera.transform.position).normalized), Is.GreaterThan(0.999f));
            yield return harness.WaitUntil(() => !transition.BlockScreen.activeSelf, "overworld reveal");
            Assert.That(transition.ShutterScreen.gameObject.activeSelf, Is.False);
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
