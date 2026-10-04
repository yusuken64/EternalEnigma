using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DungeonCombatAnimationLoopTests
{
    [Test]
    public void DungeonAllyAndEnemyCombatClipsHaveTheExpectedLoopSettings()
    {
        var heroPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town/Allies" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("Ally_MC", StringComparison.Ordinal))
            .Append("Assets/Prefabs/Dungeon/Player.prefab")
            .ToArray();
        Assert.That(heroPaths.Length, Is.GreaterThan(1));
        var allyClipCount = 0;
        foreach (var path in heroPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var heroAnimator = prefab.GetComponentInChildren<HeroAnimator>(true);
            Assert.That(heroAnimator, Is.Not.Null, path);
            Assert.That(heroAnimator.StanceAnimations, Is.Not.Empty, path);
            foreach (var stance in heroAnimator.StanceAnimations)
                foreach (var action in new[] { AnimatedAction.Attack, AnimatedAction.GetHit, AnimatedAction.Die })
                {
                    var clips = stance.NamedAnimations.Single(mapping => mapping.AnimationAction == action).Animations;
                    Assert.That(clips, Is.Not.Empty, $"{path}: {stance.Stance} {action}");
                    foreach (var clip in clips)
                    {
                        AssertLoopSetting(clip, path);
                        allyClipCount++;
                    }
                }
        }
        Assert.That(allyClipCount, Is.GreaterThan(0));

        var enemyPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        Assert.That(enemyPaths, Is.Not.Empty);
        var enemyClipCount = 0;
        foreach (var path in enemyPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var animator = prefab.Animator;
            Assert.That(animator?.runtimeAnimatorController, Is.Not.Null, path);
            var clips = animator.runtimeAnimatorController.animationClips
                .Where(clip => IsCombatClip(clip.name)).Distinct().ToArray();
            Assert.That(clips, Is.Not.Empty, path);
            foreach (var clip in clips)
            {
                AssertLoopSetting(clip, path);
                enemyClipCount++;
            }
        }
        Assert.That(enemyClipCount, Is.GreaterThan(0));
    }

    private static bool IsCombatClip(string name) =>
        name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("GetHit", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void AssertLoopSetting(AnimationClip clip, string prefabPath)
    {
        Assert.That(clip, Is.Not.Null, prefabPath);
        var sustainedDeathPose = clip.name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 clip.name.IndexOf("Stay", StringComparison.OrdinalIgnoreCase) >= 0;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        Assert.That(settings.loopTime, Is.EqualTo(sustainedDeathPose),
            $"{prefabPath}: {AssetDatabase.GetAssetPath(clip)} ({clip.name})");
    }
}
