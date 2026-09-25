#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class CombatFoundationTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<Skill> skills = new();
        private readonly List<Object> itemAssets = new();
        private readonly List<StatusEffect> statusTemplates = new();
        private Ally caster => harness.Ally;
        private Ally friend;
        private Character first, second, distant;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
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
            pad = InputSystem.AddDevice<Gamepad>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard, mouse };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            try { yield return harness.Cleanup(); }
            finally
            {
                foreach (var skill in skills) Object.DestroyImmediate(skill);
                skills.Clear();
                foreach (var asset in itemAssets) Object.DestroyImmediate(asset);
                itemAssets.Clear();
                foreach (var template in statusTemplates) Object.DestroyImmediate(template);
                statusTemplates.Clear();
                inputScope.Dispose();
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

        private T CreateStatusTemplate<T>(int turnsLeft = 3) where T : StatusEffect
        {
            var go = new GameObject();
            var status = go.AddComponent<T>();
            status.TurnsLeft = turnsLeft;
            statusTemplates.Add(status);
            return status;
        }

        [UnityTest]
        public IEnumerator FireDamageRespectsResistance()
        {
            first.BaseStats.FireResistance = 1;
            first.InvalidateCachedStats();
            yield return harness.ExecuteAction(new TakeDamageAction(caster, first, 20, true, false, DamageElement.Fire));
            Assert.That(first.Vitals.HP, Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator BarrierCutsElementalDamageToOwner()
        {
            var barrier = CreateStatusTemplate<BarrierStatusEffect>(3);
            barrier.Element = DamageElement.Fire;
            barrier.Reduction = 0.75f;
            caster.ApplyStatusEffect(barrier);

            yield return harness.ExecuteAction(new TakeDamageAction(first, caster, 20, true, false, DamageElement.Fire));
            Assert.That(caster.Vitals.HP, Is.EqualTo(55));

            yield return harness.ExecuteAction(new TakeDamageAction(first, caster, 20, true, false, DamageElement.Physical));
            Assert.That(caster.Vitals.HP, Is.EqualTo(35));
        }

        [UnityTest]
        public IEnumerator CurseReflectsOnceNotRecursively()
        {
            var curse = CreateStatusTemplate<CurseStatusEffect>(3);
            curse.ReflectFraction = 0.5f;
            first.ApplyStatusEffect(curse);

            yield return harness.ExecuteAction(new TakeDamageAction(first, caster, 10));
            Assert.That(caster.Vitals.HP, Is.EqualTo(50));
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
        }

        [UnityTest]
        public IEnumerator PassiveFollowUpIsDepthLimited()
        {
            var echoSkill = ScriptableObject.CreateInstance<Skill>();
            skills.Add(echoSkill);
            echoSkill.ActivationType = ActivationType.Passive;
            echoSkill.PassiveResponses = new List<PassiveResponse> { new EchoResponse() };
            caster.Skills.Add(echoSkill);
            caster.InvalidateCachedStats();

            yield return harness.ExecuteAction(new TakeDamageAction(caster, first, 3));
            Assert.That(first.Vitals.HP, Is.EqualTo(56));
        }

        private class EchoResponse : PassiveResponse
        {
            internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
            {
                if (action is TakeDamageAction hit && hit.Attacker == owner && DamageResponses.CanRespond(hit))
                {
                    yield return new TakeDamageAction(owner, hit.Target, 1) { ResponseDepth = hit.ResponseDepth + 1 };
                }
            }
        }

        [UnityTest]
        public IEnumerator ArrowSkillConsumesAndBlocks()
        {
            var damageSkill = Common.Instance.SkillManager.GetSkillInstanceByName("Damage");
            var arrowSkill = ScriptableObject.CreateInstance<Skill>();
            skills.Add(arrowSkill);
            arrowSkill.name = damageSkill.name;
            arrowSkill.SkillName = damageSkill.SkillName;
            arrowSkill.ActivationType = damageSkill.ActivationType;
            arrowSkill.SPCost = damageSkill.SPCost;
            //arrowSkill.TargetSelector = new TargetSelector(damageSkill.TargetSelector);
            arrowSkill.Targeting = damageSkill.Targeting;
            arrowSkill.MissileRange = damageSkill.MissileRange;
            arrowSkill.ArrowCost = 1;
            arrowSkill.ArrowCostMode = ArrowCostMode.Fixed;
            arrowSkill.ActionEffects = new List<GameAction>(damageSkill.ActionEffects);
            arrowSkill.SkillAnimation = damageSkill.SkillAnimation;
            //arrowSkill.RankScaling = new SkillRankScaling(damageSkill.RankScaling);

            var bow = AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>("Assets/Prefabs/Dungeon/Items/Weapons/LeftHand_Bows.asset");
            Assert.That(bow, Is.Not.Null);
            itemAssets.Add(bow);
            var bowItem = (EquipableInventoryItem)bow.AsInventoryItem(null);
            caster.Equipment.Equip(bowItem);

            var arrows = harness.AddItem("Wooden Arrows");
            arrows.StackStock = 2;

            caster.Skills.Add(arrowSkill);
            caster.InvalidateCachedStats();

            yield return harness.ExecuteAction(new SkillAction(caster, arrowSkill, first));
            Assert.That(ArrowSupply.Count(caster), Is.EqualTo(1));

            yield return harness.ExecuteAction(new SkillAction(caster, arrowSkill, first));
            Assert.That(ArrowSupply.Count(caster), Is.EqualTo(0));

            Assert.That(caster.CanCast(arrowSkill, out var reason1), Is.False);
            Assert.That(reason1, Is.EqualTo("Not enough arrows"));

            //caster.Equipment.Unequip(bowItem.EquipmentItemDefinition);
            Assert.That(caster.CanCast(arrowSkill, out var reason2), Is.False);
            Assert.That(reason2, Is.EqualTo("Needs a bow"));
        }

        [UnityTest]
        public IEnumerator PerTargetArrowSkillSkipsWhenOutOfArrows()
        {
            var damageSkill = Common.Instance.SkillManager.GetSkillInstanceByName("Damage");
            var allTargetSkill = ScriptableObject.CreateInstance<Skill>();
            skills.Add(allTargetSkill);
            allTargetSkill.name = damageSkill.name + "_AllTargets";
            allTargetSkill.SkillName = damageSkill.SkillName;
            allTargetSkill.ActivationType = damageSkill.ActivationType;
            allTargetSkill.SPCost = damageSkill.SPCost;
            allTargetSkill.Targeting = SkillTargeting.AllTargets;
            allTargetSkill.TargetSelector = new TargetSelector { Area = TargetArea.Visible };
            allTargetSkill.ArrowCost = 1;
            allTargetSkill.ArrowCostMode = ArrowCostMode.PerTarget;
            allTargetSkill.ActionEffects = new List<GameAction>(damageSkill.ActionEffects);
            allTargetSkill.SkillAnimation = damageSkill.SkillAnimation;
            //allTargetSkill.RankScaling = new SkillRankScaling(damageSkill.RankScaling);

            var arrows = harness.AddItem("Wooden Arrows");
            arrows.StackStock = 1;

            caster.Skills.Add(allTargetSkill);
            caster.InvalidateCachedStats();

            var initialHPs = harness.Game.Enemies.Select(e => e.Vitals.HP).ToList();

            yield return harness.ExecuteAction(new SkillAction(caster, allTargetSkill, null));

            Assert.That(ArrowSupply.Count(caster), Is.EqualTo(0));
            var damagedCount = harness.Game.Enemies.Count(e => e.Vitals.HP < initialHPs[harness.Game.Enemies.IndexOf(e)]);
            Assert.That(damagedCount, Is.EqualTo(1));
        }

        //[UnityTest]
        //public IEnumerator TauntedEnemyTargetsTaunter()
        //{
        //    var taunt = CreateStatusTemplate<TauntStatusEffect>(3);
        //    first.ApplyStatusEffect(taunt);
        //    taunt.OnApplied(first, friend);

        //    var target = first.GetPursuitTarget();
        //    Assert.That(target, Is.SameAs(friend));
        //}

        //[UnityTest]
        //public IEnumerator SongBuffsStackPerStatAndFamily()
        //{
        //    var song1 = CreateStatusTemplate<TimedBuffStatusEffect>(3);
        //    song1.BuffName = "Song";
        //    song1.Family = BuffFamily.Song;
        //    song1.Modification = new StatModification { Strength = 2 };

        //    caster.ApplyStatusEffect(song1);
        //    Assert.That(caster.StatusEffects.Count, Is.EqualTo(1));
        //    Assert.That(((TimedBuffStatusEffect)caster.StatusEffects[0]).Modification.Strength, Is.EqualTo(2));

        //    var song2 = CreateStatusTemplate<TimedBuffStatusEffect>(3);
        //    song2.BuffName = "Song";
        //    song2.Family = BuffFamily.Song;
        //    song2.Modification = new StatModification { Strength = 5 };

        //    caster.ApplyStatusEffect(song2);
        //    Assert.That(caster.StatusEffects.Count, Is.EqualTo(1));
        //    Assert.That(((TimedBuffStatusEffect)caster.StatusEffects[0]).Modification.Strength, Is.EqualTo(5));

        //    var song3 = CreateStatusTemplate<TimedBuffStatusEffect>(3);
        //    song3.BuffName = "Song";
        //    song3.Family = BuffFamily.Song;
        //    song3.Modification = new StatModification { Defense = 2 };

        //    caster.ApplyStatusEffect(song3);
        //    Assert.That(caster.StatusEffects.Count, Is.EqualTo(2));

        //    var command = CreateStatusTemplate<TimedBuffStatusEffect>(3);
        //    command.BuffName = "Command";
        //    command.Family = BuffFamily.Command;
        //    command.Modification = new StatModification { Strength = 1 };

        //    caster.ApplyStatusEffect(command);
        //    Assert.That(caster.StatusEffects.Count, Is.EqualTo(3));
        //}
    }
}
#endif
