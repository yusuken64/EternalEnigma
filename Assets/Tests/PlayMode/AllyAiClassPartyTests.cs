#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class AllyAiClassPartyTests
    {
        private static readonly string[] ClassIds =
            { "warrior", "guardian", "archer", "elementalist", "healer", "bard", "occultist", "rogue", "commander", "scout" };

        private GameTestHarness harness;
        private readonly List<Skill> skills = new();
        private Ally leader;
        private Ally allyOne;
        private Ally allyTwo;
        private List<(Ally ally, AllySkillChoice choice)> castRecords;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese", "Quinn" } });

            leader = harness.Ally;
            var allies = harness.Game.Allies.Where(a => a != leader).ToList();
            allyOne = allies[0];
            allyTwo = allies[1];

            // Find center cell
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));

            // Place allies
            leader.SetPosition(center);
            allyOne.SetPosition(center + Vector3Int.left);
            allyTwo.SetPosition(center + Vector3Int.down);
            allyOne.AllyStrategy = AllyStrategy.HoldPosition;
            allyTwo.AllyStrategy = AllyStrategy.HoldPosition;

            // Spawn enemies
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            yield return harness.SpawnEnemy("Enemy_Slime", center + new Vector3Int(1, 1));

            // Clear enemy policies
            foreach (var enemy in harness.Game.Enemies.Cast<Enemy>())
            {
                enemy.Policies.Clear();
                enemy.BaseStats.HPMax = 1000;
                enemy.BaseStats.SPMax = 1000;
                enemy.InvalidateCachedStats();
                enemy.Vitals.HP = 1000;
                enemy.Vitals.SP = 1000;
                enemy.SyncDisplayedStats();
            }

            // Set ally stats
            foreach (var ally in harness.Game.Allies)
            {
                ally.BaseStats.HPMax = 100;
                ally.BaseStats.SPMax = 100;
                ally.InvalidateCachedStats();
                ally.Vitals.HP = 100;
                ally.Vitals.SP = 100;
                ally.SyncDisplayedStats();
            }

            // Make leader wounded
            leader.Vitals.HP = 20;
            leader.SyncDisplayedStats();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AllySkillPolicy.Cast -= OnCast;
            try
            {
                yield return harness.Cleanup();
            }
            finally
            {
                foreach (var skill in skills)
                {
                    //Object.DestroyImmediate(skill);
                }
                skills.Clear();
            }
        }

        private Skill Make(Ally ally, string name, TargetTeam team, SkillTargeting targeting, params GameAction[] effects)
        {
            var s = ScriptableObject.CreateInstance<Skill>();
            skills.Add(s);
            s.SkillName = name;
            s.ActivationType = ActivationType.Active;
            s.SPCost = 1;
            s.Targeting = targeting;
            s.TargetSelector = new TargetSelector { Team = team, Area = TargetArea.Visible };
            s.ActionEffects = new List<GameAction>(effects);
            s.Rank = 1;
            ally.Skills.Add(s);
            return s;
        }

        private void OnCast(Ally ally, AllySkillChoice choice)
        {
            castRecords.Add((ally, choice));
        }

        [UnityTest]
        public IEnumerator AiPartyOfClassCastsSkillsThroughRealTurns([ValueSource(nameof(ClassIds))] string classId)
        {
            castRecords = new List<(Ally, AllySkillChoice)>();

            // Load class definition
            var definition = ClassCatalog.Load()?.Get(classId);
            Assert.That(definition, Is.Not.Null, classId);

            // Set primary class on all allies
            leader.PrimaryClass = definition;
            allyOne.PrimaryClass = definition;
            allyTwo.PrimaryClass = definition;

            // Create skills for AI allies
            Make(allyOne, "Heal", TargetTeam.Allies, SkillTargeting.SelectedTarget,
                new TakeHealAction { healing = 30 });
            Make(allyOne, "Blast", TargetTeam.Enemies, SkillTargeting.SelectedTarget,
                new TakeDamageAction { damage = 200 });
            Make(allyOne, "Anger", TargetTeam.Self, SkillTargeting.Self,
                new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Strength") });
            Make(allyOne, "Lull", TargetTeam.Enemies, SkillTargeting.SelectedTarget,
                new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Sleep") });

            Make(allyTwo, "Heal", TargetTeam.Allies, SkillTargeting.SelectedTarget,
                new TakeHealAction { healing = 30 });
            Make(allyTwo, "Blast", TargetTeam.Enemies, SkillTargeting.SelectedTarget,
                new TakeDamageAction { damage = 200 });
            Make(allyTwo, "Anger", TargetTeam.Self, SkillTargeting.Self,
                new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Strength") });
            Make(allyTwo, "Lull", TargetTeam.Enemies, SkillTargeting.SelectedTarget,
                new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Sleep") });

            // Subscribe to cast events
            AllySkillPolicy.Cast += OnCast;

            // Run 4 turns
            for (int i = 0; i < 4; i++)
            {
                yield return harness.ExecuteAction(new WaitAction());
            }

            // Assertions
            LogAssert.NoUnexpectedReceived();
            Assert.That(castRecords.Any(r => r.ally == allyOne), Is.True, $"AllyOne should cast at least once for {classId}");
            Assert.That(castRecords.Any(r => r.ally == allyTwo), Is.True, $"AllyTwo should cast at least once for {classId}");
            Assert.That(castRecords.Any(r => r.choice.Evaluator == "EmergencyHeal"), Is.True, $"EmergencyHeal should be used for {classId}");
            Assert.That(leader.Vitals.HP, Is.GreaterThan(20), $"Leader should be healed for {classId}");
        }
    }
}
#endif
