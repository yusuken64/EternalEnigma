using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class HeroAttributeTests
{
    [Test]
    public void AttributeBundlesUseTotalPointsForBreakpointsAndCaps()
    {
        var three=HeroAttributes.ToModification(new AttributePoints{Str=3,Int=26,Agi=25});
        var four=HeroAttributes.ToModification(new AttributePoints{Str=4,Int=40,Agi=40});
        Assert.That(three.Defense,Is.Zero);
        Assert.That(four.Defense,Is.EqualTo(1));
        Assert.That(four.HPMax,Is.EqualTo(12));
        Assert.That(four.SPMax,Is.EqualTo(80));
        Assert.That(four.MagicPower,Is.EqualTo(40));
        Assert.That(four.SPRegenAcccumlateThreshold,Is.EqualTo(-400));
        Assert.That(four.HitBonus,Is.EqualTo(.4f).Within(.0001f));
        Assert.That(four.Evasion,Is.EqualTo(.25f).Within(.0001f));
        Assert.That(four.CritChance,Is.EqualTo(.25f).Within(.0001f));
    }

    [Test]
    public void OldHeroGetsPendingPointsAndCannotOverspend()
    {
        Assert.That(HeroAttributes.Pending(7,default),Is.EqualTo(6));
        var gameObject=new GameObject("test hero");
        try
        {
            var hero=gameObject.AddComponent<TownAlly>();
            hero.Equipment=gameObject.AddComponent<Equipment>();
            hero.Level=3;hero.Attributes=new AttributePoints{Str=1,Int=1};
            Assert.That(AttributeSpending.TrySpend(hero,HeroAttribute.Agi),Is.False);
            hero.Level=4;
            Assert.That(AttributeSpending.TrySpend(hero,HeroAttribute.Agi),Is.True);
            Assert.That(hero.PendingAttributePoints,Is.Zero);
            Assert.That(AttributeSpending.TrySpend(hero,HeroAttribute.Agi),Is.False);
        }
        finally{Object.DestroyImmediate(gameObject);}
    }

    [Test]
    public void MagicZeroAndLoadoutPreviewPreserveExistingPower()
    {
        Assert.That(HeroAttributes.MagicMultiplier(0),Is.EqualTo(1f));
        var owner=new GameObject("loadout");
        var definition=ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        try
        {
            var equipment=owner.AddComponent<Equipment>();
            definition.EquipmentSlot=EquipmentSlot.TwoHand;
            definition.StatModification=new StatModification{Strength=4};
            var weapon=new EquipableInventoryItem(definition);
            var proposed=equipment.Current().With(weapon);
            Assert.That(proposed.Modification.Strength,Is.EqualTo(4));
            equipment.Equip(weapon);
            Assert.That(equipment.GetEquipmentStatModification().Strength,Is.EqualTo(proposed.Modification.Strength));
        }
        finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(definition);}
    }

    [Test]
    public void SimulatedShieldAndTwoHandLoadoutsMatchCommittedConditionalStats()
    {
        var owner=new GameObject("conditional hero");
        var shieldDefinition=ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        var greatswordDefinition=ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        var passive=ScriptableObject.CreateInstance<Skill>();
        try
        {
            var equipment=owner.AddComponent<Equipment>();
            var hero=owner.AddComponent<Ally>();hero.Equipment=equipment;
            hero.Skills=new List<Skill>{passive};
            hero.BaseStats=new Stats{HPMax=20,Strength=5};
            hero.Vitals=new Vitals();hero.Vitals.HP=20;
            passive.ActivationType=ActivationType.Passive;
            passive.PassiveResponses=new List<PassiveResponse>{new ConditionalStatPassive{
                Condition=StatCondition.ShieldEquipped,Bonus=new StatModification{Defense=2}}};
            shieldDefinition.EquipmentSlot=EquipmentSlot.OffHand;
            shieldDefinition.WeaponType=WeaponType.OffhandShield;
            shieldDefinition.StatModification=new StatModification{Defense=1};
            greatswordDefinition.EquipmentSlot=EquipmentSlot.TwoHand;
            greatswordDefinition.StatModification=new StatModification{Strength=3};
            var shield=new EquipableInventoryItem(shieldDefinition);
            var sword=new EquipableInventoryItem(greatswordDefinition);
            var shieldPreview=StatPreview.Final(hero,equipment.Current().With(shield));
            Assert.That(shieldPreview.Defense,Is.EqualTo(3));
            equipment.Equip(shield);
            Assert.That(StatPreview.Final(hero,equipment.Current()).Defense,Is.EqualTo(shieldPreview.Defense));
            var swordLoadout=equipment.Current().With(sword);
            Assert.That(swordLoadout.OffHand,Is.Null);
            var swordPreview=StatPreview.Final(hero,swordLoadout);
            equipment.Equip(sword);
            Assert.That(StatPreview.Final(hero,equipment.Current()).Defense,Is.EqualTo(swordPreview.Defense));
            Assert.That(StatPreview.Final(hero,equipment.Current()).Strength,Is.EqualTo(swordPreview.Strength));
        }
        finally
        {
            Object.DestroyImmediate(owner);Object.DestroyImmediate(shieldDefinition);
            Object.DestroyImmediate(greatswordDefinition);Object.DestroyImmediate(passive);
        }
    }

    [Test]
    public void NoviceAgilityPassiveOnlyBoostsItsWeaponCategory()
    {
        var owner=new GameObject("agility hero");
        var passive=ScriptableObject.CreateInstance<Skill>();
        try
        {
            var hero=owner.AddComponent<Ally>();hero.Attributes=new AttributePoints{Agi=10};
            passive.ActivationType=ActivationType.Passive;
            passive.PassiveResponses=new List<PassiveResponse>{new AgilityDamageBonus{Category=DamageCategory.Bow}};
            hero.Skills=new List<Skill>{passive};
            Assert.That(ClassPassives.DamageMultiplier(new OutgoingDamage(hero,null,DamageCategory.Bow,DamageElement.Physical,false)),Is.EqualTo(1.1f).Within(.0001f));
            Assert.That(ClassPassives.DamageMultiplier(new OutgoingDamage(hero,null,DamageCategory.Weapon,DamageElement.Physical,false)),Is.EqualTo(1f));
            ((AgilityDamageBonus)passive.PassiveResponses[0]).Category=DamageCategory.Weapon;
            Assert.That(ClassPassives.DamageMultiplier(new OutgoingDamage(hero,null,DamageCategory.Weapon,DamageElement.Physical,false)),Is.EqualTo(1.1f).Within(.0001f));
        }
        finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(passive);}
    }

    [Test]
    public void AttributesRoundTripThroughSaveAndDungeonReturn()
    {
        var data=new TownAllyData{AllyId="test",Level=5,Attributes=new AttributePoints{Str=2,Int=1}};
        var restored=JsonUtility.FromJson<TownAllyData>(JsonUtility.ToJson(data));
        Assert.That(restored.Attributes.Str,Is.EqualTo(2));
        Assert.That(HeroAttributes.Pending(restored.Level,restored.Attributes),Is.EqualTo(1));
        var owner=new GameObject("return hero");
        var configuration=ScriptableObject.CreateInstance<TownConfiguration>();
        try
        {
            var hero=owner.AddComponent<Ally>();hero.Equipment=owner.AddComponent<Equipment>();
            hero.Skills=new List<Skill>();hero.BaseStats=new Stats{HPMax=20,SPMax=5,HungerMax=100};
            hero.TownAllyId="test";hero.Attributes=restored.Attributes;
            hero.Vitals=new Vitals();hero.Vitals.Level=5;hero.Vitals.Exp=105;
            var save=new GameSaveData();save.TownSaveData.RecruitedAlliesData.Add(restored);
            DungeonReturnService.Commit(save,configuration,true,0,new List<InventoryItem>(),new[]{hero});
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].Attributes.Int,Is.EqualTo(1));
            Assert.That(HeroAttributes.Pending(save.TownSaveData.RecruitedAlliesData[0].Level,
                save.TownSaveData.RecruitedAlliesData[0].Attributes),Is.EqualTo(1));
        }
        finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(configuration);}
    }
}
