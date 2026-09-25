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
    public class FloorRevealTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<Skill> skills = new();
        private readonly List<Object> itemAssets = new();
        private Ally caster => harness.Ally;
        private Game game => harness.Game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AllyName = "Rowan" });
            caster.AllyStrategy = AllyStrategy.HoldPosition;
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            caster.SetPosition(center);
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
                inputScope.Dispose();
            }
        }

        private Skill MakeSkill(string name, SkillTargeting targeting, TargetTeam team, TargetArea area, params GameAction[] effects)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skill.SkillName = name;
            skill.ActivationType = ActivationType.Active;
            skill.SPCost = 0;
            skill.Targeting = targeting;
            skill.TargetSelector = new TargetSelector { Team = team, Area = area };
            skill.ActionEffects = effects.ToList();
            skills.Add(skill);
            caster.Skills.Add(skill);
            caster.InvalidateCachedStats();
            caster.SyncDisplayedStats();
            return skill;
        }

        private Skill MakePassive(string name, params PassiveResponse[] responses)
        {
            var skill = ScriptableObject.CreateInstance<Skill>();
            skill.SkillName = name;
            skill.ActivationType = ActivationType.Passive;
            skill.SPCost = 0;
            skill.Targeting = SkillTargeting.Self;
            skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
            skill.PassiveResponses = responses.ToList();
            skills.Add(skill);
            caster.Skills.Add(skill);
            caster.InvalidateCachedStats();
            caster.SyncDisplayedStats();
            return skill;
        }

        [UnityTest]
        public IEnumerator FloorSenseRevealsLayout()
        {
            var skill = MakeSkill("Floor Sense", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
                new RevealFloorLayoutAction());

            yield return harness.ExecuteAction(new SkillAction(caster, skill, caster));
            yield return harness.WaitForIdle();

            Assert.That(game.FloorReveal.LayoutRevealed, Is.True);

            var minimap = Object.FindFirstObjectByType<Minimap>();
            Assert.That(minimap, Is.Not.Null);
            for (int x = 0; x < minimap.dungeonMap.GetLength(0); x++)
            {
                for (int y = 0; y < minimap.dungeonMap.GetLength(1); y++)
                {
                    if (!minimap.dungeonMap[x, y].isWall)
                    {
                        Assert.That(minimap.dungeonMap[x, y].visibility,
                            Is.Not.EqualTo(Minimap.MinimapTileVisibility.Unseen),
                            $"Tile ({x}, {y}) should not be Unseen after floor reveal");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FarsightCountsDown()
        {
            var skill = MakeSkill("Farsight", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
                new RevealEnemiesAction { Turns = 3 });

            yield return harness.ExecuteAction(new SkillAction(caster, skill, caster));
            yield return harness.WaitForIdle();

            var turnsAfterCast = game.FloorReveal.EnemiesRevealedTurns;
            Assert.That(turnsAfterCast, Is.InRange(2, 3),
                "EnemiesRevealedTurns should be 2 or 3 right after cast");

            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            var turnsAfterFirstWait = game.FloorReveal.EnemiesRevealedTurns;

            yield return harness.ExecuteAction(new WaitAction());
            yield return harness.WaitForIdle();

            var turnsAfterSecondWait = game.FloorReveal.EnemiesRevealedTurns;

            var decrease = turnsAfterCast - turnsAfterSecondWait;
            Assert.That(decrease, Is.EqualTo(2),
                $"After two WaitActions, turns should decrease by exactly 2 (was {turnsAfterCast}, now {turnsAfterSecondWait})");
            Assert.That(turnsAfterSecondWait, Is.GreaterThanOrEqualTo(0),
                "EnemiesRevealedTurns should never go below 0");
        }

        [UnityTest]
        public IEnumerator FarsightDoesNotShortenLongerReveal()
        {
            game.FloorReveal.EnemiesRevealedTurns = 8;

            var skill = MakeSkill("Farsight", SkillTargeting.Self, TargetTeam.Self, TargetArea.Self,
                new RevealEnemiesAction { Turns = 3 });

            yield return harness.ExecuteAction(new SkillAction(caster, skill, caster));
            yield return harness.WaitForIdle();

            var turns = game.FloorReveal.EnemiesRevealedTurns;
            Assert.That(turns, Is.GreaterThanOrEqualTo(7),
                "EnemiesRevealedTurns should stay >= 7 when casting 3-turn reveal on 8-turn reveal");
        }

        [UnityTest]
        public IEnumerator RevealResetsOnNewFloor()
        {
            game.FloorReveal.LayoutRevealed = true;
            game.FloorReveal.EnemiesRevealedTurns = 5;
            game.FloorReveal.TreasureRevealed = true;

            game.AdvanceFloor();
            yield return harness.WaitForIdle();

            Assert.That(game.FloorReveal.LayoutRevealed, Is.False);
            Assert.That(game.FloorReveal.EnemiesRevealedTurns, Is.EqualTo(0));
            Assert.That(game.FloorReveal.TreasureRevealed, Is.False);
        }

        [UnityTest]
        public IEnumerator TreasureHunterRevealsTreasureEachFloor()
        {
            var treasurePassive = new RevealTreasurePassive();
            var treasureSkill = ScriptableObject.CreateInstance<Skill>();
            treasureSkill.SkillName = "Treasure Hunter";
            treasureSkill.ActivationType = ActivationType.Passive;
            treasureSkill.SPCost = 0;
            treasureSkill.Targeting = SkillTargeting.Self;
            treasureSkill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
            treasureSkill.PassiveResponses = new List<PassiveResponse> { treasurePassive };
            skills.Add(treasureSkill);
            caster.Skills.Add(treasureSkill);
            caster.InvalidateCachedStats();
            caster.SyncDisplayedStats();

            game.AdvanceFloor();
            yield return harness.WaitForIdle();

            Assert.That(game.FloorReveal.TreasureRevealed, Is.True);
        }
    }
}
#endif
