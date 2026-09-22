#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class EnemyPrefabTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { yield return harness.Cleanup(); }

        [UnityTest]
        public IEnumerator EveryEnemyPrefabTargetsAttacksAndAnimates()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(0));
            var dungeon = harness.Game.CurrentDungeon;
            var center = Enumerable.Range(3, dungeon.dungeonWidth-6)
                .SelectMany(x => Enumerable.Range(3, dungeon.dungeonHeight-6).Select(y => new Vector3Int(x,y)))
                .First(p => Enumerable.Range(-2,5).All(x => Enumerable.Range(-2,5)
                    .All(y => dungeon.IsWalkable(p + new Vector3Int(x,y)))));
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.Team, Is.EqualTo(Team.Enemy), path);
                Assert.That(prefab.StartingStats.Strength, Is.GreaterThan(0), path);
                Assert.That(prefab.StartingStats.ActionsPerTurnMax, Is.GreaterThan(0), path);
                Assert.That(prefab.StartingStats.AttacksPerTurnMax, Is.GreaterThan(0), path);
                int radius = prefab.FootPrint == FootPrint.Size3x3 ? 2 : 1;
                harness.PlaceAlly(center + Vector3Int.right * radius);
                yield return harness.SpawnEnemy(prefab.name, center);
                var enemy = (Enemy)harness.Game.Enemies.Single();
                Assert.That(enemy.Animator, Is.Not.Null, path);
                Assert.That(enemy.Animator.runtimeAnimatorController, Is.Not.Null, path);
                AttackAction attack = null;
                // Special policies can occasionally replace a normal attack.
                for (int turn=0; turn<64 && attack == null; turn++)
                {
                    enemy.StartTurn(); enemy.DetermineAction();
                    Assert.That(enemy.PursuitTarget, Is.SameAs(harness.Ally), path);
                    attack = enemy.GetDeterminedAction().OfType<AttackAction>().FirstOrDefault();
                }
                Assert.That(attack, Is.Not.Null, path + " never selected an attack");
                var effects = attack.ExecuteImmediate(enemy).OfType<TakeDamageAction>().ToArray();
                Assert.That(effects.Length, Is.EqualTo(1), path);
                Assert.That(effects[0].target, Is.SameAs(harness.Ally), path);
                Assert.That(effects[0].damage, Is.GreaterThan(0), path);
                Assert.That(enemy.Vitals.AttacksPerTurnLeft, Is.EqualTo(enemy.FinalStats.AttacksPerTurnMax-1), path);
                Assert.That(enemy.WalkAnimationState, Is.Not.Null, path);
                foreach (var state in new[] { "Attack", "IdleNormal", "GetHit", "Die" })
                    Assert.That(enemy.AnimationStates(state), Is.Not.Empty, path + ": " + state);
                enemy.PlayWalkAnimation(); yield return null;
                yield return attack.ExecuteRoutine(enemy, false);
                enemy.PlayTakeDamageAnimation(); yield return null;
                enemy.PlayDeathAnimation(); yield return null;
                Debug.Log("Enemy prefab verified: " + prefab.name);
                harness.Game.Enemies.Remove(enemy); Object.Destroy(enemy.gameObject); yield return null;
            }
        }
    }
}
#endif
