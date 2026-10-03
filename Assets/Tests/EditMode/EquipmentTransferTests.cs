using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class EquipmentTransferTests
    {
        [Test]public void TwoHandConflictsReturnBothInstancesAndUnequipPreservesIdentity()
        {
            var owner=new GameObject("Equipment transaction test");var equipment=owner.AddComponent<Equipment>();
            var main=ScriptableObject.CreateInstance<EquipmentItemDefinition>();main.ItemName="Duplicate";main.EquipmentSlot=EquipmentSlot.MainHand;
            var off=ScriptableObject.CreateInstance<EquipmentItemDefinition>();off.ItemName="Duplicate";off.EquipmentSlot=EquipmentSlot.OffHand;
            var two=ScriptableObject.CreateInstance<EquipmentItemDefinition>();two.ItemName="Duplicate";two.EquipmentSlot=EquipmentSlot.TwoHand;
            try
            {
                var a=new EquipableInventoryItem(main);var b=new EquipableInventoryItem(off);var c=new EquipableInventoryItem(two);
                equipment.Equip(a);equipment.Equip(b);var bag=new List<InventoryItem>{c};
                Assert.That(EquipmentTransferService.Toggle(equipment,bag,c,null,null,out _),Is.True);
                Assert.That(bag,Is.EqualTo(new InventoryItem[]{a,b}));Assert.That(equipment.EquippedWeapon,Is.SameAs(c));Assert.That(equipment.EquippedShield,Is.Null);
                Assert.That(EquipmentTransferService.Toggle(equipment,bag,c,null,null,out _),Is.True);
                Assert.That(bag,Is.EqualTo(new InventoryItem[]{a,b,c}));Assert.That(equipment.EquippedWeapon,Is.Null);
                var missing=new EquipableInventoryItem(two);
                Assert.That(EquipmentTransferService.Toggle(equipment,bag,missing,null,null,out _),Is.False);
                Assert.That(bag,Has.Count.EqualTo(3));
            }
            finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(main);Object.DestroyImmediate(off);Object.DestroyImmediate(two);}
        }
        [Test]public void ResourceStateUsesActualCapacityAndNeverInventsTownLimit()
        {
            Assert.That(new ResourceHUDState(100,9).BagLabel,Is.EqualTo("Bag  9 items"));
            Assert.That(new ResourceHUDState(100,9,10).BagLabel,Is.EqualTo("Bag  9/10"));
            Assert.That(new ResourceHUDState(100,10,10).BagLabel,Does.EndWith("Full"));
        }
    }
}
