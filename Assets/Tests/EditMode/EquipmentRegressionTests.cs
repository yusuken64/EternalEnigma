using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class EquipmentRegressionTests
{
    private GameObject owner;
    private Equipment equipment;
    private readonly List<EquipmentItemDefinition> definitions = new();
    [SetUp] public void SetUp() { owner = new GameObject(); equipment = owner.AddComponent<Equipment>(); }
    [TearDown] public void TearDown()
    {
        Object.DestroyImmediate(owner);
        foreach (var definition in definitions) Object.DestroyImmediate(definition);
        definitions.Clear();
    }
    private EquipableInventoryItem Item(EquipmentSlot slot)
    {
        var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        definition.EquipmentSlot = slot;
        definitions.Add(definition);
        return new EquipableInventoryItem(definition);
    }

    [Test]
    public void BowAndShieldConflictsAreSymmetricAndPreviewMatches()
    {
        var bow = Item(EquipmentSlot.MainHand); bow.EquipmentItemDefinition.WeaponType = WeaponType.BowAndArrow;
        bow.EquipmentItemDefinition.StatModification = new StatModification { Strength = 10 };
        var shield = Item(EquipmentSlot.OffHand); shield.EquipmentItemDefinition.StatModification = new StatModification { Defense = 3 };
        var bag = new List<InventoryItem> { bow, shield };
        EquipmentTransferService.Toggle(equipment,bag,shield,null,null,out _);
        Assert.That(equipment.GetStatsIfEquipped(bow).Defense, Is.Zero);
        EquipmentTransferService.Toggle(equipment,bag,bow,null,null,out _);
        Assert.That(equipment.EquippedShield, Is.Null); Assert.That(bag,Does.Contain(shield));
        Assert.That(equipment.GetStatsIfEquipped(shield).Strength, Is.Zero);
        EquipmentTransferService.Toggle(equipment,bag,shield,null,null,out _);
        Assert.That(equipment.EquippedWeapon, Is.Null); Assert.That(bag,Does.Contain(bow));
        Assert.That(equipment.GetEquipmentStatModification().Defense, Is.EqualTo(3));
    }
    [Test]
    public void BowRetainsArrowStackInEitherEquipOrder()
    {
        var bow = Item(EquipmentSlot.MainHand); bow.EquipmentItemDefinition.WeaponType = WeaponType.BowAndArrow;
        var arrows = Item(EquipmentSlot.OffHand); arrows.EquipmentItemDefinition.IsAmmunition = true; arrows.EquipmentItemDefinition.StackMax = 20; arrows.StackStock = 7;
        equipment.Equip(arrows); equipment.Equip(bow);
        Assert.That(equipment.EquippedShield, Is.SameAs(arrows));
        equipment.UnEquip(arrows); equipment.Equip(arrows);
        Assert.That(equipment.EquippedWeapon, Is.SameAs(bow)); Assert.That(arrows.StackStock,Is.EqualTo(7));
    }

    [TestCase(WeaponType.SimpleWeapon)]
    [TestCase(WeaponType.Axe)]
    [TestCase(WeaponType.Hammer)]
    public void ReclassifiedOffhandsAreWeaponTargetsAndReplaceTwoHanders(WeaponType type)
    {
        var offhand = Item(EquipmentSlot.OffHand);
        offhand.EquipmentItemDefinition.WeaponType = type;
        var twoHand = Item(EquipmentSlot.TwoHand);
        var selector = new InventoryTargetSelector { ItemType = InventoryTargetType.Weapon };
        Assert.That(selector.Matches(offhand), Is.True);
        equipment.Equip(twoHand);
        equipment.Equip(offhand);
        Assert.That(equipment.EquippedWeapon, Is.Null);
        Assert.That(equipment.EquippedShield, Is.SameAs(offhand));
        equipment.Equip(twoHand);
        Assert.That(equipment.EquippedShield, Is.Null);
        Assert.That(equipment.EquippedWeapon, Is.SameAs(twoHand));
    }

    [Test]
    public void OffhandPreviewAndEquipHandleSerializedEmptySlots()
    {
        JsonUtility.FromJsonOverwrite("{\"EquippedWeapon\":{\"ItemDefinition\":null},\"EquippedShield\":{\"ItemDefinition\":null},\"EquippedAccessory\":{\"ItemDefinition\":null}}", equipment);
        Assert.That(equipment.EquippedWeapon, Is.Not.Null, "Unity represents inline empty slots as objects.");
        Assert.That(equipment.EquippedWeapon.ItemDefinition, Is.Null);
        var shield = Item(EquipmentSlot.OffHand);
        Assert.DoesNotThrow(() => equipment.GetStatsIfEquipped(shield));
        Assert.DoesNotThrow(() => equipment.Equip(shield));
        Assert.That(equipment.EquippedShield, Is.SameAs(shield));
        Assert.That(equipment.EquippedWeapon, Is.Null);
    }

    [Test]
    public void SerializedEquipmentRetainsDefinitionSlotAndStats()
    {
        var weapon = Item(EquipmentSlot.TwoHand);
        weapon.EquipmentItemDefinition.StatModification = new StatModification { Strength = 7 };
        equipment.Equip(weapon);
        var clone = Object.Instantiate(owner);
        try
        {
            var restored = clone.GetComponent<Equipment>();
            Assert.That(restored.EquippedWeapon.EquipmentItemDefinition, Is.SameAs(weapon.ItemDefinition));
            Assert.That(restored.EquippedWeapon.EquipmentSlot, Is.EqualTo(EquipmentSlot.TwoHand));
            Assert.That(restored.GetEquipmentStatModification().Strength, Is.EqualTo(7));
            restored.Equip(Item(EquipmentSlot.OffHand));
            Assert.That(restored.EquippedWeapon, Is.Null);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [TestCase(EquipmentSlot.MainHand)]
    [TestCase(EquipmentSlot.OffHand)]
    [TestCase(EquipmentSlot.Accessory)]
    public void ReplacementNotifiesOnceAndStaleUnequipDoesNotRemoveReplacement(EquipmentSlot slot)
    {
        var first = Item(slot);
        var second = Item(slot);
        equipment.Equip(first);
        var returned = new List<EquipableInventoryItem>();
        equipment.HandleEquipmentChanged += (change, item) => {
            if (change == EquipChangeType.UnEquip) returned.Add(item);
        };
        equipment.Equip(second);
        equipment.Equip(second);
        equipment.UnEquip(first);
        Assert.That(returned, Is.EqualTo(new[] { first }));
        Assert.That(equipment.IsEquipped(second), Is.True);
    }
}
