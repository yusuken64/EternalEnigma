#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public sealed class ClassContentTests
    {
        private const string Hint = "Run Tools > Eternal Enigma > Classes > Generate All Class Content first.";

        private static ClassCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ClassCatalog>("Assets/Resources/Classes/ClassCatalog.asset");
            Assert.That(catalog, Is.Not.Null, Hint);
            return catalog;
        }

        [Test]
        public void CatalogHasTheTenClasses()
        {
            var catalog = Catalog();
            var classIds = catalog.Classes.Select(c => c.Id).ToList();
            var expectedIds = ClassContentExpectations.ClassIds.ToList();

            Assert.That(classIds, Is.EqualTo(expectedIds), "Class IDs should match expectations");

            var errors = catalog.Validate();
            Assert.That(errors, Is.Empty, "Catalog validation should pass");
        }

        [Test]
        public void EachClassKitMatchesTheDesign()
        {
            var catalog = Catalog();

            foreach (var id in ClassContentExpectations.ClassIds)
            {
                var def = catalog.Get(id);
                Assert.That(def, Is.Not.Null, $"Class {id} should exist in catalog");

                var expected = ClassContentExpectations.Rows.Where(r => r.ClassId == id).ToList();

                Assert.That(def.Skills.Count, Is.EqualTo(20), $"Class {id} should have 20 skills");

                for (int i = 0; i < 20; i++)
                {
                    var entry = def.Skills[i];
                    Assert.That(entry, Is.Not.Null, $"{id} #{i + 1} entry should not be null");
                    Assert.That(entry.Skill, Is.Not.Null, $"{id} #{i + 1} skill should not be null");

                    var expectedRow = expected[i];
                    var message = $"{id} #{i + 1} {expectedRow.Name}";

                    Assert.That(entry.Skill.SkillName, Is.EqualTo(expectedRow.Name), message);
                    Assert.That(entry.Tier, Is.EqualTo(expectedRow.Tier), message);
                    Assert.That(entry.Kind, Is.EqualTo(expectedRow.Kind), message);
                    Assert.That(entry.MaxRank, Is.EqualTo(expectedRow.MaxRank), message);
                }

                var skillTable = def.ToSkillTable();
                var validationErrors = ClassKitValidator.Validate(skillTable);
                Assert.That(validationErrors, Is.Empty, $"ClassKitValidator should pass for {id}");
            }
        }

        [Test]
        public void SkillAssetsAreWellFormed()
        {
            var catalog = Catalog();
            var sharedSkills = ClassContentExpectations.SharedSkills;

            foreach (var classId in ClassContentExpectations.ClassIds)
            {
                var def = catalog.Get(classId);
                Assert.That(def, Is.Not.Null);

                foreach (var entry in def.Skills)
                {
                    if (entry?.Skill == null) continue;

                    var skill = entry.Skill;
                    var skillName = skill.SkillName;

                    // Check SPCost
                    if (skill.ActivationType == ActivationType.Active)
                    {
                        Assert.That(skill.SPCost, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(3),
                            $"{classId}/{skillName} active skill should have SPCost 1-3");
                        Assert.That(skill.ActionEffects, Is.Not.Null.And.Not.Empty,
                            $"{classId}/{skillName} active skill should have ActionEffects");
                        Assert.That(skill.ActionEffects.All(a => a != null), Is.True,
                            $"{classId}/{skillName} ActionEffects should not contain null");
                    }
                    else
                    {
                        Assert.That(skill.SPCost, Is.EqualTo(0),
                            $"{classId}/{skillName} passive skill should have SPCost 0");
                    }

                    // Check LearnCost and MaxRank for non-shared skills
                    if (!sharedSkills.Contains(skillName))
                    {
                        int expectedLearnCost;
                        if (entry.Kind == SkillKind.Mastery)
                        {
                            expectedLearnCost = entry.Tier switch
                            {
                                1 => 0,
                                2 => 300,
                                3 => 800,
                                _ => -1
                            };
                        }
                        else
                        {
                            expectedLearnCost = entry.Tier switch
                            {
                                1 => 50,
                                2 => 150,
                                3 => 400,
                                _ => -1
                            };
                        }

                        Assert.That(skill.LearnCost, Is.EqualTo(expectedLearnCost),
                            $"{classId}/{skillName} tier {entry.Tier} should have LearnCost {expectedLearnCost}");
                        Assert.That(skill.MaxRank, Is.EqualTo(entry.MaxRank),
                            $"{classId}/{skillName} MaxRank should match entry");
                    }
                }
            }
        }

        [Test]
        public void StatusEffectsAreWired()
        {
            var catalog = Catalog();

            foreach (var classId in ClassContentExpectations.ClassIds)
            {
                var def = catalog.Get(classId);
                Assert.That(def, Is.Not.Null);

                foreach (var entry in def.Skills)
                {
                    if (entry?.Skill?.ActionEffects == null) continue;

                    foreach (var action in entry.Skill.ActionEffects)
                    {
                        if (action is ApplyStatusChanceAction statusAction)
                        {
                            Assert.That(statusAction.StatusEffect, Is.Not.Null,
                                $"{classId}/{entry.Skill.SkillName} ApplyStatusChanceAction should have StatusEffect");
                        }
                    }
                }
            }
        }

        [Test]
        public void ArrowSkillsNeedABow()
        {
            var catalog = Catalog();

            foreach (var classId in ClassContentExpectations.ClassIds)
            {
                var def = catalog.Get(classId);
                Assert.That(def, Is.Not.Null);

                foreach (var entry in def.Skills)
                {
                    if (entry?.Skill == null || entry.Skill.ArrowCost <= 0) continue;

                    var skillName = entry.Skill.SkillName;
                    var hasBowRequirement = false;

                    if (entry.Skill.ActionEffects != null)
                    {
                        foreach (var action in entry.Skill.ActionEffects)
                        {
                            if (action is ScaledDamageAction scaledDamage)
                            {
                                if (scaledDamage.Requires == EquipmentRequirement.Bow)
                                {
                                    hasBowRequirement = true;
                                    break;
                                }
                            }
                            else if (action is PierceLineAction pierceAction)
                            {
                                if (pierceAction.Damage?.Requires == EquipmentRequirement.Bow)
                                {
                                    hasBowRequirement = true;
                                    break;
                                }
                            }
                            else if (action is RandomHitsAction randomAction)
                            {
                                if (randomAction.Damage?.Requires == EquipmentRequirement.Bow)
                                {
                                    hasBowRequirement = true;
                                    break;
                                }
                            }
                        }
                    }

                    Assert.That(hasBowRequirement, Is.True,
                        $"{classId}/{skillName} with ArrowCost > 0 should require a Bow");
                }
            }
        }

        [Test]
        public void SkillNamesResolveToOneAsset()
        {
            var catalog = Catalog();
            var skillsByName = new Dictionary<string, Skill>();

            // Build dictionary from all class entries
            foreach (var classId in ClassContentExpectations.ClassIds)
            {
                var def = catalog.Get(classId);
                Assert.That(def, Is.Not.Null);

                foreach (var entry in def.Skills)
                {
                    if (entry?.Skill == null) continue;

                    var skillName = entry.Skill.SkillName;

                    if (skillsByName.TryGetValue(skillName, out var existing))
                    {
                        Assert.That(existing, Is.SameAs(entry.Skill),
                            $"Skill name '{skillName}' must resolve to the same asset");
                    }
                    else
                    {
                        skillsByName[skillName] = entry.Skill;
                    }
                }
            }

            // Check legacy assets under Assets/Prefabs/Dungeon/Skills/SkillsData
            var legacyGuids = AssetDatabase.FindAssets("t:Skill", new[] { "Assets/Prefabs/Dungeon/Skills/SkillsData" });
            foreach (var guid in legacyGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var legacySkill = AssetDatabase.LoadAssetAtPath<Skill>(path);

                if (legacySkill != null && !string.IsNullOrEmpty(legacySkill.SkillName))
                {
                    if (skillsByName.TryGetValue(legacySkill.SkillName, out var expectedSkill))
                    {
                        Assert.That(legacySkill, Is.SameAs(expectedSkill),
                            $"Legacy skill '{legacySkill.SkillName}' at {path} should match the class skill asset");
                    }
                }
            }
        }

        [Test]
        public void EveryHeroHasItsFixedClass()
        {
            var catalog = Catalog();
            var primaryClassesUsed = new HashSet<string>();

            foreach (var (prefab, primary, secondary) in ClassContentExpectations.Heroes)
            {
                var ally = AssetDatabase.LoadAssetAtPath<TownAlly>($"Assets/Prefabs/Town/Allies/{prefab}.prefab");
                Assert.That(ally, Is.Not.Null, $"Hero {prefab} prefab should exist");

                if (!string.IsNullOrEmpty(primary))
                {
                    Assert.That(ally.PrimaryClass?.Id, Is.EqualTo(primary),
                        $"Hero {prefab} primary class should be {primary}");
                    primaryClassesUsed.Add(primary);
                }
                else
                {
                    Assert.That(ally.PrimaryClass, Is.Null, $"Hero {prefab} should have no primary class");
                }

                if (!string.IsNullOrEmpty(secondary))
                {
                    Assert.That(ally.SecondaryClass?.Id, Is.EqualTo(secondary),
                        $"Hero {prefab} secondary class should be {secondary}");
                }
                else
                {
                    Assert.That(ally.SecondaryClass, Is.Null, $"Hero {prefab} should have no secondary class");
                }

                var kit = HeroClass.ToKit(ally.PrimaryClass, ally.SecondaryClass);
                if (kit != null)
                {
                    var validationErrors = ClassKitValidator.Validate(kit);
                    Assert.That(validationErrors, Is.Empty,
                        $"Hero {prefab} class kit should validate");
                }
            }

            // Each class id appears as a primary at least twice
            foreach (var classId in ClassContentExpectations.ClassIds)
            {
                var count = ClassContentExpectations.Heroes.Count(h => h.Primary == classId);
                Assert.That(count, Is.GreaterThanOrEqualTo(2),
                    $"Class {classId} should be assigned as primary class to at least 2 heroes");
            }
        }

        [Test]
        public void TownsHaveNoLegacyTrainerList()
        {
            var townGuids = AssetDatabase.FindAssets("t:TownConfiguration");
            foreach (var guid in townGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var townConfig = AssetDatabase.LoadAssetAtPath<TownConfiguration>(path);

                if (townConfig != null)
                {
                    Assert.That(townConfig.LearnableSkills == null || townConfig.LearnableSkills.Count == 0,
                        $"Town {townConfig.Id} ({path}) should have no legacy LearnableSkills");
                }
            }
        }

        [Test]
        public void BossesAndResistancesAreAuthored()
        {
            // Dragons and Demon Kings are bosses
            var dragon = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Dragon.prefab");
            Assert.That(dragon, Is.Not.Null, "Dragon prefab should exist");
            Assert.That(dragon.IsBoss, Is.True, "Dragon should be marked as boss");

            var demonKing = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_DemonKing.prefab");
            Assert.That(demonKing, Is.Not.Null, "Demon King prefab should exist");
            Assert.That(demonKing.IsBoss, Is.True, "Demon King should be marked as boss");

            // Slime is not a boss
            var slime = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
            Assert.That(slime, Is.Not.Null, "Slime prefab should exist");
            Assert.That(slime.IsBoss, Is.False, "Slime should not be marked as boss");

            // Salamander has Fire Resistance 2
            var salamander = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Salamander.prefab");
            Assert.That(salamander, Is.Not.Null, "Salamander prefab should exist");
            Assert.That(salamander.StartingStats.FireResistance, Is.EqualTo(2),
                "Salamander should have FireResistance 2");

            // Golem has Lightning Resistance 2
            var golem = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Golem.prefab");
            Assert.That(golem, Is.Not.Null, "Golem prefab should exist");
            Assert.That(golem.StartingStats.LightningResistance, Is.EqualTo(2),
                "Golem should have LightningResistance 2");
        }
    }
}
#endif
