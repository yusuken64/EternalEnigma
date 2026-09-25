#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class TownTrainerRankTests
    {
        private GameTestHarness harness;
        private readonly List<Object> assets = new();
        private Town World => Object.FindFirstObjectByType<Town>();
        private TownMenuManager Manager => Object.FindFirstObjectByType<TownMenuManager>();

        [UnitySetUp]
        public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return harness.Cleanup();
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private Skill MakeSkill(string name, int cost = 50)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            assets.Add(skill);
            skill.SkillName = name;
            skill.LearnCost = cost;
            return skill;
        }

        private ClassDefinition MakeClass()
        {
            var cls = ScriptableObject.CreateInstance<ClassDefinition>();
            assets.Add(cls);
            cls.Id = "test";
            cls.DisplayName = "Test";
            cls.Skills = new List<ClassSkillEntryData>
            {
                new ClassSkillEntryData { Skill = MakeSkill("T Novice"), Tier = 1, MaxRank = 1, Kind = SkillKind.Mastery },
                new ClassSkillEntryData { Skill = MakeSkill("T Strike"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Guard"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Parry"), Tier = 1, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Adept"), Tier = 2, MaxRank = 1, Kind = SkillKind.Mastery },
                new ClassSkillEntryData { Skill = MakeSkill("T Cleave"), Tier = 2, MaxRank = 5, Kind = SkillKind.Normal },
                new ClassSkillEntryData { Skill = MakeSkill("T Master"), Tier = 3, MaxRank = 1, Kind = SkillKind.Mastery },
            };
            return cls;
        }

        [UnityTest]
        public IEnumerator RankUpChargesPerRankAndPersists()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;
            hero.HighestLevel = 20;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(1));
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000 - 50));

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(2));
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000 - 50 - 100));

                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                Assert.That(hero.GetRank("T Strike"), Is.EqualTo(3));
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(10000 - 50 - 100 - 150));

                var saved = SaveSystem.LoadData().TownSaveData;
                Assert.That(saved.RecruitedAlliesData[0].SkillRanks.Any(sr => sr.SkillName == "T Strike" && sr.Rank == 3), Is.True);
                Assert.That(saved.RecruitedAlliesData[0].Skills, Does.Contain("T Strike"));
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator LevelGateAndTierLockReturnReasons()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;
            hero.HighestLevel = 1;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var strikeSkill = cls.Skills.First(s => s.Skill.SkillName == "T Strike").Skill;
                Assert.That(World.Services.Learn(hero, strikeSkill, out _), Is.True);
                int goldAfterFirst = World.TownPlayer.Gold;

                Assert.That(World.Services.Learn(hero, strikeSkill, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(World.TownPlayer.Gold, Is.EqualTo(goldAfterFirst));

                var cleaveSkill = cls.Skills.First(s => s.Skill.SkillName == "T Cleave").Skill;
                Assert.That(World.Services.Learn(hero, cleaveSkill, out reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator TrainerDialogShowsRanksAndRefreshesAfterPurchase()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());

            var hero = World.TownPlayer.ControllingTownAlly;
            var cls = MakeClass();
            hero.PrimaryClass = cls;

            var original = World.Configuration.LearnableSkills.ToList();
            World.Configuration.LearnableSkills.Clear();

            try
            {
                var building = World.TownBuildings.First(b => b.Definition.DialogId == "trainer");
                building.Interact(World.TownPlayer, null);
                yield return null;

                ((BallistaDialog)Manager.CurrentDialog).Show();

                var noviceItem = ((BallistaDialog)Manager.CurrentDialog).SkillGridItems
                    .First(item => item.GetSkill().SkillName == "T Novice");

                noviceItem.ToggleOn_Clicked();
                yield return null;
                ((BallistaDialog)Manager.CurrentDialog).BallistaPurchaseDialog.Purchase_Clicked();

                Assert.That(hero.GetRank("T Novice"), Is.EqualTo(1));
                Assert.That(noviceItem.SkillText.text, Does.Contain("1/1"));
            }
            finally
            {
                World.Configuration.LearnableSkills = original;
            }
        }

        [UnityTest]
        public IEnumerator RestoredHeroKeepsRanksAndHighestLevel()
        {
            var save = new TestScenario { Gold = 100 }.CreateSave();
            save.TownSaveData.RecruitedAlliesData[0].Skills = new List<string> { "Healing" };
            save.TownSaveData.RecruitedAlliesData[0].SkillRanks = new List<SkillRankSaveData>
            {
                new SkillRankSaveData { SkillName = "Healing", Rank = 2 }
            };
            save.TownSaveData.RecruitedAlliesData[0].HighestLevel = 12;

            yield return harness.LoadTown(save);

            var hero = World.TownPlayer.ControllingTownAlly;
            Assert.That(hero.GetRank("Healing"), Is.EqualTo(2));
            Assert.That(hero.HighestLevel, Is.EqualTo(12));
        }
    }
}
#endif
