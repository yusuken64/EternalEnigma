using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EternalEnigma.Core.Classes;

public class HeroClassTests
{
    private readonly List<Object> createdObjects = new();

    [SetUp]
    public void SetUp() { }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in createdObjects)
        {
            if (obj != null)
                Object.DestroyImmediate(obj);
        }
        createdObjects.Clear();
    }

    private ClassDefinition Class(string id, params WeaponType[] weapons)
    {
        var classDefinition = ScriptableObject.CreateInstance<ClassDefinition>();
        classDefinition.Id = id;
        classDefinition.DisplayName = char.ToUpper(id[0]) + id.Substring(1);
        classDefinition.AllowedWeapons = new List<WeaponType>(weapons);
        createdObjects.Add(classDefinition);
        return classDefinition;
    }

    private EquipableInventoryItem Item(EquipmentSlot slot, WeaponType type)
    {
        var definition = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        definition.EquipmentSlot = slot;
        definition.WeaponType = type;
        createdObjects.Add(definition);
        return new EquipableInventoryItem(definition);
    }

    private TownAlly Hero(ClassDefinition primary, ClassDefinition secondary)
    {
        var gameObject = new GameObject();
        createdObjects.Add(gameObject);
        var hero = gameObject.AddComponent<TownAlly>();
        hero.Id = "hero";
        hero.Name = "Hero";
        hero.PrimaryClass = primary;
        hero.SecondaryClass = secondary;
        return hero;
    }

    [Test]
    public void ToSkillTableSkipsEmptyEntriesAndCopiesTierRankKind()
    {
        var skill = ScriptableObject.CreateInstance<Skill>();
        skill.SkillName = "Double Strike";
        createdObjects.Add(skill);

        var classDefinition = Class("test");
        classDefinition.Skills = new List<ClassSkillEntryData>
        {
            new ClassSkillEntryData { Skill = skill, Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
            new ClassSkillEntryData { Skill = null },
            null
        };

        var skillTable = classDefinition.ToSkillTable();

        Assert.That(skillTable.ClassId, Is.EqualTo("test"));
        Assert.That(skillTable.Skills.Count, Is.EqualTo(1));
        Assert.That(skillTable.Skills[0].SkillId, Is.EqualTo("Double Strike"));
        Assert.That(skillTable.Skills[0].Tier, Is.EqualTo(1));
        Assert.That(skillTable.Skills[0].MaxRank, Is.EqualTo(5));
        Assert.That(skillTable.Skills[0].Kind, Is.EqualTo(SkillKind.Normal));
    }

    [Test]
    public void CatalogGetIsOrdinalAndValidateReportsProblems()
    {
        var catalog = ScriptableObject.CreateInstance<ClassCatalog>();
        createdObjects.Add(catalog);

        var warrior = Class("warrior", WeaponType.SingleSword, WeaponType.TwoHandSword);
        var scout = Class("scout", WeaponType.BowAndArrow);

        catalog.Classes = new List<ClassDefinition> { warrior, scout };

        // Test Get is ordinal
        Assert.That(catalog.Get("warrior"), Is.SameAs(warrior));
        Assert.That(catalog.Get("Warrior"), Is.Null);
        Assert.That(catalog.Get(""), Is.Null);
        Assert.That(catalog.Get(null), Is.Null);

        // Validate should be empty
        Assert.That(catalog.Validate(), Is.Empty);

        // Add duplicate and invalid entries
        var warrior2 = Class("warrior");
        var noWeapons = Class("naked");
        noWeapons.AllowedWeapons = new List<WeaponType>();

        catalog.Classes.Add(warrior2);
        catalog.Classes.Add(noWeapons);
        catalog.Classes.Add(null);

        var errors = catalog.Validate();
        var errorText = string.Concat(errors);

        Assert.That(errorText, Does.Contain("Duplicate class Id 'warrior'"));
        Assert.That(errorText, Does.Contain("allows no weapons"));
        Assert.That(errorText, Does.Contain("empty"));
    }

    [Test]
    public void NoClassAllowsEverythingAndKeepsDefaultGrowth()
    {
        Assert.That(HeroClass.AllowsWeapon(null, null, WeaponType.MagicWand), Is.True);
        Assert.That(HeroClass.Label(null, null), Is.EqualTo(""));
        Assert.That(HeroClass.ToKit(null, null), Is.Null);

        var growth = HeroClass.Growth(null);
        Assert.That(growth.Strength, Is.EqualTo(2));
        Assert.That(growth.HPMax, Is.EqualTo(5));
        Assert.That(growth.SPMax, Is.EqualTo(0));
        Assert.That(growth.Defense, Is.EqualTo(0));
    }

    [Test]
    public void CombinationUnionsWeaponsAndAccessoriesAreUnrestricted()
    {
        var warrior = Class("warrior", WeaponType.SingleSword, WeaponType.TwoHandSword);
        var archer = Class("archer", WeaponType.BowAndArrow);

        Assert.That(HeroClass.AllowsWeapon(warrior, null, WeaponType.BowAndArrow), Is.False);
        Assert.That(HeroClass.AllowsWeapon(warrior, archer, WeaponType.BowAndArrow), Is.True);

        Assert.That(HeroClass.AllowsItem(warrior, null, Item(EquipmentSlot.Accessory, WeaponType.BowAndArrow)), Is.True);
        Assert.That(HeroClass.AllowsItem(warrior, null, Item(EquipmentSlot.OffHand, WeaponType.OffhandShield)), Is.False);

        Assert.That(HeroClass.Label(warrior, archer), Is.EqualTo("Warrior / Archer"));
        Assert.That(HeroClass.Label(warrior, warrior), Is.EqualTo("Warrior"));
    }

    [Test]
    public void GrowthUsesPrimaryClass()
    {
        var classDefinition = Class("mage");
        classDefinition.GrowthPerLevel = new StatModification { HPMax = 7, Strength = 1, Defense = 1 };

        var growth = HeroClass.Growth(classDefinition);

        Assert.That(growth, Is.SameAs(classDefinition.GrowthPerLevel));
        Assert.That(growth.Strength, Is.EqualTo(1));
        Assert.That(growth.HPMax, Is.EqualTo(7));
        Assert.That(growth.Defense, Is.EqualTo(1));
    }

    [Test]
    public void ToKitBuildsCombinationAndIgnoresDuplicateSecondary()
    {
        var warrior = Class("warrior");
        warrior.Skills = new List<ClassSkillEntryData>();
        var scout = Class("scout");
        scout.Skills = new List<ClassSkillEntryData>();

        var kit = HeroClass.ToKit(warrior, scout);
        Assert.That(kit.IsCombination, Is.True);
        Assert.That(kit.Primary.ClassId, Is.EqualTo("warrior"));
        Assert.That(kit.Secondary.ClassId, Is.EqualTo("scout"));

        var singleKit = HeroClass.ToKit(warrior, warrior);
        Assert.That(singleKit.IsCombination, Is.False);
    }

    [Test]
    public void BindingRecruitKeepsPrefabClass()
    {
        var save = new GameSaveData { ProtagonistId = "protagonist" };
        var warrior = Class("warrior");
        var hero = Hero(warrior, null);
        var data = new TownAllyData { AllyId = "hero", PrimaryClassId = "scout" };

        LogAssert.Expect(LogType.Warning, new Regex("differs from its prefab"));
        HeroClassBinding.Apply(hero, data, save);

        Assert.That(hero.PrimaryClass, Is.SameAs(warrior));
        Assert.That(data.PrimaryClassId, Is.EqualTo("warrior"));
        Assert.That(data.SecondaryClassId, Is.EqualTo(""));
    }

    [Test]
    public void BindingProtagonistWithoutSavedClassKeepsPrefabClass()
    {
        var save = new GameSaveData { ProtagonistId = "hero" };
        var warrior = Class("warrior");
        var hero = Hero(warrior, null);
        var data = new TownAllyData { AllyId = "hero", PrimaryClassId = "", SecondaryClassId = "" };

        HeroClassBinding.Apply(hero, data, save);

        Assert.That(hero.PrimaryClass, Is.SameAs(warrior));
        Assert.That(data.PrimaryClassId, Is.EqualTo("warrior"));
    }

    [Test]
    public void IsProtagonistFallsBackToFirstRecruitedEntry()
    {
        var save = new GameSaveData { ProtagonistId = "" };
        var dataA = new TownAllyData { AllyId = "a" };
        var dataB = new TownAllyData { AllyId = "b" };
        save.TownSaveData = new TownSaveData { RecruitedAlliesData = new List<TownAllyData> { dataA, dataB } };

        Assert.That(HeroClassBinding.IsProtagonist(save, dataA), Is.True);
        Assert.That(HeroClassBinding.IsProtagonist(save, dataB), Is.False);

        save.ProtagonistId = "b-id";
        dataB.AllyId = "b-id";

        Assert.That(HeroClassBinding.IsProtagonist(save, dataB), Is.True);
        Assert.That(HeroClassBinding.IsProtagonist(save, dataA), Is.False);
    }

    [Test]
    public void SaveJsonRoundTripsClassIds()
    {
        var data = new TownAllyData
        {
            AllyId = "x",
            PrimaryClassId = "guardian",
            SecondaryClassId = "scout",
            Skills = new List<string>()
        };

        var json = JsonUtility.ToJson(data);
        var restored = JsonUtility.FromJson<TownAllyData>(json);

        Assert.That(restored.PrimaryClassId, Is.EqualTo("guardian"));
        Assert.That(restored.SecondaryClassId, Is.EqualTo("scout"));

        var partial = JsonUtility.FromJson<TownAllyData>("{\"AllyId\":\"y\"}");
        Assert.That(partial.PrimaryClassId, Is.EqualTo(""));
    }
}
