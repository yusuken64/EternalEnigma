#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class EnemySpawnVisualTests
    {
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario()); }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest]
        public IEnumerator ActiveAndDormantSpawnsUseVariedFacingsAndMatchingVisualRotations()
        {
            var normal = new HashSet<Facing>(); var dormant = new HashSet<Facing>();
            var dungeon = harness.Game.CurrentDungeon;
            var cell = dungeon.GetStartPosition();
            foreach (var name in new[] { "Enemy_Slime", "Enemy_EvilMage" })
            for (int i = 0; i < 24; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/" + name + ".prefab");
                var action = new SpawnEnemyAction(prefab, cell, dungeon.CellToWorld(cell));
                action.ExecuteImmediate(harness.Ally);
                var routine = action.ExecuteRoutine(harness.Ally, true); while (routine.MoveNext()) yield return routine.Current;
                yield return null;
                var enemy = action.SpawnedEnemy;
                Assert.That(Enum.IsDefined(typeof(Facing), enemy.CurrentFacing), Is.True);
                var rotation = enemy.VisualParent.transform.rotation;
                enemy.SetFacing(enemy.CurrentFacing);
                Assert.That(Quaternion.Angle(rotation, enemy.VisualParent.transform.rotation), Is.LessThan(.01f));
                (enemy.IsDormant ? dormant : normal).Add(enemy.CurrentFacing);
                harness.Game.Enemies.Remove(enemy); Object.Destroy(enemy.gameObject);
            }
            Assert.That(normal.Count, Is.GreaterThan(1), "Active enemies must not share one fixed spawn facing.");
            Assert.That(dormant.Count, Is.GreaterThan(1), "Dormant enemies also receive random spawn facings.");
        }

        [UnityTest]
        public IEnumerator MimicDisguiseKeepsSouthFacingAndSizeThroughSpawnScaling()
        {
            var dungeon = harness.Game.CurrentDungeon;
            var cell = dungeon.GetStartPosition();
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_ChestMonster.prefab");
            var action = new SpawnEnemyAction(prefab, cell, dungeon.CellToWorld(cell));
            action.ExecuteImmediate(harness.Ally);
            yield return null; // Initialize while the spawn visual still has zero scale.
            var routine = action.ExecuteRoutine(harness.Ally, true); while (routine.MoveNext()) yield return routine.Current;
            var enemy = action.SpawnedEnemy;
            yield return null;
            var chest = enemy.GetComponentsInChildren<Transform>().Single(t => t.name == "Treasure chest disguise");
            foreach (Facing facing in Enum.GetValues(typeof(Facing)))
            {
                enemy.SetFacing(facing);
                enemy.VisualParent.transform.localScale = Vector3.one;
                yield return null; yield return null;
                Assert.That(Quaternion.Angle(chest.rotation, Quaternion.identity), Is.LessThan(.01f));
                Assert.That(chest.GetComponentInChildren<DungeonPickupFootprint>().GroundZ, Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.001f));
                enemy.VisualParent.transform.localScale = Vector3.one * 2;
                yield return null; yield return null;
                Assert.That(chest.GetComponentInChildren<DungeonPickupFootprint>().GroundZ, Is.EqualTo(DungeonPresentation.GroundPlaneZ).Within(.001f));
            }
            var disguised = chest.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
            enemy.GetComponent<EnemyBehavior>().Provoke();
            enemy.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return null;
            enemy.SetFacing(Facing.Down); enemy.PlayIdleAnimation(); enemy.Animator.speed = 0;
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DungeonPropVisualAuthoring")).First(t => t != null);
            var points = (Vector3[])authoring.GetMethod("EnemyPoints").Invoke(null, new object[] { enemy.gameObject });
            var actual = new Bounds(points[0], Vector3.zero); foreach (var point in points) actual.Encapsulate(point);
            Assert.That(Vector3.Distance(disguised.size, actual.size), Is.LessThan(.02f), $"The disguise and revealed enemy retain the same size: disguise={disguised.size}, enemy={actual.size}, idle={enemy.Animator.GetCurrentAnimatorClipInfo(0)[0].clip.name}.");
        }

        [UnityTest]
        public IEnumerator StatueMatchesLiveSouthFacingEnemy()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_BlackKnight.prefab");
            var enemy = Object.Instantiate(prefab, harness.Game.transform);
            enemy.InitialzeVitalsFromStats(); enemy.TilemapPosition = harness.Game.CurrentDungeon.GetStartPosition();
            harness.Game.Enemies.Add(enemy); yield return null;
            enemy.SetFacing(Facing.Down); enemy.PlayIdleAnimation(); enemy.Animator.speed = 0;
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DungeonPropVisualAuthoring")).First(t => t != null);
            var points = (Vector3[])authoring.GetMethod("EnemyPoints").Invoke(null, new object[] { enemy.gameObject });
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
            var statue = Resources.Load<GameObject>("DungeonProps/StoneHulkStatue").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(Vector3.Distance(statue.bounds.size, bounds.size), Is.LessThan(.02f), $"statue={statue.bounds.size} enemy={bounds.size}");
        }
    }
}
#endif
