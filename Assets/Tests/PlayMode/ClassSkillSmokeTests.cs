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
    public class ClassSkillSmokeTests
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

        private IEnumerator Missile(Skill skill, Vector3Int dir)
        {
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, skill, dir));
        }

        private void ResetHp()
        {
            foreach (var c in harness.Game.AllCharacters)
            {
                c.Vitals.HP = 60;
                c.SyncDisplayedStats();
            }
        }

        [UnityTest]
        public IEnumerator MeleeAndAreaDamageSkillsHurtEnemies()
        {
            foreach (var name in new[] { "Double Strike", "Sunder", "Heavy Strike", "Hamstring", "Throat Strike" })
            {
                ResetHp();
                var s = Learn(name);
                yield return Cast(s, first);
                Assert.That(first.Vitals.HP, Is.LessThan(60), name);
            }
            ResetHp();
            yield return Cast(Learn("Cleave"), caster);
            Assert.That(first.Vitals.HP, Is.LessThan(60), "Cleave");
            Assert.That(second.Vitals.HP, Is.LessThan(60), "Cleave");
        }

        [UnityTest]
        public IEnumerator SpellsHitThroughMissiles()
        {
            ResetHp();
            yield return Missile(Learn("Fire Bolt"), Vector3Int.right);
            Assert.That(first.Vitals.HP, Is.LessThan(60), "Fire Bolt");
        }

        [UnityTest]
        public IEnumerator HealsRestoreHp()
        {
            ResetHp();
            yield return Cast(Learn("Heal"), friend);
            Assert.That(friend.Vitals.HP, Is.GreaterThan(60), "Heal");
            ResetHp();
            yield return Cast(Learn("Full Heal"), caster);
            Assert.That(caster.Vitals.HP, Is.GreaterThan(60), "Full Heal");
            Assert.That(friend.Vitals.HP, Is.GreaterThan(60), "Full Heal");
        }

        [UnityTest]
        public IEnumerator StatusSkillsApplyStatuses()
        {
            ResetHp();
            yield return Cast(Learn("Provoke"), caster);
            Assert.That(first.StatusEffects.Any(s => s.GetEffectName() == "Taunt"), "Provoke");
            ResetHp();
            yield return Missile(Learn("Expose"), Vector3Int.right);
            Assert.That(first.StatusEffects.Any(s => s.GetEffectName() == "Exposed"), "Expose");
            ResetHp();
            yield return Cast(Learn("Vanish"), caster);
            Assert.That(caster.StatusEffects.Any(s => s.GetEffectName() == "Stealth"), "Vanish");
            ResetHp();
            yield return Cast(Learn("Command: Endure"), caster);
            Assert.That(friend.StatusEffects.Any(s => s.GetEffectName() == "Endure"), "Command: Endure");
            ResetHp();
            yield return Cast(Learn("Sunder"), first);
            Assert.That(first.StatusEffects.Any(s => s.GetEffectName() == "Weaken"), "Sunder");
        }

        [UnityTest]
        public IEnumerator CleanseAndSpSupport()
        {
            ResetHp();
            friend.ApplyStatusEffect(StatusEffectRegistry.GetByName("Dot"));
            yield return Cast(Learn("Cure"), friend);
            Assert.That(friend.StatusEffects.Any(s => s.GetEffectName() == "Dot"), Is.False, "Cure");
            friend.Vitals.SP = 10;
            friend.SyncDisplayedStats();
            yield return Cast(Learn("Inspire"), friend);
            Assert.That(friend.Vitals.SP, Is.EqualTo(12), "Inspire");
        }

        [UnityTest]
        public IEnumerator PassivesChangeStats()
        {
            ResetHp();
            int def = caster.FinalStats.Defense;
            Learn("Iron Skin");
            yield return null;
            Assert.That(caster.FinalStats.Defense, Is.EqualTo(def + 2), "Iron Skin");
            float ev = caster.FinalStats.Evasion;
            Learn("Nimble");
            yield return null;
            Assert.That(caster.FinalStats.Evasion, Is.EqualTo(ev + 0.1f).Within(0.0001f), "Nimble");
        }

        [UnityTest]
        public IEnumerator ArrowSkillUsesAnArrow()
        {
            ResetHp();
            var bow = Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>().First(d => d.WeaponType == WeaponType.BowAndArrow);
            caster.Equipment.Equip((EquipableInventoryItem)bow.AsInventoryItem(null));
            caster.InvalidateCachedStats();
            harness.AddItem("Wooden Arrows");
            int arrows = ArrowSupply.Count(caster);
            Assume.That(arrows, Is.GreaterThan(0));
            ResetHp();
            yield return Missile(Learn("Aimed Shot"), Vector3Int.right);
            Assert.That(ArrowSupply.Count(caster), Is.EqualTo(arrows - 1), "Aimed Shot");
            Assert.That(first.Vitals.HP, Is.LessThan(60), "Aimed Shot");
        }
    }
}
#endif
