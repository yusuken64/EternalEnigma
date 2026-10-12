#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class GameplayFollowupTests
    {
        GameTestHarness harness;
        TestInputScope inputs;
        Vector3Int center;
        DungeonAnimationMode? oldAnimations;
        bool? oldControl;
        Enemy Last => (Enemy)harness.Game.Enemies.Last();
        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); oldAnimations = DungeonPreferences.AnimationOverride;
            oldControl = DungeonPreferences.FullControlOverride; DungeonPreferences.FullControlOverride = false;
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            harness = new GameTestHarness(); yield return harness.LoadDungeon(new TestScenario { SP = 50 });
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            var dungeon = harness.Game.CurrentDungeon;
            center = Enumerable.Range(3, dungeon.dungeonWidth - 6).SelectMany(x => Enumerable.Range(3, dungeon.dungeonHeight - 6).Select(y => new Vector3Int(x, y)))
                .First(p => Enumerable.Range(-2, 5).All(x => Enumerable.Range(-2, 5).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            harness.PlaceAlly(center); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { yield return harness.Cleanup(); inputs.Dispose(); DungeonPreferences.AnimationOverride = oldAnimations; DungeonPreferences.FullControlOverride = oldControl; }

        [UnityTest] public IEnumerator DragonRollsOnceOnlyForValidShotsAndDealsFire()
        {
            yield return harness.SpawnEnemy("Enemy_Dragon", center + Vector3Int.right * 2);
            var dragon = Last; dragon.StartTurn(); dragon.IsDormant = false; dragon.PursuitTarget = harness.Ally;
            var behavior = dragon.GetComponent<EnemyBehavior>();
            Assert.That(behavior.FireBreath && behavior.RangedAttack, Is.True);
            Assert.That(behavior.RangedAttackChance, Is.EqualTo(.5f));
            Random.InitState(791); var expected = Enumerable.Range(0, 512).Select(_ => Random.value < .5f).ToArray();
            var afterRolls = Random.state; Random.InitState(791);
            Assert.That(Enumerable.Range(0, 512).Select(_ => behavior.ChooseAction() != null), Is.EqualTo(expected));
            Assert.That(Random.state, Is.EqualTo(afterRolls));
            harness.Ally.SetPosition(center + Vector3Int.up);
            var before = Random.state;
            Assert.That(behavior.ChooseAction(), Is.Null); Assert.That(Random.state, Is.EqualTo(before));
            harness.Ally.SetPosition(center);
            var silence = dragon.gameObject.AddComponent<SilenceStatusEffect>(); silence.TurnsLeft = 2; dragon.StatusEffects.Add(silence);
            Assert.That(behavior.ChooseAction(), Is.Null); Assert.That(Random.state, Is.EqualTo(before));
            dragon.StatusEffects.Remove(silence); Object.Destroy(silence);
            GameAction shot = null;
            for (int i = 0; shot == null && i < 32; i++) shot = behavior.ChooseAction();
            Assert.That(shot, Is.Not.Null);
            var damage = dragon.ExecuteActionImmediate(shot).OfType<TakeDamageAction>().Single();
            Assert.That(damage.Element, Is.EqualTo(DamageElement.Fire));
            Assert.That(shot.Visuals.Sequence.Profile, Is.SameAs(CombatVisualCatalog.Instance.FireBreath));
            Assert.That(shot.Visuals.Sequence.Profile.CastSound, Is.Not.Null);
            Assert.That(shot.Visuals.Sequence.Profile.Projectile.Prefab, Is.Not.Null);
            dragon.Vitals.AttacksPerTurnLeft = 1; before = Random.state;
            Assert.That(behavior.ChooseAction(), Is.Null, "Cooldown prevents another attempt.");
            Assert.That(Random.state, Is.EqualTo(before));
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("breathes a fireball")), Is.True);
        }

        [UnityTest] public IEnumerator WerewolfPackMembersMoveTwiceButHaveOneAttack()
        {
            harness.PlaceAlly(center + Vector3Int.left * 2);
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Werewolf.prefab");
            var pack = harness.Game.EnemyManager.SpawnPack(prefab, center + Vector3Int.right * 2, new System.Collections.Generic.HashSet<Vector3Int>());
            Assert.That(pack.Count, Is.EqualTo(3));
            foreach (var wolf in pack)
            {
                wolf.StartTurn(); Assert.That(wolf.Vitals.ActionsPerTurnLeft, Is.EqualTo(2));
                Assert.That(wolf.Vitals.AttacksPerTurnLeft, Is.EqualTo(1));
            }
            var leader = pack[0]; var start = leader.TilemapPosition;
            leader.IsDormant = false; leader.PursuitTarget = harness.Ally;
            for (int i = 0; i < 2; i++)
            {
                leader.DetermineAction(); var move = leader.GetDeterminedAction().Single();
                Assert.That(move, Is.TypeOf<MovementAction>()); leader.ExecuteActionImmediate(move);
                yield return move.ExecuteRoutine(leader, true);
            }
            Assert.That(leader.Vitals.ActionsPerTurnLeft, Is.Zero);
            Assert.That(TileWorldDungeon.ChevDistance(start, leader.TilemapPosition), Is.EqualTo(2));
            Assert.That(leader.Vitals.AttacksPerTurnLeft, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MimicRevealAndSpiderSplitReplayEffectsSoundAndHistory()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            yield return harness.SpawnEnemy("Enemy_ChestMonster", center + Vector3Int.right);
            var mimic = Last;
            var hit = new TakeDamageAction(harness.Ally, mimic, 0, false, true);
            harness.Ally.ExecuteActionImmediate(hit);
            Assert.That(EnemyBehavior.IsDisguised(mimic), Is.False);
            var effects = CombatEffectPlayer.Get(); effects.Clear();
            yield return hit.Visuals.Play(hit, harness.Ally, false);
            Assert.That(effects.ActiveCount, Is.GreaterThan(0), "The reveal replay spawns its authored cue.");
            System.IO.Directory.CreateDirectory("Temp/TodoVerification");
            ScreenCapture.CaptureScreenshot("Temp/TodoVerification/mimic-reveal.png"); yield return new WaitForEndOfFrame();
            Assert.That(CombatVisualCatalog.Instance.Reveal.CastSound, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("was a mimic")), Is.True);
            effects.Clear();
            yield return harness.SpawnEnemy("Enemy_Spider", center + Vector3Int.up);
            var spider = Last;
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Spider.prefab");
            var split = new SpawnEnemyAction(prefab, center + Vector3Int.left, spider.VisualParent.transform.position, spider);
            spider.ExecuteActionImmediate(split);
            Assert.That(split.SpawnedEnemy, Is.Not.Null);
            foreach (var audio in AudioManager.Instance.EffectAudioSources) audio.Stop();
            var replay = spider.StartCoroutine(split.Visuals.Play(split, spider, false));
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(AudioManager.Instance.EffectAudioSources.Any(s => s.isPlaying), Is.True);
            yield return replay;
            Assert.That(split.SpawnedEnemy.VisualParent.transform.localScale, Is.EqualTo(spider.VisualParent.transform.localScale));
            Assert.That(Vector3.Distance(split.SpawnedEnemy.VisualParent.transform.localPosition, spider.VisualParent.transform.localPosition), Is.LessThan(.001f));
            Assert.That(CombatVisualCatalog.Instance.Duplication.CastSound, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GameMessages>().History.Any(s => s.Contains("split into two")), Is.True);
        }

        [UnityTest] public IEnumerator ThrowUsesAuthoredGestureAndCellCentersRegardlessOfFacingVisualOffset()
        {
            DungeonPreferences.AnimationOverride = DungeonAnimationMode.Normal;
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right * 2);
            var target = Last;
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Prefabs/Dungeon/Items/Potion_HealPotion.asset").AsInventoryItem(null);
            harness.Game.PlayerController.Inventory.Add(item);
            harness.Ally.SetFacing(Facing.Right);
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere); projectile.name = "Throw verification"; projectile.SetActive(false);
            var action = new ThrowItemAction(harness.Game.PlayerController.Inventory, harness.Ally, item, projectile) { LookAt = true };
            harness.Ally.ExecuteActionImmediate(action);
            var dungeon = harness.Game.CurrentDungeon;
            var halfCell = (dungeon.CellToWorld(Vector3Int.right) - dungeon.CellToWorld(Vector3Int.zero)).x / 2;
            Assert.That(action.ProjectileStart, Is.EqualTo(dungeon.CellToWorld(center) + new Vector3(halfCell, halfCell)));
            Assert.That(action.ProjectileEnd, Is.EqualTo(dungeon.CellToWorld(target.TilemapPosition) + new Vector3(halfCell, halfCell)));
            var animator = harness.Ally.HeroAnimator; var stance = animator.CurrentStance;
            var routine = action.ExecuteRoutine(harness.Ally);
            Assert.That(routine.MoveNext(), Is.True); animator.Animator.Update(0);
            Assert.That(animator.LastPlayedAction, Is.EqualTo(AnimatedAction.Throw));
            Assert.That(animator.Animator.GetCurrentAnimatorClipInfo(0).Single().clip.name, Does.StartWith("CarryThrow_"));
            while (routine.MoveNext()) yield return routine.Current;
            Assert.That(animator.CurrentStance, Is.EqualTo(stance)); Assert.That(animator.LastPlayedAction, Is.EqualTo(AnimatedAction.Idle));
            Assert.That(GameObject.Find("Throw verification(Clone)"), Is.Null);
            Object.Destroy(projectile);
        }

        [UnityTest] public IEnumerator FindExitWalksThroughProductionTurnsAndDoesNotChangeFloors()
        {
            var skill = Resources.Load<Skill>("Classes/Skills/Scout/Find Exit");
            harness.Ally.Skills.Add(skill); harness.Ally.Vitals.SP = 20; harness.Ally.SyncDisplayedStats();
            var floor = harness.Game.CurrentDungeon; int number = harness.Game.PlayerController.Floor;
            Assert.That(ScoutExitNavigation.CanBegin(harness.Ally), Is.True);
            yield return harness.ExecuteAction(new SkillAction(harness.Ally, skill, harness.Ally));
            yield return harness.WaitUntil(() => harness.Ally.GetComponent<ScoutExitNavigation>()?.Active == false, "Scout reaches exit");
            yield return harness.WaitForIdle();
            Assert.That(harness.Ally.TilemapPosition, Is.EqualTo(floor.Floor.Stairs.ToCell()));
            Assert.That(harness.Game.CurrentDungeon, Is.SameAs(floor)); Assert.That(harness.Game.PlayerController.Floor, Is.EqualTo(number));
            Assert.That(harness.Ally.Vitals.SP, Is.LessThanOrEqualTo(18));
        }

        [UnityTest] public IEnumerator FindExitRejectsDangerAndStopsForDamageOrManualCommands()
        {
            var skill = Resources.Load<Skill>("Classes/Skills/Scout/Find Exit"); harness.Ally.Skills.Add(skill);
            var navigate = new FindExitAction(); navigate.ExecuteImmediate(harness.Ally);
            var guide = harness.Ally.GetComponent<ScoutExitNavigation>(); Assert.That(guide.Active, Is.True);
            harness.Ally.GetResponseTo(new TakeDamageAction(harness.Ally, harness.Ally, 1)).ToList(); Assert.That(guide.Active, Is.False);
            navigate.ExecuteImmediate(harness.Ally); harness.Ally.SetAction(new WaitAction());
            harness.Ally.PrepareManualAction(); Assert.That(guide.Active, Is.False);
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            int mana = harness.Ally.Vitals.SP;
            var cast = new SkillAction(harness.Ally, skill, harness.Ally);
            Assert.That(cast.IsValid(harness.Ally), Is.False); Assert.That(cast.ExecuteImmediate(harness.Ally), Is.Empty);
            Assert.That(harness.Ally.Vitals.SP, Is.EqualTo(mana));
        }

        [UnityTest] public IEnumerator FindExitGuidesAFollowerWhileTheLeaderMoves()
        {
            var town = Object.Instantiate(TownSceneLoader.Default.AllyCatalog.First(h => h.Id != harness.Ally.TownAllyId));
            var follower = Object.Instantiate(harness.Game.AllyPrefab, harness.Game.transform);
            follower.InitialzeModel(town); follower.InitialzeVitalsFromStats(); follower.SyncDisplayedStats();
            follower.SetPosition(center + Vector3Int.right * 2); harness.Game.Allies.Add(follower);
            Object.Destroy(town.gameObject); yield return null;
            new FindExitAction().ExecuteImmediate(follower);
            var guide = follower.GetComponent<ScoutExitNavigation>(); Assert.That(guide.Active, Is.True);
            var start = follower.TilemapPosition;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                JuicyChickenGames.Menu.PlayerInputHandler.Instance.PlayerInput.SwitchCurrentControlScheme(keyboard);
                yield return null; // Release the command before the leader moves.
                var leaderStart = harness.Ally.TilemapPosition;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
                yield return harness.WaitUntil(() => harness.Ally.TilemapPosition != leaderStart, "leader keyboard movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                yield return harness.WaitForIdle();
                Assert.That(follower.TilemapPosition, Is.Not.EqualTo(start));
                Assert.That(guide.Active, Is.True, "The leader's input must not cancel a different Scout's route.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
#endif
