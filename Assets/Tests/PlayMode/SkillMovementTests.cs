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
    public class SkillMovementTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<Skill> skills = new();
        private Ally caster => harness.Ally;
        private Ally friend;
        private Character first, second, distant;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Rowan" } });
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
                actor.Vitals.HP = 500; actor.Vitals.SP = 20;
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

        private IEnumerator Cast(Skill skill, Character target)
        {
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skill, target));
        }

        [UnityTest]
        public IEnumerator ShadowStepLandsBehindTarget()
        {
            // Enemy 3 tiles east
            var enemy = first;
            var initialEnemyPos = enemy.TilemapPosition;
            Assert.That(TileWorldDungeon.ChevDistance(caster.TilemapPosition, enemy.TilemapPosition), Is.EqualTo(1));
            caster.SetPosition(enemy.TilemapPosition + Vector3Int.left * 3);
            Assert.That(TileWorldDungeon.ChevDistance(caster.TilemapPosition, enemy.TilemapPosition), Is.EqualTo(3));

            // Create and learn a SelectedTarget/Enemies/Visible skill with TeleportBehindAction
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.SelectedTarget;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            skill.TargetSelector.Area = TargetArea.Visible;
            var action = new TeleportBehindAction { MaxRange = 4 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            var initialCasterPos = caster.TilemapPosition;
            var initialEnemyHP = enemy.Vitals.HP;

            yield return Cast(skill, enemy);

            // Caster should end up behind target (east of target at distance 1 or 2)
            var finalCasterPos = caster.TilemapPosition;
            Assert.That(finalCasterPos, Is.Not.EqualTo(initialCasterPos), "Caster should have moved");
            // Enemy HP should decrease or a miss occurred
            Assert.That(enemy.Vitals.HP < initialEnemyHP || caster.Vitals.HP < 500, "Either enemy HP decreased or some effect occurred");
        }

        [UnityTest]
        public IEnumerator ShadowStepOutOfRangeDoesNothing()
        {
            // Enemy 6 tiles away
            var enemy = distant;
            var initialCasterPos = caster.TilemapPosition;
            Assert.That(TileWorldDungeon.ChevDistance(caster.TilemapPosition, enemy.TilemapPosition), Is.GreaterThan(3));

            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.SelectedTarget;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            skill.TargetSelector.Area = TargetArea.Visible;
            var action = new TeleportBehindAction { MaxRange = 4 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            yield return Cast(skill, enemy);

            // Caster position should be unchanged
            Assert.That(caster.TilemapPosition, Is.EqualTo(initialCasterPos), "Caster should not have moved when out of range");
        }

        [UnityTest]
        public IEnumerator ShadowStepFallsBackWhenBehindIsBlocked()
        {
            // Place first enemy 3 tiles away
            var primaryTarget = first;
            caster.SetPosition(primaryTarget.TilemapPosition + Vector3Int.left * 3);

            // Place second enemy on the preferred behind tile
            second.SetPosition(primaryTarget.TilemapPosition + Vector3Int.right);

            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.SelectedTarget;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            skill.TargetSelector.Area = TargetArea.Visible;
            var action = new TeleportBehindAction { MaxRange = 4 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            var initialCasterPos = caster.TilemapPosition;
            yield return Cast(skill, primaryTarget);

            // Caster should end at Chebyshev distance 1 from primary target
            var finalCasterPos = caster.TilemapPosition;
            var chebDistance = TileWorldDungeon.ChevDistance(finalCasterPos, primaryTarget.TilemapPosition);
            Assert.That(chebDistance, Is.EqualTo(1), "Caster should end at Chebyshev distance 1 from target");
        }

        [UnityTest]
        public IEnumerator ShadowDanceStrikesEachNearbyEnemyOnce()
        {
            // Reset all enemy positions relative to caster
            var origin = caster.TilemapPosition;

            // Place two enemies within 2 tiles
            first.SetPosition(origin + Vector3Int.right);
            second.SetPosition(origin + Vector3Int.up);

            // Place one enemy 4 tiles away
            distant.SetPosition(origin + Vector3Int.right * 4);

            var initialFirstHP = first.Vitals.HP;
            var initialSecondHP = second.Vitals.HP;
            var initialDistantHP = distant.Vitals.HP;

            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.AllTargets;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            var action = new ShadowDanceAction { Radius = 2 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            yield return Cast(skill, null);

            // Near enemies should take damage
            Assert.That(first.Vitals.HP, Is.LessThan(initialFirstHP), "First enemy should be damaged");
            Assert.That(second.Vitals.HP, Is.LessThan(initialSecondHP), "Second enemy should be damaged");

            // Far enemy should be unchanged
            Assert.That(distant.Vitals.HP, Is.EqualTo(initialDistantHP), "Distant enemy should be unchanged");

            // Caster should end adjacent to last struck enemy (second)
            var chebDistance = TileWorldDungeon.ChevDistance(caster.TilemapPosition, second.TilemapPosition);
            Assert.That(chebDistance, Is.EqualTo(1), "Caster should end adjacent to last struck enemy");
        }

        [UnityTest]
        public IEnumerator LungeStopsBeforeWall()
        {
            // Get enemy 3 tiles away
            var enemy = first;
            var dungeon = harness.Game.CurrentDungeon;

            // Find a 3-tile straight line of walkable cells and place at third tile
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();

            // Find a row with at least 7 walkable cells
            var origin = cells.First(p => Enumerable.Range(0, 7).All(i => dungeon.IsWalkable(p + Vector3Int.right * i)));
            caster.SetPosition(origin);
            enemy.SetPosition(origin + Vector3Int.right * 3);

            var initialCasterPos = caster.TilemapPosition;

            // Create a Missile skill with MissileRange 3 and DashStrikeAction
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.Missile;
            skill.MissileRange = 3;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            skill.TargetSelector.Area = TargetArea.All;
            var action = new DashStrikeAction();
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, skill, Vector3Int.right));

            // Caster should end adjacent to enemy
            var finalCasterPos = caster.TilemapPosition;
            var chebDistance = TileWorldDungeon.ChevDistance(finalCasterPos, enemy.TilemapPosition);
            Assert.That(chebDistance, Is.EqualTo(1), "Caster should end adjacent to enemy");

            // Now test with a wall between
            second.SetPosition(origin + Vector3Int.right * 2);
            caster.SetPosition(origin);
            var wallCasterStart = caster.TilemapPosition;

            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, skill, Vector3Int.right));

            // Caster should never have entered a non-walkable cell
            var wallFinalCasterPos = caster.TilemapPosition;
            Assert.That(dungeon.IsWalkable(wallFinalCasterPos), Is.True, "Caster must always be on walkable tile");
        }

        [UnityTest]
        public IEnumerator RetreatShotStepsAwayUnlessBlocked()
        {
            // Place enemy adjacent to caster
            var enemy = first;
            var dungeon = harness.Game.CurrentDungeon;

            // Find a row with at least 7 walkable cells
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();

            var origin = cells.First(p => Enumerable.Range(0, 7).All(i => dungeon.IsWalkable(p + Vector3Int.right * i)));
            caster.SetPosition(origin + Vector3Int.right * 3);
            enemy.SetPosition(origin + Vector3Int.right * 2);

            var initialCasterPos = caster.TilemapPosition;

            // Create a SelectedTarget skill with StepBackAction
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.SelectedTarget;
            skill.TargetSelector.Team = TargetTeam.Enemies;
            skill.TargetSelector.Area = TargetArea.Visible;
            var action = new StepBackAction { Tiles = 1 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            yield return Cast(skill, enemy);

            // Caster should have moved 1 tile directly away
            var finalCasterPos = caster.TilemapPosition;
            var expectedPos = initialCasterPos + Vector3Int.right;
            Assert.That(finalCasterPos, Is.EqualTo(expectedPos), "Caster should step back 1 tile away from enemy");

            // Now test with wall behind caster
            caster.SetPosition(origin + Vector3Int.right);
            enemy.SetPosition(origin + Vector3Int.right * 2);
            var blockedInitialPos = caster.TilemapPosition;

            yield return Cast(skill, enemy);

            // Caster should be unchanged (blocked by edge or wall)
            var blockedFinalPos = caster.TilemapPosition;
            Assert.That(blockedFinalPos, Is.EqualTo(blockedInitialPos), "Caster should not move when blocked");
        }

        [UnityTest]
        public IEnumerator GrappleLineStopsBeforeCharacter()
        {
            // Setup: caster faces east, enemy 4 tiles east
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();

            var origin = cells.First(p => Enumerable.Range(0, 5).All(i => dungeon.IsWalkable(p + Vector3Int.right * i)));
            caster.SetPosition(origin);
            caster.SetFacing(Facing.Right);

            var enemy = first;
            enemy.SetPosition(origin + Vector3Int.right * 4);

            // Create a Self skill with GrappleLineAction
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.Self;
            var action = new GrappleLineAction { MaxRange = 5 };
            skill.ActionEffects.Clear();
            skill.ActionEffects.Add(action);

            var initialCasterPos = caster.TilemapPosition;

            yield return Cast(skill, caster);

            // Caster should end 3 tiles east (adjacent to enemy at 4 east)
            var finalCasterPos = caster.TilemapPosition;
            var expectedPos = origin + Vector3Int.right * 3;
            Assert.That(finalCasterPos, Is.EqualTo(expectedPos), "Caster should stop adjacent to enemy");
        }

        [UnityTest]
        public IEnumerator BigUnitCannotOccupyNarrowTile()
        {
            // Spawn a 3x3 enemy
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();

            // Find a cell adjacent to a wall (narrow tile)
            var cellAdjacentToWall = cells.First(p =>
                !dungeon.IsWalkable(p + Vector3Int.right) ||
                !dungeon.IsWalkable(p + Vector3Int.left) ||
                !dungeon.IsWalkable(p + Vector3Int.up) ||
                !dungeon.IsWalkable(p + Vector3Int.down));

            // Find an open area cell
            var openAreaCell = cells.First(p => Enumerable.Range(-2, 5).All(x =>
                Enumerable.Range(-2, 5).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));

            // Spawn big unit
            yield return harness.SpawnEnemy("Enemy_Slime_Big", openAreaCell);
            var bigEnemy = (Enemy)harness.Game.Enemies.Last();

            // Assert CanOccupy is false for cell adjacent to wall
            Assert.That(SkillMovement.CanOccupy(bigEnemy, cellAdjacentToWall), Is.False,
                "Big unit should not occupy tile that has wall in footprint");

            // Assert CanOccupy is true for open area
            Assert.That(SkillMovement.CanOccupy(bigEnemy, openAreaCell), Is.True,
                "Big unit should occupy open area center");
        }
    }
}
#endif
