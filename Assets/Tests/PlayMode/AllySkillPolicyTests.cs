#if UNITY_EDITOR
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
    public class AllySkillPolicyTests
    {
        private GameTestHarness harness;
        private Ally leader;
        private Ally friend;
        private Ally third;
        private Enemy weak;
        private Enemy strong;
        private readonly List<Object> created = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese", "Quinn" } });

            leader = harness.Ally;
            var allies = harness.Game.Allies.Where(a => a != leader).ToList();
            friend = allies[0];
            third = allies[1];

            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));

            leader.SetPosition(center);
            friend.SetPosition(center + Vector3Int.left);
            third.SetPosition(center + Vector3Int.down);
            leader.AllyStrategy = AllyStrategy.Follow;
            friend.AllyStrategy = AllyStrategy.Follow;
            third.AllyStrategy = AllyStrategy.Follow;

            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            weak = (Enemy)harness.Game.Enemies[0];
            yield return harness.SpawnEnemy("Enemy_Slime", center + new Vector3Int(1, 1, 0));
            strong = (Enemy)harness.Game.Enemies[1];

            foreach (var enemy in harness.Game.Enemies.Cast<Enemy>())
            {
                enemy.Policies.Clear();
            }

            foreach (var actor in harness.Game.AllCharacters)
            {
                actor.BaseStats.HPMax = 100;
                actor.BaseStats.SPMax = 100;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = 100;
                actor.Vitals.SP = 50;
                actor.SyncDisplayedStats();
            }

            weak.BaseStats.Strength = 5;
            strong.BaseStats.Strength = 50;
            weak.InvalidateCachedStats();
            weak.SyncDisplayedStats();
            strong.InvalidateCachedStats();
            strong.SyncDisplayedStats();

            friend.Skills.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return harness.Cleanup();
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        private Skill Make(string name, int sp, TargetTeam team, SkillTargeting targeting, params GameAction[] effects)
        {
            var s = ScriptableObject.CreateInstance<Skill>();
            created.Add(s);
            s.SkillName = name;
            s.ActivationType = ActivationType.Active;
            s.SPCost = sp;
            s.Targeting = targeting;
            s.TargetSelector = new TargetSelector { Team = team, Area = TargetArea.Visible };
            s.ActionEffects = new List<GameAction>(effects);
            s.Rank = 1;
            friend.Skills.Add(s);
            return s;
        }

        private AllySkillChoice Decide(Ally who = null) => new AllySkillPolicy(harness.Game, who ?? friend, 0).Decide();

        [UnityTest]
        public IEnumerator EmergencyHealTargetsTheWoundedAllyBeforeDamage()
        {
            var heal = Make("Heal", 1, TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 20 });
            var blast = Make("Blast", 1, TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 100 });

            leader.Vitals.HP = 20;
            leader.SyncDisplayedStats();

            var choice = Decide();

            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("EmergencyHeal"));
            Assert.That(choice.Option.Target, Is.SameAs(leader));

            yield break;
        }

        [UnityTest]
        public IEnumerator NoEmergencyHealWhenEveryoneIsHealthy()
        {
            var heal = Make("Heal", 1, TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 20 });
            var blast = Make("Blast", 1, TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 100 });

            // All at HP 100 already

            var choice = Decide();

            Assert.That(choice != null && choice.Evaluator == "EmergencyHeal", Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator SpReserveBlocksDamageUnlessAggressive()
        {
            var heal = Make("Heal", 3, TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 20 });
            var blast = Make("Blast", 2, TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 100 });

            friend.Vitals.SP = 3;
            friend.SyncDisplayedStats();
            friend.AllyStrategy = AllyStrategy.Follow;

            var choice = Decide();
            Assert.That(choice, Is.Null);

            friend.AllyStrategy = AllyStrategy.Aggresive;
            choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("Damage"));

            yield break;
        }

        [UnityTest]
        public IEnumerator ReviveComesFirst()
        {
            var revive = Make("Revive", 1, TargetTeam.Self, SkillTargeting.Self, new ReviveAction { Scope = ReviveScope.Adjacent, HpFraction = 0.25f });
            var heal = Make("Heal", 1, TargetTeam.Allies, SkillTargeting.SelectedTarget, new TakeHealAction { healing = 20 });

            leader.Vitals.HP = 10;
            leader.SyncDisplayedStats();

            // Place third at a position adjacent to friend for this test
            var dungeon = harness.Game.CurrentDungeon;
            var centerCell = friend.TilemapPosition;
            third.SetPosition(centerCell + new Vector3Int(-1, -1, 0));

            third.Vitals.HP = 0;
            third.SyncDisplayedStats();
            PartyRules.MarkDowned(harness.Game, third);

            var choice = Decide();

            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("Revive"));

            yield break;
        }

        [UnityTest]
        public IEnumerator CureRemovesSleepFromAlly()
        {
            var cure = Make("Cure", 1, TargetTeam.Allies, SkillTargeting.SelectedTarget, new CureStatusEffectsAction());

            var sleepStatus = StatusEffectRegistry.GetByName("Sleep");
            leader.ApplyStatusEffect(sleepStatus);

            var choice = Decide();

            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("Cure"));
            Assert.That(choice.Option.Target, Is.SameAs(leader));

            yield break;
        }

        [UnityTest]
        public IEnumerator BuffCastOnlyWhenMissingAndEnemiesVisible()
        {
            var anger = Make("Anger", 1, TargetTeam.Self, SkillTargeting.Self, new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Strength") });

            // First call: no buff, should be BuffUpkeep
            var choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("BuffUpkeep"));

            // Second call: buff already exists with turns left, should be null or not BuffUpkeep
            var strengthStatus = StatusEffectRegistry.GetByName("Strength");
            var applied = friend.ApplyStatusEffect(strengthStatus);
            applied.TurnsLeft = 5;

            choice = Decide();
            Assert.That(choice == null || choice.Evaluator != "BuffUpkeep", Is.True);

            // Third call: remove buff and all enemies dead, should be null
            friend.StatusEffects.Clear();
            weak.Vitals.HP = 0;
            strong.Vitals.HP = 0;
            weak.SyncDisplayedStats();
            strong.SyncDisplayedStats();

            choice = Decide();
            Assert.That(choice, Is.Null);

            yield break;
        }

        [UnityTest]
        public IEnumerator CrowdControlPicksTheMostDangerousEnemy()
        {
            var sleepEffect = new ApplyStatusEffectAction { StatusEffect = StatusEffectRegistry.GetByName("Sleep") };
            var lull = Make("Lull", 1, TargetTeam.Enemies, SkillTargeting.SelectedTarget, sleepEffect);

            var choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("CrowdControl"));
            Assert.That(choice.Option.Target, Is.SameAs(strong));

            // Apply sleep to strong, should target weak next
            var sleepPrefab = StatusEffectRegistry.GetByName("Sleep");
            strong.ApplyStatusEffect(sleepPrefab);

            choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("CrowdControl"));
            Assert.That(choice.Option.Target, Is.SameAs(weak));

            yield break;
        }

        [UnityTest]
        public IEnumerator DamageSkillOnlyWhenBetterThanNormalAttack()
        {
            var dungeon = harness.Game.CurrentDungeon;

            // Move friend next to weak
            Vector3Int friendPos = Vector3Int.zero;
            bool foundPos = false;
            foreach (var d in SkillCastOptions.Directions)
            {
                var candidate = weak.TilemapPosition + d;
                if (dungeon.IsWalkable(candidate) && !harness.Game.AllCharacters.Any(c => c.TilemapPosition == candidate))
                {
                    friendPos = candidate;
                    foundPos = true;
                    break;
                }
            }

            if (!foundPos)
            {
                Assert.Ignore("layout");
                yield break;
            }

            friend.SetPosition(friendPos);
            friend.BaseStats.Strength = 10;
            friend.InvalidateCachedStats();
            friend.SyncDisplayedStats();

            var blast1 = Make("Blast", 1, TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 1 });

            var choice = Decide();
            Assert.That(choice != null && choice.Evaluator == "Damage", Is.False);

            // Change damage to 200
            blast1.ActionEffects[0] = new TakeDamageAction { damage = 200 };

            choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("Damage"));

            yield break;
        }

        [UnityTest]
        public IEnumerator HoldPositionSkipsMovementSkills()
        {
            var dungeon = harness.Game.CurrentDungeon;

            // Find position for friend such that missile line to weak is clear
            Vector3Int friendPos = Vector3Int.zero;
            bool foundPos = false;
            foreach (var d in SkillCastOptions.Directions)
            {
                var pos1 = weak.TilemapPosition + d;
                var pos2 = weak.TilemapPosition + d * 2;
                if (dungeon.IsWalkable(pos1) && dungeon.IsWalkable(pos2) &&
                    !harness.Game.AllCharacters.Any(c => c.TilemapPosition == pos1) &&
                    !harness.Game.AllCharacters.Any(c => c.TilemapPosition == pos2))
                {
                    friendPos = pos2;
                    foundPos = true;
                    break;
                }
            }

            if (!foundPos)
            {
                Assert.Ignore("layout");
                yield break;
            }

            friend.SetPosition(friendPos);

            var lunge = Make("Lunge", 1, TargetTeam.Enemies, SkillTargeting.Missile, new DashStrikeAction());
            lunge.MissileRange = 3;

            friend.AllyStrategy = AllyStrategy.Follow;
            var choice = Decide();
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.Evaluator, Is.EqualTo("Damage"));

            friend.AllyStrategy = AllyStrategy.HoldPosition;
            choice = Decide();
            Assert.That(choice == null || choice.Evaluator != "Damage", Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator RetreatIsNeverCast()
        {
            friend.Skills.Clear();
            var retreat = Make("Retreat", 0, TargetTeam.Self, SkillTargeting.Self, new RetreatAction());

            weak.Vitals.HP = 0;
            strong.Vitals.HP = 0;
            weak.SyncDisplayedStats();
            strong.SyncDisplayedStats();

            var choice = Decide();

            Assert.That(choice, Is.Null);

            yield break;
        }

        [UnityTest]
        public IEnumerator ControlledAllyIsSkippedUnlessIncluded()
        {
            var blast = Make("Blast", 1, TargetTeam.Enemies, SkillTargeting.SelectedTarget, new TakeDamageAction { damage = 200 });
            leader.Skills.Add(blast);

            harness.Game.PlayerController.TakeControl(leader);

            var policy = new AllySkillPolicy(harness.Game, leader, 0);
            Assert.That(policy.ShouldRun(), Is.False);

            policy.IncludeControlledAlly = true;
            Assert.That(policy.ShouldRun(), Is.True);

            yield break;
        }
    }
}
#endif
