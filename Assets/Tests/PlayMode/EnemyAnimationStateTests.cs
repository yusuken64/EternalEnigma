#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyAnimationStateTests
{
    private GameObject messageRoot;
    [SetUp] public void SetupMessages()
    {
        messageRoot = new GameObject("Mimic test messages");
        messageRoot.SetActive(false);
        messageRoot.AddComponent<GameMessages>().AuthorLayout(true);
        messageRoot.SetActive(true);
    }
    [TearDown] public void CleanupMessages() => Object.DestroyImmediate(messageRoot);
    [Test]
    public void EnemyPrefabsResolveTheirRequiredAnimationStates()
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        Assert.That(paths, Is.Not.Empty);
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
            if (prefab == null) continue;
            var enemy = Object.Instantiate(prefab);
            try
            {
                Assert.That(enemy.WalkAnimationState, Is.Not.Null, path);
                foreach (var action in new[] { "IdleNormal", "Attack", "GetHit", "Die" })
                    Assert.That(enemy.AnimationStates(action), Is.Not.Empty, path + ": " + action);
            }
            finally { Object.DestroyImmediate(enemy.gameObject); }
        }
    }

    [Test]
    public void ChestMonsterStaysDormantUntilPickupAttemptAndRevealsWhenMoved()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_ChestMonster.prefab");
        var allyPrefab = AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Dungeon/Ally.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(allyPrefab, Is.Not.Null);
        var enemy = Object.Instantiate(prefab);
        var ally = Object.Instantiate(allyPrefab);
        enemy.InitialzeVitalsFromStats();
        try
        {
            var behavior = enemy.GetComponent<EnemyBehavior>();
            Assert.That(behavior, Is.Not.Null);
            enemy.TilemapPosition = Vector3Int.right;
            ally.TilemapPosition = Vector3Int.zero;
            behavior.Initialize(enemy);
            Assert.That(behavior.Disguised, Is.True);
            Assert.That(enemy.IsDormant, Is.True);
            Assert.That(enemy.Animator.gameObject.activeInHierarchy, Is.False);
            Assert.DoesNotThrow(() => enemy.PlayIdleAnimation());

            var pickup = new RevealMimicAction(enemy);
            ally.TilemapPosition = Vector3Int.left * 2;
            Assert.That(pickup.IsValid(ally), Is.False);
            Assert.That(behavior.Disguised, Is.True);
            ally.TilemapPosition = Vector3Int.zero;
            Assert.That(pickup.IsValid(ally), Is.True);
            pickup.ExecuteImmediate(ally);
            Assert.That(behavior.Disguised, Is.False);
            Assert.That(enemy.IsDormant, Is.False);
            Assert.That(enemy.PursuitTarget, Is.SameAs(ally));
            Assert.That(enemy.Animator.gameObject.activeInHierarchy, Is.True);
            Assert.That(enemy.AnimationStates("IdleNormal"), Is.Not.Empty);
            Assert.DoesNotThrow(() => enemy.PlayIdleAnimation());
        }
        finally { Object.DestroyImmediate(ally.gameObject); Object.DestroyImmediate(enemy.gameObject); }

        enemy = Object.Instantiate(prefab);
        enemy.InitialzeVitalsFromStats();
        try
        {
            var behavior = enemy.GetComponent<EnemyBehavior>();
            behavior.Initialize(enemy);
            Assert.That(enemy.IsDormant, Is.True);
            behavior.RevealIfMoved();
            Assert.That(behavior.Disguised, Is.False);
            Assert.That(enemy.IsDormant, Is.False);
        }
        finally { Object.DestroyImmediate(enemy.gameObject); }
    }

    [Test]
    public void ChestMonsterRevealsWhenAttackedEvenIfTheAttackMisses()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_ChestMonster.prefab");
        var allyPrefab = AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Dungeon/Ally.prefab");
        var enemy = Object.Instantiate(prefab);
        var ally = Object.Instantiate(allyPrefab);
        enemy.InitialzeVitalsFromStats();
        try
        {
            var behavior = enemy.GetComponent<EnemyBehavior>();
            behavior.Initialize(enemy);
            Assert.That(behavior.Disguised, Is.True);
            Assert.That(enemy.IsDormant, Is.True);
            enemy.WakeFrom(new MovementAction(ally, Vector3Int.left, Vector3Int.zero));
            Assert.That(behavior.Disguised, Is.True);
            Assert.That(enemy.IsDormant, Is.True);
            enemy.WakeFrom(new TakeDamageAction(ally, enemy, 0, false, true));
            Assert.That(behavior.Disguised, Is.False);
            Assert.That(enemy.IsDormant, Is.False);
        }
        finally { Object.DestroyImmediate(ally.gameObject); Object.DestroyImmediate(enemy.gameObject); }
    }
}
#endif
