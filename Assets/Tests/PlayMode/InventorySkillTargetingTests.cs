#if UNITY_EDITOR
using System;
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
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class InventorySkillTargetingTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Keyboard keyboard;
        private Gamepad pad;
        private Mouse mouse;
        private readonly List<Object> assets = new();
        private Ally caster => harness.Ally;
        private Inventory bag => harness.Game.PlayerController.Inventory;
        private Skill skill;
        private MarkItemEffect effect;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            caster.BaseStats.SPMax = 30;
            caster.BaseStats.SPRegenAcccumlateThreshold = 10000;
            caster.InvalidateCachedStats();
            caster.Vitals.SP = 20;
            caster.SyncDisplayedStats();
            skill = ScriptableObject.CreateInstance<Skill>();
            assets.Add(skill);
            skill.SkillName = "Item targeting test";
            skill.SPCost = 2;
            skill.Targeting = SkillTargeting.InventoryItem;
            skill.TargetSelector = null; // Inventory skills do not need world-target settings.
            effect = new MarkItemEffect();
            skill.ActionEffects = new List<GameAction> { effect };
            caster.Skills.Add(skill);
            keyboard = InputSystem.AddDevice<Keyboard>();
            pad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { keyboard, pad, mouse };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            try { yield return harness.Cleanup(); }
            finally
            {
                foreach (var asset in assets) Object.DestroyImmediate(asset);
                assets.Clear();
                inputScope.Dispose();
            }
        }

        private EquipableInventoryItem AddEquipment(EquipmentSlot slot, WeaponType weaponType = WeaponType.SingleSword)
        {
            var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            definition.ItemName = "Duplicate name";
            definition.EquipmentSlot = slot;
            definition.WeaponType = weaponType;
            definition.StatModification = new StatModification();
            assets.Add(definition);
            var item = (EquipableInventoryItem)definition.AsInventoryItem(null);
            bag.Add(item);
            return item;
        }

        private InventoryItem AddUsable()
        {
            var definition = ScriptableObject.CreateInstance<UsableItemDefinition>();
            definition.ItemName = "Potion";
            assets.Add(definition);
            var item = definition.AsInventoryItem(null);
            bag.Add(item);
            return item;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiltersIncludeOnlyEligibleBagItemsAndOptionallyCasterEquipment()
        {
            var sword = AddEquipment(EquipmentSlot.MainHand);
            var twoHand = AddEquipment(EquipmentSlot.TwoHand);
            var offhandSword = AddEquipment(EquipmentSlot.OffHand, WeaponType.OffhandSword);
            var shield = AddEquipment(EquipmentSlot.OffHand, WeaponType.OffhandShield);
            var accessory = AddEquipment(EquipmentSlot.Accessory);
            var potion = AddUsable();
            Assert.That(skill.GetInventoryTargets(caster).Count, Is.EqualTo(6));
            skill.InventoryTargetSelector.ItemType = InventoryTargetType.Equipment;
            Assert.That(skill.GetInventoryTargets(caster), Is.EquivalentTo(new[] { sword, twoHand, offhandSword, shield, accessory }));
            skill.InventoryTargetSelector.ItemType = InventoryTargetType.Weapon;
            Assert.That(skill.GetInventoryTargets(caster), Is.EquivalentTo(new[] { sword, twoHand, offhandSword }));
            caster.Equipment.Equip(sword);
            bag.Remove(sword);
            Assert.That(skill.GetInventoryTargets(caster), Has.No.Member(sword));
            skill.InventoryTargetSelector.IncludeEquipped = true;
            Assert.That(skill.GetInventoryTargets(caster), Has.Member(sword));
            effect.Marked.Add(twoHand); // Identify-style effects can exclude already-processed items.
            Assert.That(skill.GetInventoryTargets(caster), Has.No.Member(twoHand));
            Assert.That(SkillAction.ForInventoryItem(caster, skill, potion).IsValid(caster), Is.False);
            Assert.That(skill.GetTargetCharacters(caster), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelReopenAndConfirmTargetTheExactDuplicateThenNormalInventoryWorks()
        {
            var first = AddEquipment(EquipmentSlot.MainHand);
            var second = first.ItemDefinition.AsInventoryItem(null);
            bag.Add(second);
            yield return Press(Key.R);
            var skillButton = EventSystem.current.currentSelectedGameObject;
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.SameAs(harness.Game.InventoryMenu));
            Assert.That(harness.Game.InventoryMenu.InventoryMenuItems.Count, Is.EqualTo(2));
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Escape);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(skillButton));
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            Assert.That(effect.Marked, Is.Empty);
            yield return Press(Key.Enter);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            yield return harness.WaitForIdle();
            Assert.That(effect.Marked, Is.EquivalentTo(new[] { second }));
            Assert.That(caster.Vitals.SP, Is.EqualTo(18));
            Assert.That(caster.DisplayedVitals.SP, Is.EqualTo(18));
            Assert.That(bag.InventoryItems, Is.EqualTo(new[] { first, second }));
            Assert.That(MenuManager.Instance.Opened, Is.False);
            yield return Press(Key.Q);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.DialogStack.Peek(), Is.SameAs(harness.Game.InventoryMenu.ActionDialog));
            Assert.That(effect.Marked.Count, Is.EqualTo(1), "Ordinary item actions must replace the targeting callback.");
            yield return Press(Key.Q);
        }

        [UnityTest]
        public IEnumerator WeaponFilterCanBeConfirmedWithMouseAfterOpeningWithGamepad()
        {
            AddUsable();
            AddEquipment(EquipmentSlot.OffHand, WeaponType.OffhandShield);
            var sword = AddEquipment(EquipmentSlot.MainHand);
            skill.InventoryTargetSelector.ItemType = InventoryTargetType.Weapon;
            var pickerCanvas = harness.Game.InventoryMenu.GetComponent<Canvas>();
            int normalOrder = pickerCanvas.sortingOrder;
            yield return Press(GamepadButton.LeftShoulder);
            yield return Press(GamepadButton.South);
            Assert.That(harness.Game.InventoryMenu.InventoryMenuItems.Count, Is.EqualTo(1));
            var row = harness.Game.InventoryMenu.InventoryMenuItems[0];
            var canvas = row.GetComponentInParent<Canvas>();
            var position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                row.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return harness.WaitForIdle();
            Assert.That(effect.Marked, Is.EquivalentTo(new[] { sword }));
            Assert.That(caster.Vitals.SP, Is.EqualTo(18));
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(pickerCanvas.sortingOrder, Is.EqualTo(normalOrder));
        }

        [UnityTest]
        public IEnumerator EmptyIneligibleRemovedAndUnaffordableTargetsCannotSpendSp()
        {
            Assert.That(caster.CanCast(skill, out _), Is.False);
            var item = AddEquipment(EquipmentSlot.MainHand);
            var queued = SkillAction.ForInventoryItem(caster, skill, item);
            Assert.That(queued.IsValid(caster), Is.True);
            var replacement = item.ItemDefinition.AsInventoryItem(null);
            bag.Remove(item); bag.Add(replacement);
            Assert.That(queued.ExecuteImmediate(caster), Is.Empty, "A same-named replacement is not the selected item.");
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            bag.Add(item);
            effect.Marked.Add(item);
            Assert.That(queued.ExecuteImmediate(caster), Is.Empty, "Eligibility must be rechecked before execution.");
            effect.Marked.Clear();
            caster.Vitals.SP = 1; caster.SyncDisplayedStats();
            yield return Press(Key.R);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.SameAs(harness.Game.SkillDialog));
            Assert.That(queued.ExecuteImmediate(caster), Is.Empty);
            Assert.That(caster.Vitals.SP, Is.EqualTo(1));
            Assert.That(effect.Marked, Is.Empty);
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            caster.Vitals.SP = 20; caster.SyncDisplayedStats();
            skill.ActionEffects = new List<GameAction> { new TakeDamageAction { damage = 5 } };
            Assert.That(caster.CanCast(skill, out _), Is.False, "Character effects must not be dispatched to inventory items.");
            yield return Press(Key.Escape);
        }

        // Test effect records per-item identification without introducing an identification
        // or enchantment system into the game's existing item/save model.
        [Serializable]
        private sealed class MarkItemEffect : InventorySkillEffect
        {
            public readonly HashSet<InventoryItem> Marked = new();
            internal override bool CanTarget(Character actor, InventoryItem item) => base.CanTarget(actor, item) && !Marked.Contains(item);
            internal override GameAction Bind(Character actor, InventoryItem item) => new DynamicGameAction(
                _ => { Marked.Add(item); return new List<GameAction>(); }, null, () => true);
        }
    }
}
#endif
