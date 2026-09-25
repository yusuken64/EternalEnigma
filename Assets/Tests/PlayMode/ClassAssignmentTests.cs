#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ClassAssignmentTests
    {
        private GameTestHarness harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup() => harness.Cleanup();

        private static ClassDefinition Class(string id)
        {
            var catalog = ClassCatalog.Load();
            Assert.That(catalog, Is.Not.Null, "Run Tools > Eternal Enigma > Classes > Create Missing Class Definitions first.");
            var definition = catalog.Get(id);
            Assert.That(definition, Is.Not.Null, "Missing class " + id);
            return definition;
        }

        [UnityTest]
        public IEnumerator NewSaveStoresChosenProtagonistClass()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();

            // Test with chosen primary and secondary classes
            var save = menu.CreateNewSave(7, Class("warrior"), Class("scout"));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].PrimaryClassId, Is.EqualTo("warrior"));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].SecondaryClassId, Is.EqualTo("scout"));

            // Test with same primary and secondary (should clear secondary)
            save = menu.CreateNewSave(7, Class("warrior"), Class("warrior"));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].PrimaryClassId, Is.EqualTo("warrior"));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].SecondaryClassId, Is.EqualTo(""));

            // Test with no classes (should match prefab defaults)
            save = menu.CreateNewSave(7);
            var prefab = TownSceneLoader.Default.StartingParty[0];
            string expectedPrimary = prefab.PrimaryClass != null ? prefab.PrimaryClass.Id : "";
            string expectedSecondary = prefab.SecondaryClass != null && prefab.SecondaryClass.Id != expectedPrimary ? prefab.SecondaryClass.Id : "";
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].PrimaryClassId, Is.EqualTo(expectedPrimary));
            Assert.That(save.TownSaveData.RecruitedAlliesData[0].SecondaryClassId, Is.EqualTo(expectedSecondary));
        }

        [UnityTest]
        public IEnumerator TownAppliesAndSavesProtagonistClass()
        {
            var save = new TestScenario().CreateSave();
            save.TownSaveData.RecruitedAlliesData[0].PrimaryClassId = "guardian";
            save.TownSaveData.RecruitedAlliesData[0].SecondaryClassId = "scout";

            yield return harness.LoadTown(save);
            var world = Object.FindFirstObjectByType<Town>();
            var protagonist = world.TownPlayer.RecruitedAllies[0];

            Assert.That(protagonist.PrimaryClass.Id, Is.EqualTo("guardian"));
            Assert.That(protagonist.SecondaryClass.Id, Is.EqualTo("scout"));

            world.SaveProgress();
            var savedData = SaveSystem.LoadData().TownSaveData.RecruitedAlliesData[0];
            Assert.That(savedData.PrimaryClassId, Is.EqualTo("guardian"));
            Assert.That(savedData.SecondaryClassId, Is.EqualTo("scout"));
        }

        [UnityTest]
        public IEnumerator TownRejectsWeaponOutsideClass()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var world = Object.FindFirstObjectByType<Town>();
            var ally = world.TownPlayer.RecruitedAllies[0];
            ally.PrimaryClass = Class("elementalist");
            ally.SecondaryClass = null;

            var definition = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                .First(d => d.EquipmentSlot != EquipmentSlot.Accessory && !HeroClass.AllowsWeapon(ally.PrimaryClass, null, d.WeaponType));
            var item = definition.AsInventoryItem(null);
            world.TownPlayer.Inventory.Add(item);

            Assert.That(world.Services.ToggleEquipment(ally, item, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(ally.Equipment.IsEquipped(item), Is.False);
            Assert.That(world.TownPlayer.Inventory, Does.Contain(item));
        }

        [UnityTest]
        public IEnumerator DungeonAllyGrowsByClassAndHasEquipmentFilter()
        {
            yield return harness.LoadDungeon(new TestScenario());
            var ally = harness.Ally;

            Assert.That(ally.Equipment.ClassFilter, Is.Not.Null);

            var growthClass = ScriptableObject.CreateInstance<ClassDefinition>();
            growthClass.Id = "test";
            growthClass.GrowthPerLevel = new StatModification { HPMax = 7, Strength = 1, Defense = 1 };

            ally.PrimaryClass = growthClass;
            int str = ally.BaseStats.Strength;
            int hp = ally.BaseStats.HPMax;
            int def = ally.BaseStats.Defense;

            foreach (var action in new LevelUpAction().ExecuteImmediate(ally))
                action.ExecuteImmediate(ally);

            Assert.That(ally.BaseStats.Strength, Is.EqualTo(str + 1));
            Assert.That(ally.BaseStats.HPMax, Is.EqualTo(hp + 7));
            Assert.That(ally.BaseStats.Defense, Is.EqualTo(def + 1));

            Object.Destroy(growthClass);
        }
    }
}
#endif
