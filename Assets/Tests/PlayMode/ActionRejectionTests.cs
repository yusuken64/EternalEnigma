#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class ActionRejectionTests
    {
        private GameTestHarness harness;
        private TestInputScope input;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private readonly List<Object> assets = new();
        private Ally Hero => harness.Ally;
        private Inventory Bag => harness.Game.PlayerController.Inventory;
        private MenuManager Menus => MenuManager.Instance;
        private string LastMessage => Object.FindFirstObjectByType<GameMessages>().History.Last();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            Hero.BaseStats.ManaMax = 30;
            Hero.BaseStats.ManaRegenThreshold = 10000;
            Hero.InvalidateCachedStats();
            Hero.Vitals.Mana = 20;
            Hero.SyncDisplayedStats();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, gamepad };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            yield return harness.Cleanup();
            input.Dispose();
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }

        private EquipableInventoryItem Weapon(string name, WeaponType type = WeaponType.BowAndArrow, EquipmentSlot slot = EquipmentSlot.TwoHand)
        {
            var definition = Asset<EquipmentItemDefinition>();
            definition.ItemName = name; definition.WeaponType = type; definition.EquipmentSlot = slot;
            definition.StatModification = new StatModification();
            var item = new EquipableInventoryItem(definition); Bag.Add(item); return item;
        }

        private Skill Inspection()
        {
            var skill = Asset<Skill>(); skill.SkillName = "Rejection test inspection";
            skill.ManaCost = 2; skill.Targeting = SkillTargeting.InventoryItem;
            skill.InventoryTargetSelector = new InventoryTargetSelector { ItemType = InventoryTargetType.Weapon };
            skill.ActionEffects = new List<GameAction> { new InspectInventoryItemEffect() };
            Hero.Skills.Clear(); Hero.Skills.Add(skill); return skill;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
        }

        [UnityTest]
        public IEnumerator RestrictedEquipmentCanBeAttemptedFromInventoryAndEquipmentAndRetried()
        {
            var primary = Asset<ClassDefinition>(); primary.Id = "mage-test"; primary.DisplayName = "Test Mage";
            primary.AllowedWeapons = new List<WeaponType> { WeaponType.MagicWand };
            Hero.PrimaryClass = primary; Hero.SecondaryClass = null;
            var bow = Weapon("Restricted bow");
            int actions = Hero.Vitals.ActionsPerTurnLeft;
            Menus.OpenPartyMenu(PartyMenuTab.Inventory); yield return null;
            Menus.PartyMenu.EntryButtons[0].onClick.Invoke(); yield return null;
            var dialog = Menus.ActionDialog;
            Assert.That(dialog.Buttons.Take(3).All(b => b.interactable), Is.True);
            var equip = dialog.Buttons[0]; equip.Select(); yield return Press(Key.Enter);
            Assert.That(Menus.CurrentDialog, Is.SameAs(dialog));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(equip.gameObject));
            Assert.That(LastMessage, Does.Contain("bows and arrows proficiency").And.Contain("Test Mage"));
            Assert.That(Hero.Equipment.IsEquipped(bow), Is.False);
            Assert.That(Hero.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);

            dialog.CloseDialog(); Menus.PartyMenu.SwitchTab(PartyMenuTab.Equipment);
            Menus.PartyMenu.EntryButtons[0].onClick.Invoke(); yield return null;
            var picker = (PartyMenuPicker)Menus.CurrentDialog;
            var row = picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(b => b.Label.text == bow.DisplayName);
            Assert.That(row.Button.interactable, Is.True);
            row.Button.Select(); yield return Press(GamepadButton.South);
            Assert.That(Menus.CurrentDialog, Is.SameAs(picker));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));
            Assert.That(LastMessage, Does.Contain("bows and arrows proficiency"));

            var secondary = Asset<ClassDefinition>(); secondary.Id = "archer-test"; secondary.DisplayName = "Test Archer";
            secondary.AllowedWeapons = new List<WeaponType> { WeaponType.BowAndArrow };
            Hero.SecondaryClass = secondary;
            row.Button.onClick.Invoke(); yield return null; yield return harness.WaitForIdle();
            Assert.That(Hero.Equipment.IsEquipped(bow), Is.True, "Eligibility is rechecked without rebuilding the picker.");
            Assert.That(Menus.Opened, Is.False);
        }

        [UnityTest]
        public IEnumerator AmmunitionReportsBowProficiencyAndEquippedItemsRejectDropAndThrow()
        {
            var primary = Asset<ClassDefinition>(); primary.Id = "mage-test"; primary.DisplayName = "Test Mage";
            Hero.PrimaryClass = primary; Hero.SecondaryClass = null;
            var arrows = Weapon("Restricted arrows", WeaponType.SimpleWeapon, EquipmentSlot.OffHand);
            arrows.EquipmentItemDefinition.IsAmmunition = true;
            var use = new UseInventoryItemAction(Bag, Hero, arrows);
            Assert.That(use.CanBegin(Hero, out var reason), Is.False);
            Assert.That(reason, Does.Contain("bows and arrows proficiency"));
            Assert.That(reason, Does.Not.Contain("Simple Weapon"));

            Menus.OpenPartyMenu(PartyMenuTab.Inventory); yield return null;
            Menus.PartyMenu.EntryButtons[0].onClick.Invoke(); yield return null;
            var dialog = Menus.ActionDialog;
            // Equipping moves the item out of the shared bag. Keep the previously opened
            // item dialog to exercise the stale-state guard on Throw and Drop.
            Hero.Equipment.Equip(arrows);
            int actions = Hero.Vitals.ActionsPerTurnLeft;
            foreach (int index in new[] { 1, 2 })
            {
                var button = dialog.Buttons[index]; Assert.That(button.interactable, Is.True);
                button.Select(); button.onClick.Invoke(); yield return null;
                Assert.That(Menus.CurrentDialog, Is.SameAs(dialog));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(button.gameObject));
                Assert.That(LastMessage, Does.Contain("Unequip this item"));
                Assert.That(Hero.Equipment.IsEquipped(arrows), Is.True);
                Assert.That(Bag.InventoryItems, Has.No.Member(arrows));
                Assert.That(Hero.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
                Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ManaAndMissingTargetsExplainFailureAndStaleInventorySelectionCanRetry()
        {
            Inspection(); Hero.Vitals.Mana = 0; Hero.SyncDisplayedStats();
            Menus.OpenPartyMenu(PartyMenuTab.Skills); yield return null;
            var cast = Menus.PartyMenu.EntryButtons[0]; cast.Select(); yield return Press(GamepadButton.South);
            Assert.That(Menus.CurrentDialog, Is.SameAs(Menus.PartyMenu));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(cast.gameObject));
            Assert.That(LastMessage, Does.Contain("2 Mana").And.Contain("have 0"));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Hero.Vitals.Mana = 20; Hero.SyncDisplayedStats();
            yield return Press(Key.Enter);
            Assert.That(Menus.CurrentDialog, Is.SameAs(Menus.PartyMenu));
            Assert.That(LastMessage, Does.Contain("inventory"));

            var target = Weapon("Inspection target");
            cast.onClick.Invoke(); yield return null;
            var picker = (PartyMenuPicker)Menus.CurrentDialog;
            var row = picker.Rows.GetComponentsInChildren<AuthoredButton>().Single();
            row.Button.Select(); Bag.Remove(target); row.Button.onClick.Invoke(); yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(picker));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));
            Assert.That(LastMessage, Does.Contain("inventory"));
            Assert.That(Hero.Vitals.Mana, Is.EqualTo(20));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Bag.Add(target); Hero.Vitals.Mana = 0;
            row.Button.onClick.Invoke(); yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(picker));
            Assert.That(LastMessage, Does.Contain("2 Mana").And.Contain("have 0"));
            Hero.Vitals.Mana = 20; row.Button.onClick.Invoke(); yield return null; yield return harness.WaitForIdle();
            Assert.That(Menus.Opened, Is.False);
            Assert.That(Hero.Vitals.Mana, Is.EqualTo(18));
        }

        [UnityTest]
        public IEnumerator ChargingRejectsNewMenuCommandsAndKeepsTheCurrentPicker()
        {
            var skill = Inspection(); var target = Weapon("Inspection target");
            var mobile = Asset<Skill>(); mobile.SkillName = "Mobile Casting"; mobile.ActivationType = ActivationType.Passive;
            Hero.Skills.Add(mobile);
            Menus.OpenPartyMenu(PartyMenuTab.Skills); yield return null;
            var cast = Menus.PartyMenu.EntryButtons[0];
            var pending = new PendingCast(SkillAction.ForInventoryItem(Hero, skill, target), "Existing cast", 3, skill);
            Hero.PendingCast = pending;
            cast.Select(); cast.onClick.Invoke(); yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(Menus.PartyMenu));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(cast.gameObject));
            Assert.That(LastMessage, Does.Contain("Finish charging Existing cast"));
            Hero.PendingCast = null;
            cast.onClick.Invoke(); yield return null;
            var picker = (PartyMenuPicker)Menus.CurrentDialog;
            var row = picker.Rows.GetComponentsInChildren<AuthoredButton>().Single();
            Hero.PendingCast = pending;
            row.Button.Select(); row.Button.onClick.Invoke(); yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(picker));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));
            Assert.That(LastMessage, Does.Contain("Finish charging Existing cast"));
            picker.CloseDialog(); Menus.PartyMenu.SwitchTab(PartyMenuTab.Inventory);
            Menus.PartyMenu.EntryButtons[0].onClick.Invoke(); yield return null;
            var dialog = Menus.ActionDialog;
            foreach (var button in dialog.Buttons.Take(3))
            {
                button.Select(); button.onClick.Invoke(); yield return null;
                Assert.That(Menus.CurrentDialog, Is.SameAs(dialog));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(button.gameObject));
                Assert.That(LastMessage, Does.Contain("Finish charging Existing cast"));
            }
            Assert.That(Hero.PendingCast, Is.SameAs(pending));
            Assert.That(pending.Remaining, Is.EqualTo(2));
            Assert.That(Hero.Vitals.Mana, Is.EqualTo(20));
            Assert.That(Bag.InventoryItems, Does.Contain(target));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Hero.PendingCast = null;
        }

        [UnityTest]
        public IEnumerator ItemUseRejectsMissingInventoryTargetsWithoutConsumingItsStock()
        {
            var definition = Asset<UsableItemDefinition>(); definition.ItemName = "Inspection scroll";
            definition.Targeting = SkillTargeting.InventoryItem; definition.StackMax = 10;
            definition.InventoryTargetSelector.ItemType = InventoryTargetType.Weapon;
            definition.InventoryEffects.Add(new InspectInventoryItemEffect());
            var item = definition.AsInventoryItem(2); Bag.Add(item);
            Menus.OpenPartyMenu(PartyMenuTab.Inventory); yield return null;
            Menus.PartyMenu.EntryButtons[0].onClick.Invoke(); yield return null;
            var dialog = Menus.ActionDialog;
            var use = dialog.Buttons[0]; Assert.That(use.interactable, Is.True);
            use.Select(); use.onClick.Invoke(); yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(dialog));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(use.gameObject));
            Assert.That(LastMessage, Does.Contain("No valid inventory items"));
            Assert.That(item.StackStock, Is.EqualTo(2));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Bag.Remove(item);
            Assert.That(new UseInventoryItemAction(Bag, Hero, item).CanBegin(Hero, out var reason), Is.False);
            Assert.That(reason, Does.Contain("no longer in your inventory"));
        }
    }
}
#endif
