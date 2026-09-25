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
    public class SkillRankRegressionTests
    {
        private GameTestHarness harness;
        private readonly List<Skill> skills = new();
        private Ally caster => harness.Ally;
        private Ally friend;
        private Character first, second, distant;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese" } });
            friend = harness.Game.Allies.Single(a => a != caster);
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            caster.SetPosition(center);
            friend.SetPosition(center + Vector3Int.left);
            friend.AllyStrategy = AllyStrategy.HoldPosition;
            var positions = new[] { center + Vector3Int.right, center + Vector3Int.right + Vector3Int.up,
                cells.First(p => TileWorldDungeon.ChevDistance(p, center) > 3) };
            foreach (var position in positions) yield return harness.SpawnEnemy("Enemy_Slime", position);
            first = harness.Game.Enemies[0]; second = harness.Game.Enemies[1]; distant = harness.Game.Enemies[2];
            foreach (var enemy in harness.Game.Enemies.Cast<Enemy>()) enemy.Policies.Clear();
            foreach (var actor in harness.Game.AllCharacters)
            {
                actor.BaseStats.HPMax = actor.BaseStats.SPMax = 100;
                actor.BaseStats.HPRegenAcccumlateThreshold = actor.BaseStats.SPRegenAcccumlateThreshold = 10000;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = 60; actor.Vitals.SP = 20;
                actor.SyncDisplayedStats();
            }
            harness.Game.UpdateMiniMap();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                foreach (var skill in skills) Object.DestroyImmediate(skill);
                skills.Clear();
            }
        }

        private Skill Learn(string name, Character actor = null)
        {
            var skill = Common.Instance.SkillManager.GetSkillInstanceByName(name);
            Assert.That(skill, Is.Not.Null, name);
            skills.Add(skill);
            (actor ?? caster).Skills.Add(skill);
            (actor ?? caster).InvalidateCachedStats();
            (actor ?? caster).SyncDisplayedStats();
            return skill;
        }

        private IEnumerator Cast(Skill skill, Character target)
        {
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skill, target));
        }

        [UnityTest]
        public IEnumerator RankTwoDamageSkillDealsFifteenPercentMore()
        {
            var skill = Learn("Damage");
            skill.Rank = 2;
            yield return Cast(skill, first);
            Assert.That(first.Vitals.HP, Is.EqualTo(60 - 6));
        }

        [UnityTest]
        public IEnumerator RankThreeStatusLastsOneTurnLonger()
        {
            var prefabTurnsLeft = 0;
            var dot = Learn("Dot");

            // Cast at rank 1 on first
            yield return Cast(dot, first);
            var t1 = first.StatusEffects.OfType<DotStatusEffect>().Single().TurnsLeft;
            prefabTurnsLeft = dot.ActionEffects.OfType<ApplyStatusEffectAction>().Single().StatusEffect.TurnsLeft;

            // Remove the status effect
            var s = first.StatusEffects.OfType<DotStatusEffect>().Single();
            first.RemoveStatusEffect(s);
            Object.Destroy(s.gameObject);

            // Cast at rank 3 on first
            dot.Rank = 3;
            yield return Cast(dot, first);
            var t3 = first.StatusEffects.OfType<DotStatusEffect>().Single().TurnsLeft;

            // Assert duration increased by 1 turn
            Assert.That(t3, Is.EqualTo(t1 + 1));

            // Assert prefab is unchanged
            Assert.That(dot.ActionEffects.OfType<ApplyStatusEffectAction>().Single().StatusEffect.TurnsLeft, Is.EqualTo(prefabTurnsLeft));
        }

        //[UnityTest]
        //public IEnumerator RankThreePassiveGrantsScaledBonus()
        //{
        //    var baseHp = caster.FinalStats.HPMax;
        //    var passive = Learn("HP Up");
        //    var basePassiveBonus = passive.PassiveStatModification.HPMax;

        //    // At rank 1, should grant base bonus
        //    Assert.That(caster.FinalStats.HPMax, Is.EqualTo(baseHp + basePassiveBonus));

        //    // At rank 3, should grant base + 2*sign
        //    passive.Rank = 3;
        //    caster.InvalidateCachedStats();
        //    var sign = basePassiveBonus > 0 ? 1 : (basePassiveBonus < 0 ? -1 : 0);
        //    Assert.That(caster.FinalStats.HPMax, Is.EqualTo(baseHp + basePassiveBonus + sign * 2));
        //}

        [UnityTest]
        public IEnumerator DungeonReturnRecordsHighestLevel()
        {
            var save = new GameSaveData();
            var config = ScriptableObject.CreateInstance<TownConfiguration>();

            try
            {
                // Set up caster ID
                if (string.IsNullOrEmpty(caster.TownAllyId))
                    caster.TownAllyId = "rank-test";

                // Create TownAllyData in save
                var allyData = new TownAllyData
                {
                    AllyId = caster.TownAllyId,
                    AllyName = caster.CharacterName,
                    HighestLevel = 3
                };
                save.TownSaveData.RecruitedAlliesData = new List<TownAllyData> { allyData };

                // Set caster level to 7 and commit
                caster.Vitals.Level = 7;
                DungeonReturnService.Commit(save, config, false, 0, new List<InventoryItem>(), new[] { caster });

                // Check that HighestLevel was updated to 7
                var savedAlly = save.TownSaveData.RecruitedAlliesData.First(a => a.AllyId == caster.TownAllyId);
                Assert.That(savedAlly.HighestLevel, Is.EqualTo(7));

                // Reset and commit again at level 2
                save.DungeonSaveData.ReturnCommitted = false;
                caster.Vitals.Level = 2;
                DungeonReturnService.Commit(save, config, false, 0, new List<InventoryItem>(), new[] { caster });

                // Check that HighestLevel stays at 7
                Assert.That(savedAlly.HighestLevel, Is.EqualTo(7));

                // Restore caster level
                caster.Vitals.Level = 1;
            }
            finally
            {
                Object.DestroyImmediate(config);
            }

            yield return null;
        }
    }
}
#endif
