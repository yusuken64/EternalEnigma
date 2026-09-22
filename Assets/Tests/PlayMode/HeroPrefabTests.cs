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
    public sealed class HeroPrefabTests
    {
        private GameTestHarness harness;
        private readonly List<GameObject> spawned = new();
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var obj in spawned) if (obj != null) Object.Destroy(obj);
            yield return harness.Cleanup();
        }

        [UnityTest]
        public IEnumerator EveryHeroSupportsTownAnimationsDungeonTransferAndCombat()
        {
            var heroes = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town/Allies" })
                .Select(g => AssetDatabase.LoadAssetAtPath<TownAlly>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.name.StartsWith("Ally_MC", StringComparison.Ordinal)).OrderBy(p => p.name).ToArray();
            Assert.That(heroes.Length, Is.GreaterThan(0));
            Assert.That(heroes.Select(h => h.Id).Distinct().Count(), Is.EqualTo(heroes.Length));
            Assert.That(TownSceneLoader.Default.AllyCatalog.All(heroes.Contains), Is.True,
                "Every recruitable hero must be included in the audit; TownAlly is an authoring template.");
            var dungeon = harness.Game.CurrentDungeon;
            var center = Enumerable.Range(3, dungeon.dungeonWidth-6)
                .SelectMany(x => Enumerable.Range(3, dungeon.dungeonHeight-6).Select(y => new Vector3Int(x,y)))
                .First(p => Enumerable.Range(-2,5).All(x => Enumerable.Range(-2,5)
                    .All(y => dungeon.IsWalkable(p + new Vector3Int(x,y)))));
            harness.PlaceAlly(center + Vector3Int.left * 2);
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            var target = harness.Game.Enemies.Single();
            foreach (var prefab in heroes)
            {
                string label = prefab.name + " (" + prefab.Name + ")";
                Assert.That(prefab.Id, Is.Not.Empty, label);
                Assert.That(prefab.Name, Is.Not.Empty, label);
                Assert.That(prefab.HeroAnimator, Is.Not.Null, label);
                Assert.That(prefab.AnimatedModel, Is.Not.Null, label);
                var town = Object.Instantiate(prefab); spawned.Add(town.gameObject);
                var animation = town.HeroAnimator;
                Assert.That(animation.Animator.transform.IsChildOf(town.AnimatedModel.transform), Is.True, label);
                Assert.That(animation.Animator.applyRootMotion, Is.False, label);
                Assert.That(town.Equipment, Is.Not.Null, label);
                Assert.That(town.CirlcleRenderer, Is.Not.Null, label);
                foreach (var hand in animation.RightHandObjects.Concat(animation.LeftHandObjects))
                    Assert.That(hand != null && hand.transform.IsChildOf(town.AnimatedModel.transform), Is.True, label + " weapon reference");
                foreach (var weapon in Common.Instance.ItemManager.ItemDefinitions.OfType<EquipmentItemDefinition>()
                    .Where(item => !string.IsNullOrEmpty(item.WeaponModelName)))
                {
                    bool offhand = weapon.EquipmentSlot == EquipmentSlot.OffHand;
                    animation.SetWeapon(offhand ? null : weapon, offhand ? weapon : null);
                    var models = offhand ? animation.LeftHandObjects : animation.RightHandObjects;
                    Assert.That(models.Count(model => model.activeSelf && model.name == weapon.WeaponModelName),
                        Is.EqualTo(1), label + " " + weapon.ItemName + " model " + weapon.WeaponModelName);
                    if (weapon.WeaponType == WeaponType.BowAndArrow)
                        Assert.That(animation.CurrentStance, Is.EqualTo(Stance.BowAndArrowStance), label + " bow stance");
                }
                foreach (Stance stance in Enum.GetValues(typeof(Stance)))
                {
                    animation.CurrentStance = stance;
                    var definition = animation.StanceAnimations.Single(s => s.Stance == stance);
                    Assert.That(definition.AnimatorController, Is.Not.Null, label + " " + stance);
                    foreach (var action in new[] { AnimatedAction.Idle, AnimatedAction.MoveFWD,
                        AnimatedAction.Attack, AnimatedAction.GetHit, AnimatedAction.Die })
                    {
                        var clips = definition.NamedAnimations.Single(a => a.AnimationAction == action).Animations;
                        Assert.That(clips, Is.Not.Empty, label + " " + stance + " " + action);
                        animation.PlayAnimation(action); animation.Animator.Update(0);
                        foreach (var clip in clips)
                            Assert.That(clip != null && animation.Animator.HasState(0, Animator.StringToHash(clip.name)),
                                Is.True, label + " " + stance + " " + action + " " + (clip != null ? clip.name : "missing clip"));
                    }
                    yield return null;
                }
                animation.SetWeapon(null, null);
                Assert.That(animation.CurrentStance, Is.EqualTo(Stance.NoWeapon), label);
                Assert.That(animation.RightHandObjects.Concat(animation.LeftHandObjects).Any(o => o.activeSelf), Is.False, label);
                town.SetFacing(Facing.Down); animation.PlayWalkAnimation(); animation.PlayIdleAnimation();

                var ally = Object.Instantiate(harness.Game.AllyPrefab); spawned.Add(ally.gameObject);
                ally.InitialzeModel(town); ally.InitialzeVitalsFromStats(); ally.SyncDisplayedStats();
                ally.SetPosition(center); harness.Game.Allies.Add(ally);
                Object.Destroy(town.gameObject); yield return null;
                Assert.That(ally.Team, Is.EqualTo(Team.Player), label);
                Assert.That(ally.Vitals.HP, Is.GreaterThan(0), label);
                Assert.That(ally.HeroAnimator.Animator.transform.IsChildOf(ally.transform), Is.True, label + " transferred model");
                Assert.That(ally.HeroAnimator.Animator.applyRootMotion, Is.False, label);
                ally.StartTurn(); ally.DetermineAction();
                var attack = ally.GetDeterminedAction().OfType<AttackAction>().Single();
                var damage = attack.ExecuteImmediate(ally).OfType<TakeDamageAction>().Single();
                Assert.That(damage.target, Is.SameAs(target), label);
                Assert.That(damage.damage, Is.GreaterThan(0), label);
                yield return attack.ExecuteRoutine(ally, false);
                ally.Inventory_HandleInventoryChanged();
                ally.PlayWalkAnimation(); ally.PlayTakeDamageAnimation(); ally.PlayDeathAnimation();
                Debug.Log("Hero prefab verified: " + label + " | 8 stances | town/dungeon transfer | attack");
                harness.Game.Allies.Remove(ally); Object.Destroy(ally.gameObject); yield return null;
            }
        }
    }
}
#endif
