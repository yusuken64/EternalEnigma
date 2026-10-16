using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GameplayTextTests
{
    [TestCase("Bow02", "Hunter's Bow")]
    [TestCase("Iron Sword (Off-hand)", "Off-hand Iron Sword")]
    [TestCase("ShieldBash", "Shield Bash")]
    [TestCase("Command: Advance", "Advance Order")]
    [TestCase("Demo Scroll: Ally Mend", "Ally Mend Scroll")]
    [TestCase("Enemy_MetalSlime(Clone)", "Metal Slime")]
    [TestCase("Potion 100HP", "Superior Healing Potion")]
    public void NamesAreReadableWithoutChangingTheirIdentity(string identity, string expected)
    {
        var item = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        var skill = ScriptableObject.CreateInstance<Skill>();
        try
        {
            item.ItemName = skill.SkillName = identity;
            Assert.That(item.DisplayName, Is.EqualTo(expected));
            Assert.That(skill.DisplayName, Is.EqualTo(expected));
            Assert.That(item.ItemName, Is.EqualTo(identity));
            Assert.That(skill.SkillName, Is.EqualTo(identity));
        }
        finally { Object.DestroyImmediate(item); Object.DestroyImmediate(skill); }
    }

    [Test]
    public void FormattingPreservesLineBreaksStylingAndUsefulValues()
    {
        Assert.That(GameplayText.Format("\n<b>Restores 40 HP</b>\nCosts 25G. Hit chance +12.5%. Saved at 10:30."),
            Is.EqualTo("\n<b>Restores 40 HP</b>\nCosts 25 gold. Hit chance +12.5%. Saved at 10:30."));
        Assert.That(GameplayText.Format("[Cast] Aria casts Heal (5 Mana)."), Is.EqualTo("Aria casts Heal 5 Mana."));
        Assert.That(GameplayText.Plain("<b>Guard</b> (1 turn)"), Is.EqualTo("Guard for 1 turn"));
        Assert.That(GameplayText.Format("Press {Confirm} to choose or {Back} to return."),
            Is.EqualTo($"Press {InputPrompts.Confirm} to choose or {InputPrompts.Back} to return."));
    }

    [Test]
    public void ShippedItemsSkillsAndNpcCopyContainNoProgrammerNotation()
    {
        string[] roots = { "Assets/Resources", "Assets/Prefabs" };
        int count = 0;
        void Check(string value, string asset)
        {
            string plain = Regex.Replace(value ?? "", "<[^>]+>", "");
            Assert.That(plain, Does.Not.Match(@"[\[\]()_:{}]|HPMax|ManaMax|New Text|placeholder|TODO"), asset);
        }
        foreach (var id in AssetDatabase.FindAssets("t:ItemDefinition", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            Check(item.DisplayName, path); Check(item.Description, path); count++;
        }
        foreach (var id in AssetDatabase.FindAssets("t:Skill", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            var skill = AssetDatabase.LoadAssetAtPath<Skill>(path);
            Check(skill.DisplayName, path); Check(skill.Description, path); count++;
        }
        foreach (var id in AssetDatabase.FindAssets("t:TownNpcDefinition", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            var npc = AssetDatabase.LoadAssetAtPath<TownNpcDefinition>(path);
            Check(npc.DisplayName, path); Check(npc.Greeting, path); count++;
        }
        Assert.That(count, Is.GreaterThan(100), "The audit must cover the shipped content.");
    }

    [Test]
    public void InspectionDescribesActualEquipmentBonuses()
    {
        var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        try
        {
            definition.ItemName = "Iron_Shield";
            definition.EquipmentSlot = EquipmentSlot.OffHand;
            var item = new EquipableInventoryItem(definition);
            string description = InspectInventoryItemEffect.Describe(item);
            Assert.That(description, Does.StartWith("Iron Shield\nOff hand"));
            Assert.That(description, Does.Not.Contain("Strength +0"));
            Assert.That(description, Does.Not.Match(@"[\[\]()_:{}]"));
        }
        finally { Object.DestroyImmediate(definition); }
    }
}
