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
