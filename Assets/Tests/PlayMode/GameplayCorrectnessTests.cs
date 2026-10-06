#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class GameplayCorrectnessTests
    {
        GameTestHarness harness;
        Vector3Int center;
        Enemy Last => (Enemy)harness.Game.Enemies.Last();
        [UnitySetUp] public IEnumerator Setup()
        {
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario());
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            var dungeon = harness.Game.CurrentDungeon;
            center = Enumerable.Range(3, dungeon.dungeonWidth - 6)
                .SelectMany(x => Enumerable.Range(3, dungeon.dungeonHeight - 6).Select(y => new Vector3Int(x, y)))
                .First(p => Enumerable.Range(-2, 5).All(x => Enumerable.Range(-2, 5).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            harness.Ally.SetPosition(center);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();
        void Resolve(Character actor, GameAction action)
        {
            var queue = new Queue<GameAction>(); queue.Enqueue(action);
            int count = 0;
            while (queue.Count > 0)
            {
                Assert.That(++count, Is.LessThan(100));
                foreach (var effect in actor.ExecuteActionImmediate(queue.Dequeue())) queue.Enqueue(effect);
            }
        }

        [UnityTest] public IEnumerator ExperienceAndMultipleGrowthsFollowRecipientInsteadOfActor()
        {
            yield return harness.SpawnEnemy("Enemy_Slime", center + Vector3Int.right);
            var actor = Last; var recipient = harness.Ally;
            actor.Vitals.Level = 10; actor.Vitals.Exp = 700;
            recipient.Vitals.Level = 1; recipient.Vitals.Exp = 0;
            var growth = HeroClass.Growth(recipient.PrimaryClass);
            int hp = recipient.BaseStats.HPMax, strength = recipient.BaseStats.Strength;
            int actorHp = actor.BaseStats.HPMax;
            Resolve(actor, new AddXPAction(recipient, 30));
            Assert.That(recipient.Vitals.Level, Is.EqualTo(3));
            Assert.That(recipient.Vitals.Exp, Is.EqualTo(30));
            Assert.That(recipient.BaseStats.HPMax, Is.EqualTo(hp + growth.HPMax * 2));
            Assert.That(recipient.BaseStats.Strength, Is.EqualTo(strength + growth.Strength * 2));
            Assert.That(actor.Vitals.Level, Is.EqualTo(10));
            Assert.That(actor.Vitals.Exp, Is.EqualTo(700));
            Assert.That(actor.BaseStats.HPMax, Is.EqualTo(actorHp));
            Assert.That(recipient.PendingAttributePoints,Is.EqualTo(2));
            Assert.That(recipient.AttributePromptPending,Is.True);
            yield return null;
            Assert.That(MenuManager.Instance.CurrentDialog,Is.TypeOf<LevelUpChoiceDialog>());
            ((LevelUpChoiceDialog)MenuManager.Instance.CurrentDialog).LaterButton.onClick.Invoke();
            yield return null;
            Assert.That(recipient.AttributePromptPending,Is.False);
            Assert.That(recipient.PendingAttributePoints,Is.EqualTo(2));
            Assert.That(MenuManager.Instance.CurrentDialog,Is.Not.TypeOf<LevelUpChoiceDialog>());
            Assert.That(AttributeSpending.TrySpend(recipient,HeroAttribute.Str),Is.True);
            Assert.That(recipient.PendingAttributePoints,Is.EqualTo(1));
            Resolve(actor, new AddXPAction(null, 30));
        }

        [UnityTest] public IEnumerator TurtleDeathExplodesOnceAndSilenceSuppressesIt()
        {
            yield return harness.SpawnEnemy("Enemy_TurtleShell", center + Vector3Int.right);
            var turtle = Last;
            var death = new DeathAction(turtle, harness.Ally);
            turtle.Vitals.HP = 0;
            var effects = death.ExecuteImmediate(harness.Ally);
            Assert.That(effects.OfType<TakeDamageAction>().Any(a => a.Target == harness.Ally && a.Damage == 20), Is.True);
            Assert.That(death.ExecuteImmediate(harness.Ally), Is.Empty);
            Assert.That(new DeathAction(turtle, harness.Ally).ExecuteImmediate(harness.Ally), Is.Empty);
            yield return harness.SpawnEnemy("Enemy_TurtleShell", center + Vector3Int.left);
            turtle = Last;
            var silence = turtle.gameObject.AddComponent<SilenceStatusEffect>(); silence.TurnsLeft = 3;
            turtle.StatusEffects.Add(silence); turtle.Vitals.HP = 0;
            Assert.That(new DeathAction(turtle, harness.Ally).ExecuteImmediate(harness.Ally).OfType<TakeDamageAction>(), Is.Empty);
        }

        [UnityTest] public IEnumerator TurtleExplosionsChainThroughRealDamageAndDeathResolution()
        {
            harness.Ally.Vitals.Level = 37;
            harness.Ally.BaseStats.HPMax = 200; harness.Ally.UpdateCachedStats(); harness.Ally.Vitals.HP = 200;
            yield return harness.SpawnEnemy("Enemy_TurtleShell", center + Vector3Int.right);
            var first = Last;
            yield return harness.SpawnEnemy("Enemy_TurtleShell", center + Vector3Int.right + Vector3Int.up);
            var second = Last; second.Vitals.HP = 1;
            int before = harness.Ally.Vitals.HP;
            Resolve(harness.Ally, new TakeDamageAction(harness.Ally, first, 99999));
            Assert.That(harness.Game.DeadUnits, Does.Contain(first));
            Assert.That(harness.Game.DeadUnits, Does.Contain(second));
            Assert.That(harness.Ally.Vitals.HP, Is.EqualTo(before - 40));
        }

        [UnityTest] public IEnumerator MetalSlimeWarpsOnlyAfterPositiveSurvivingHitToSafeCell()
        {
            yield return harness.SpawnEnemy("Enemy_MetalSlime", center + Vector3Int.right);
            var slime = Last; var behavior = slime.GetComponent<EnemyBehavior>();
            Assert.That(behavior.WarpWhenHitChance, Is.EqualTo(.25f));
            behavior.WarpWhenHitChance = 1;
            slime.Vitals.HP = 100;
            Assert.That(new TakeDamageAction(harness.Ally, slime, 0).ExecuteImmediate(harness.Ally), Is.Empty);
            Assert.That(new TakeDamageAction(harness.Ally, slime, 1, false, true).ExecuteImmediate(harness.Ally), Is.Empty);
            var before = slime.TilemapPosition;
            var warp = new TakeDamageAction(harness.Ally, slime, 1).ExecuteImmediate(harness.Ally).OfType<WarpAction>().Single();
            warp.ExecuteImmediate(harness.Ally);
            Assert.That(slime.TilemapPosition, Is.Not.EqualTo(before));
            Assert.That(DungeonPlacement.Fits(harness.Game.CurrentDungeon, slime, slime.TilemapPosition), Is.True);
            Assert.That(DungeonPlacement.Reachable(harness.Game.CurrentDungeon, before), Does.Contain(slime.TilemapPosition));
            var bind = slime.gameObject.AddComponent<StuckStatusEffect>(); bind.TurnsLeft = 3;
            slime.StatusEffects.Add(bind); before = slime.TilemapPosition;
            Assert.That(new TakeDamageAction(harness.Ally, slime, 1).ExecuteImmediate(harness.Ally).OfType<WarpAction>(), Is.Empty);
            new WarpAction(slime).ExecuteImmediate(harness.Ally);
            Assert.That(slime.TilemapPosition, Is.EqualTo(before));
            Assert.That(new TakeDamageAction(harness.Ally, slime, 99999).ExecuteImmediate(harness.Ally).OfType<WarpAction>(), Is.Empty);
        }

        [UnityTest] public IEnumerator WolvesFormSafePacksAndNessieOwnsRootAttack()
        {
            var wolf = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Werewolf.prefab");
            var reserved = new HashSet<Vector3Int> { center + Vector3Int.right * 2 };
            var pack = harness.Game.EnemyManager.SpawnPack(wolf, center + Vector3Int.right, reserved);
            Assert.That(pack.Count, Is.EqualTo(3));
            Assert.That(pack.Select(e => e.TilemapPosition).Distinct().Count(), Is.EqualTo(3));
            foreach (var member in pack)
            {
                Assert.That(member.TilemapPosition, Is.Not.EqualTo(center + Vector3Int.right * 2));
                Assert.That(DungeonPlacement.Fits(harness.Game.CurrentDungeon, member, member.TilemapPosition), Is.True);
            }
            yield return harness.SpawnEnemy("Enemy_WormMonster", center + Vector3Int.left);
            var nessie = Last; nessie.StartTurn(); nessie.IsDormant = false; nessie.PursuitTarget = harness.Ally;
            var root = nessie.GetComponent<EnemyBehavior>().ChooseAction();
            Assert.That(root, Is.Not.Null);
            Resolve(nessie, root);
            Assert.That(harness.Ally.StatusEffects.OfType<GoopiRootStatusEffect>().Any(r => r.Source == nessie && r.Holding), Is.True);
            var skeleton = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Skeleton.prefab");
            Assert.That(skeleton.GetComponent<EnemyBehavior>().RootsAdjacentTarget, Is.False);
            var crowdedOrigin = center + Vector3Int.up * 2;
            var crowded = new HashSet<Vector3Int>(harness.Game.CurrentDungeon.GetWalkableNeighborhoodTiles(crowdedOrigin));
            Assert.That(harness.Game.EnemyManager.SpawnPack(wolf, crowdedOrigin, crowded).Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator WeightedSelectionMakesMetalSlimeTwentyTimesRarer()
        {
            var manager = harness.Game.EnemyManager;
            var previous = manager.SpawnDefinitions;
            var metal = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_MetalSlime.prefab");
            var normal = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
            try
            {
                manager.SpawnDefinitions = new() {
                    new() { SpawnName = "Metal", FloorMin = 1, FloorMax = 27, EnemyCharacterPrefab = metal },
                    new() { SpawnName = "Normal", FloorMin = 1, FloorMax = 27, EnemyCharacterPrefab = normal } };
                Assert.That(Enumerable.Range(0, 2100).Count(i => manager.GetEnemyPrefab(1, i) == metal), Is.EqualTo(100));
                Assert.That(manager.GetEnemyPrefab(1, -1), Is.Not.Null);
            }
            finally { manager.SpawnDefinitions = previous; }
            yield return null;
        }

        [UnityTest] public IEnumerator SelectingActiveSkillOpensTargetingDirectlyAndCancelCostsNothing()
        {
            var actor = harness.Ally;
            var skill = Object.Instantiate(Common.Instance.SkillManager.GetSkillByName("Damage"));
            try
            {
                skill.Targeting = SkillTargeting.Missile; skill.SPCost = 1;
                actor.Skills = new() { skill }; actor.Vitals.SP = actor.FinalStats.SPMax;
                int sp = actor.Vitals.SP;
                var manager = MenuManager.Instance;
                manager.OpenPartyMenu(PartyMenuTab.Skills); yield return null;
                int actions = actor.Vitals.ActionsPerTurnLeft;
                manager.PartyMenu.EntryButtons.Single().onClick.Invoke();
                yield return null;
                Assert.That(manager.TargetDialog.gameObject.activeInHierarchy, Is.True);
                Assert.That(actor.Vitals.SP, Is.EqualTo(sp));
                manager.TargetDialog.CloseDialog(); yield return null;
                Assert.That(manager.PartyMenu.IsRoot, Is.True);
                Assert.That(actor.Vitals.SP, Is.EqualTo(sp));
                Assert.That(actor.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
                manager.CloseAllMenus();
            }
            finally { Object.Destroy(skill); }
        }
    }
}
#endif
