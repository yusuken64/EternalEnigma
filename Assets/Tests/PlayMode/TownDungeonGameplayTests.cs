#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TownDungeonGameplayTests
    {
        GameTestHarness harness;
        IDisposable preferences;
        bool? control;
        DungeonAnimationMode? animation;
        [UnitySetUp] public IEnumerator Setup()
        {
            preferences = GamePreferences.Isolate();
            control = DungeonPreferences.FullControlOverride; animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false; DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            yield return harness.LoadDungeon(new TestScenario { Seed = 42, Variant = DungeonVariant.Town, Tier = 2, EndFloor = 3 });
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return harness.Cleanup();
            DungeonPreferences.FullControlOverride = control; DungeonPreferences.AnimationOverride = animation;
            preferences?.Dispose();
        }

        [UnityTest] public IEnumerator DoorwaysSupportStraightMovementCombatSightAndRoofCutaways()
        {
            var dungeon = harness.Game.CurrentDungeon;
            var interior = dungeon.Floor.TownPlan.Interiors.First();
            var door = interior.Door.ToCell();
            var outside = door + Vector3Int.down;
            var inside = door + Vector3Int.up * 2;
            harness.PlaceAlly(outside);
            Assert.That(dungeon.CanWalkTo(door + new Vector3Int(-1,-1), door), Is.False);
            Assert.That(dungeon.CanWalkTo(door, door + new Vector3Int(1,-1)), Is.False);
            yield return harness.ExecuteAction(new MovementAction(harness.Ally, outside, door));
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(door));
            yield return harness.SpawnEnemy("Enemy_Slime", inside);
            var enemy = (Enemy)harness.Game.Enemies.Single(); enemy.Policies.Clear();
            enemy.BaseStats.HPMax = 100;
            enemy.BaseStats.HPRegenAcccumlateThreshold = 10000;
            enemy.InvalidateCachedStats(); enemy.Vitals.HP = 50; enemy.SyncDisplayedStats();
            Assert.That(MissileTargeting.TraceLine(harness.Ally, Vector3Int.up, 8).Encounters.Single().Character, Is.SameAs(enemy));
            Assert.That(dungeon.GetVisibleTiles(harness.Ally, door), Does.Contain(inside));
            var wallApproach = door + Vector3Int.left;
            Assert.That(MissileTargeting.TraceLineFrom(harness.Ally, wallApproach, Vector3Int.up, 8).Endpoint, Is.EqualTo(wallApproach));
            harness.Game.UpdateMiniMap(); yield return null;
            var visibility = dungeon.GetComponentInParent<TownDungeonVisibility>() ?? Object.FindFirstObjectByType<TownDungeonVisibility>();
            visibility.Refresh();
            var roof = visibility.GetComponentsInChildren<TownRoofVisual>(true).Single(r => r.Room.Door.Equals(interior.Door));
            Assert.That(roof.gameObject.activeSelf, Is.False, "Visible interior combat must not be covered by its roof.");
            var skill = ScriptableObject.CreateInstance<Skill>();
            try
            {
                skill.SkillName = "Doorway test"; skill.Targeting = SkillTargeting.SelectedTarget;
                skill.TargetSelector = new TargetSelector { Team = TargetTeam.Enemies, Area = TargetArea.Visible };
                skill.ActionEffects = new List<GameAction> { new TakeDamageAction { damage = 7 } };
                harness.Ally.Skills.Add(skill);
                yield return harness.ExecuteSkillToCompletion(new SkillAction(harness.Ally, skill, enemy));
                Assert.That(enemy.Vitals.HP, Is.EqualTo(43));
                yield return Capture("gameplay-doorway");
            }
            finally { harness.Ally.Skills.Remove(skill); Object.Destroy(skill); }
            harness.Game.Enemies.Remove(enemy); Object.Destroy(enemy.gameObject); yield return null;
            foreach (var cell in new[] { door + Vector3Int.up, inside })
            {
                yield return harness.ExecuteAction(new MovementAction(harness.Ally, harness.Ally.TilemapPosition, cell));
                Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(cell));
            }
            Assert.That(harness.Game.PartyVisibleTiles, Does.Contain(inside));
            yield return Capture("gameplay-interior");
        }

        [UnityTest] public IEnumerator GatesKeepProgressionLootBossClearanceAndReleaseOldGeometry()
        {
            Assert.That(harness.Game.CurrentDungeon.IsThroneFloor, Is.True);
            for (int expected = 2; expected <= 3; expected++)
            {
                var old = Object.FindFirstObjectByType<TownDungeonVisibility>().GetComponentsInChildren<EnvironmentMeshOwner>(true)
                    .SelectMany(o => o.Meshes).Where(m => m != null).ToArray();
                harness.PlaceBesideStairs();
                var gate = harness.Game.CurrentDungeon.Interactables.OfType<Stairs>().Single();
                Assert.That(gate.GetInteractionText(), Is.EqualTo("Pass through gate"));
                Assert.That(gate.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
                yield return Capture("gameplay-gate-" + (expected - 1));
                yield return harness.ExecuteAction(new InteractAction(gate)); yield return null;
                Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(expected));
                Assert.That(harness.Game.CurrentDungeon.Floor.Variant, Is.EqualTo(DungeonVariant.Town));
                Assert.That(old.All(m => m == null), Is.True, "Transition must release old generated meshes.");
                if (expected != 2) continue;
                foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
                harness.Game.Enemies.Clear(); yield return null;
                var dungeon = harness.Game.CurrentDungeon;
                var pickup = dungeon.Interactables.OfType<DroppedItem>().First(); var item = pickup.InventoryItem;
                harness.PlaceAlly(pickup.Position); yield return null;
                Assert.That(pickup.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !r.forceRenderingOff), Is.True);
                yield return harness.ExecuteAction(new PickUpItemAction(pickup));
                Assert.That(harness.Game.PlayerController.Inventory.InventoryItems, Does.Contain(item));
                var square = TownDungeonGenerator.BossSquare(dungeon.Floor.TownPlan);
                Assert.That(square.Cells().All(p => dungeon.IsWalkable(p.ToCell())), Is.True);
                var boss = Object.Instantiate(LargeBossBehavior.Prefab("Golem"), harness.Game.transform);
                boss.InitialzeVitalsFromStats(); boss.SetPosition(new Vector3Int(square.X + 2, square.Y + 2));
                harness.Game.Enemies.Add(boss); yield return null; boss.Policies.Clear();
                Assert.That(boss.FootPrint, Is.EqualTo(FootPrint.Size3x3));
                Assert.That(SkillMovement.CanOccupy(boss, boss.TilemapPosition), Is.True);
                Assert.That(MovementAction.FootprintCanStep(boss, boss.TilemapPosition, boss.TilemapPosition + Vector3Int.right), Is.True);
                harness.Game.Enemies.Remove(boss); Object.Destroy(boss.gameObject); yield return null;
            }
            Assert.That(harness.Game.CurrentDungeon.IsExitFloor, Is.True);
            Assert.That(harness.Game.CurrentDungeon.IsThroneFloor, Is.True);
        }

        static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory("Temp/TownDungeon");
            // Let the gameplay camera settle after direct harness placement.
            yield return new WaitForSecondsRealtime(.25f);
            ScreenCapture.CaptureScreenshot("Temp/TownDungeon/" + name + ".png");
            yield return new WaitForSecondsRealtime(.1f);
        }
    }
}
#endif
