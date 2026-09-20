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
