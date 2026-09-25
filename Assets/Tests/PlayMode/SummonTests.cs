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
    public class SummonTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<Skill> skills = new();
        private readonly List<Object> itemAssets = new();
        private Ally caster => harness.Ally;
        private Character firstEnemy, secondEnemy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AllyName = "Rowan" });

            // Set caster strategy to HoldPosition
            caster.AllyStrategy = AllyStrategy.HoldPosition;

            // Find a 3x3 open area and place the caster in the center
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            caster.SetPosition(center);

            // Spawn two Enemy_Slime 2-3 tiles away
            var positions = new[] {
                center + Vector3Int.right + Vector3Int.right,
                center + Vector3Int.right + Vector3Int.right + Vector3Int.up
            };
            foreach (var position in positions) yield return harness.SpawnEnemy("Enemy_Slime", position);
            firstEnemy = harness.Game.Enemies[0];
            secondEnemy = harness.Game.Enemies[1];

            // Clear their policies
            foreach (var enemy in harness.Game.Enemies.Cast<Enemy>()) enemy.Policies.Clear();

            // Set standard BaseStats: HPMax 100, Strength 20, Defense 10
            foreach (var actor in harness.Game.AllCharacters)
            {
                actor.BaseStats.HPMax = 100;
                actor.BaseStats.Strength = 20;
                actor.BaseStats.Defense = 10;
                actor.BaseStats.SPMax = 100;
                actor.BaseStats.HPRegenAcccumlateThreshold = actor.BaseStats.SPRegenAcccumlateThreshold = 10000;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = actor.FinalStats.HPMax;
                actor.Vitals.SP = actor.FinalStats.SPMax;
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
                inputScope.Dispose();
            }
        }

        private Skill MakeSkill(string name, GameAction action)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skills.Add(skill);
            skill.SkillName = name;
            skill.Targeting = SkillTargeting.Self;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.All };
            skill.SPCost = 0;
            skill.MissileRange = 0;
            skill.AreaRadius = 0;
            skill.ActionEffects = new List<GameAction> { action };
            skill.ActivationType = ActivationType.Active;
            skill.PassiveResponses = new List<PassiveResponse>();
            return skill;
        }

        private Skill MakePassive(string name, params PassiveResponse[] responses)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skills.Add(skill);
            skill.SkillName = name;
            skill.ActivationType = ActivationType.Passive;
            skill.PassiveResponses = responses.ToList();
            skill.ActionEffects = new List<GameAction>();
            skill.SPCost = 0;
            return skill;
        }

        private Skill Learn(Skill skill, Character actor = null)
        {
            (actor ?? caster).Skills.Add(skill);
            (actor ?? caster).InvalidateCachedStats();
            (actor ?? caster).SyncDisplayedStats();
            return skill;
        }

        [UnityTest]
        public IEnumerator CloneSpawnsWithHalfStats()
        {
            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            var clones = SummonRules.ClonesOf(harness.Game, caster);
            Assert.That(clones.Count, Is.EqualTo(1), "Should have exactly 1 clone");

            var clone = clones[0];
            Assert.That(clone.BaseStats.HPMax, Is.EqualTo(50), "Clone HPMax should be 50 (half of 100)");
            Assert.That(clone.BaseStats.Strength, Is.EqualTo(10), "Clone Strength should be 10 (half of 20)");
            Assert.That(clone.BaseStats.Defense, Is.EqualTo(5), "Clone Defense should be 5 (half of 10)");
            Assert.That(clone.Skills.Count, Is.EqualTo(0), "Clone should have no skills");
            Assert.That(clone.Vitals.HP, Is.EqualTo(clone.FinalStats.HPMax), "Clone HP should equal its FinalStats.HPMax");
            Assert.That(clone.TownAllyId, Is.EqualTo(""), "Clone TownAllyId should be empty");
            Assert.That(clone, Is.Not.SameAs(caster), "Clone should not be the controlled ally");
        }

        [UnityTest]
        public IEnumerator SecondCloneReplacesFirstAtLimitOne()
        {
            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            var firstClonesBeforeSecond = SummonRules.ClonesOf(harness.Game, caster);
            var firstClone = firstClonesBeforeSecond.Count > 0 ? firstClonesBeforeSecond[0] : null;

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            var clonesAfter = SummonRules.ClonesOf(harness.Game, caster);
            Assert.That(clonesAfter.Count, Is.EqualTo(1), "Should have exactly 1 clone after second cast");

            if (firstClone != null)
            {
                Assert.That(firstClone == null || !harness.Game.Allies.Contains(firstClone),
                    "First clone should be replaced (not in game.Allies)");
            }
        }

        [UnityTest]
        public IEnumerator TwinShadowsAllowsTwoClones()
        {
            var passive = MakePassive("Twin Shadows", new SummonLimitBonus { ExtraClones = 1 });
            Learn(passive);

            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            var clones = SummonRules.ClonesOf(harness.Game, caster);
            Assert.That(clones.Count, Is.EqualTo(2), "With SummonLimitBonus, should have 2 clones after 3 casts");
        }

        [UnityTest]
        public IEnumerator CloneExpiresAfterItsTurns()
        {
            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 2 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            Assert.That(SummonRules.ClonesOf(harness.Game, caster).Count, Is.EqualTo(1), "Clone should exist after spawn");

            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            var clonesAfterFirstWait = SummonRules.ClonesOf(harness.Game, caster);
            Assert.That(clonesAfterFirstWait.Count, Is.EqualTo(1), "Clone should still exist after 1st turn");

            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            var clonesAfterSecondWait = SummonRules.ClonesOf(harness.Game, caster);
            Assert.That(clonesAfterSecondWait.Count, Is.EqualTo(0), "Clone should be gone after 2 turns");
        }

        [UnityTest]
        public IEnumerator ClonesDespawnOnFloorChange()
        {
            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            Assert.That(SummonRules.ClonesOf(harness.Game, caster).Count, Is.EqualTo(1), "Clone should exist before floor change");

            harness.Game.AdvanceFloor();
            yield return harness.WaitForIdle();

            foreach (var ally in harness.Game.Allies)
            {
                var summon = ally.GetComponent<SummonedUnit>();
                Assert.That(summon, Is.Null, "No SummonedUnit should exist on any ally after floor change");
            }
        }

        [UnityTest]
        public IEnumerator ClonesGainNoExp()
        {
            var cloneSkill = MakeSkill("Clone", new SummonCloneAction { StatPercent = 0.5f, Turns = 10 });
            Learn(cloneSkill);

            yield return harness.ExecuteAction(new SkillAction(caster, cloneSkill, caster));
            yield return harness.WaitForIdle();

            var clone = SummonRules.ClonesOf(harness.Game, caster)[0];

            // AddXPAction is internal; use ExecuteImmediate directly
            var xpAction = new AddXPAction(clone, 100);
            var effects = xpAction.ExecuteImmediate(clone);
            foreach (var effect in effects)
                clone.ExecuteActionImmediate(effect);

            Assert.That(clone.Vitals.Exp, Is.EqualTo(0), "Clone should not gain XP");
        }

        [UnityTest]
        public IEnumerator DominateFearedEnemySwitchesTeamThenReverts()
        {
            var dominateSkill = MakeSkill("Dominate", new DominateAction { Turns = 2 });
            dominateSkill.Targeting = SkillTargeting.Missile;
            dominateSkill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.All };
            dominateSkill.MissileRange = 10;
            Learn(dominateSkill);

            // Apply Fear to firstEnemy via AddComponent (Phase 3)
            var fearObj = new GameObject();
            var fearEffect = fearObj.AddComponent<FearStatusEffect>();
            fearEffect.TurnsLeft = 5;
            firstEnemy.StatusEffects.Add(fearEffect);

            // Calculate direction from caster to firstEnemy
            var direction = SkillMovement.Step(caster.TilemapPosition, firstEnemy.TilemapPosition);

            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, dominateSkill, direction));
            yield return harness.WaitForIdle();

            Assert.That(firstEnemy.Team, Is.EqualTo(Team.Player), "Dominated enemy should switch to Player team");

            var summonUnit = firstEnemy.GetComponent<SummonedUnit>();
            Assert.That(summonUnit, Is.Not.Null, "Dominated enemy should have SummonedUnit component");
            Assert.That(summonUnit.Kind, Is.EqualTo(SummonKind.Dominated), "SummonedUnit Kind should be Dominated");

            var hasNoFear = firstEnemy.StatusEffects.OfType<FearStatusEffect>().Count() == 0;
            Assert.That(hasNoFear, Is.True, "Dominated enemy should have no Fear status effect");

            // Wait two turns
            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            Assert.That(firstEnemy.Team, Is.EqualTo(Team.Enemy), "After 2 turns, enemy should revert to Enemy team");
            var summonAfter = firstEnemy.GetComponent<SummonedUnit>();
            Assert.That(summonAfter, Is.Null, "After revert, SummonedUnit component should be removed");
        }

        [UnityTest]
        public IEnumerator DominateDoesNotAffectBosses()
        {
            ((Enemy)firstEnemy).IsBoss = true;

            var dominateSkill = MakeSkill("Dominate", new DominateAction { Turns = 2 });
            dominateSkill.Targeting = SkillTargeting.Missile;
            dominateSkill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.All };
            dominateSkill.MissileRange = 10;
            Learn(dominateSkill);

            // Apply Fear to firstEnemy
            var fearPrefab = AssetDatabase.LoadAssetAtPath<FearStatusEffect>("Assets/Prefabs/Dungeon/StatusEffects/FearStatus.prefab");
            if (fearPrefab != null)
            {
                firstEnemy.ApplyStatusEffect(fearPrefab);
            }
            else
            {
                var fearObj = new GameObject();
                var fearEffect = fearObj.AddComponent<FearStatusEffect>();
                fearEffect.TurnsLeft = 5;
                firstEnemy.StatusEffects.Add(fearEffect);
            }

            var direction = SkillMovement.Step(caster.TilemapPosition, firstEnemy.TilemapPosition);

            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, dominateSkill, direction));
            yield return harness.WaitForIdle();

            Assert.That(firstEnemy.Team, Is.EqualTo(Team.Enemy), "Boss enemy should not be dominated");
            var summonUnit = firstEnemy.GetComponent<SummonedUnit>();
            Assert.That(summonUnit, Is.Null, "Boss enemy should not have SummonedUnit component");
        }

        [UnityTest]
        public IEnumerator DominateRequiresFear()
        {
            var dominateSkill = MakeSkill("Dominate", new DominateAction { Turns = 2 });
            dominateSkill.Targeting = SkillTargeting.Missile;
            dominateSkill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.All };
            dominateSkill.MissileRange = 10;
            Learn(dominateSkill);

            // Don't apply Fear - just cast
            var direction = SkillMovement.Step(caster.TilemapPosition, firstEnemy.TilemapPosition);

            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, dominateSkill, direction));
            yield return harness.WaitForIdle();

            Assert.That(firstEnemy.Team, Is.EqualTo(Team.Enemy), "Without Fear, enemy team should remain unchanged");
        }

        [UnityTest]
        public IEnumerator DominatedEnemyDoesNotCountForDefeat()
        {
            var dominateSkill = MakeSkill("Dominate", new DominateAction { Turns = 2 });
            dominateSkill.Targeting = SkillTargeting.Missile;
            dominateSkill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.All };
            dominateSkill.MissileRange = 10;
            Learn(dominateSkill);

            // Apply Fear to firstEnemy
            var fearPrefab = AssetDatabase.LoadAssetAtPath<FearStatusEffect>("Assets/Prefabs/Dungeon/StatusEffects/FearStatus.prefab");
            if (fearPrefab != null)
            {
                firstEnemy.ApplyStatusEffect(fearPrefab);
            }
            else
            {
                var fearObj = new GameObject();
                var fearEffect = fearObj.AddComponent<FearStatusEffect>();
                fearEffect.TurnsLeft = 5;
                firstEnemy.StatusEffects.Add(fearEffect);
            }

            var direction = SkillMovement.Step(caster.TilemapPosition, firstEnemy.TilemapPosition);

            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, dominateSkill, direction));
            yield return harness.WaitForIdle();

            var standingMembers = PartyRules.StandingMembers(harness.Game);
            Assert.That(standingMembers, Does.Not.Contains(firstEnemy), "Dominated enemy should not count as a party member");
            Assert.That(PartyRules.IsSummon(firstEnemy), Is.True, "Dominated enemy should be recognized as a summon");
        }
    }
}
#endif
