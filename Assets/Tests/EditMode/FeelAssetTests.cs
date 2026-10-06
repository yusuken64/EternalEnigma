using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class FeelAssetTests
    {
        [Test]
        public void HeroPoolsContainPlayableClipsOnly()
        {
            foreach (var path in new[] { "Assets/Prefabs/Dungeon/Player.prefab", "Assets/Prefabs/Town/TownPlayer.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var hero in prefab.GetComponentsInChildren<HeroAnimator>(true))
                foreach (var stance in hero.StanceAnimations)
                {
                    foreach (var action in new[] { AnimatedAction.Idle, AnimatedAction.Attack, AnimatedAction.MoveFWD, AnimatedAction.GetHit, AnimatedAction.Die })
                        Assert.That(stance.NamedAnimations.First(a => a.AnimationAction == action).Animations.Count, Is.GreaterThan(0), path + " " + stance.Stance + " " + action);
                    foreach (var clip in stance.NamedAnimations.SelectMany(a => a.Animations))
                    {
                        Assert.That(clip.name, Does.Not.Contain("Stay").IgnoreCase, path + " " + stance.Stance);
                        Assert.That(clip.name, Does.Not.Contain("_Start").IgnoreCase, path + " " + stance.Stance);
                        Assert.That(clip.name, Does.Not.Contain("_Maintain").IgnoreCase, path + " " + stance.Stance);
                    }
                }
            }
        }

        [Test]
        public void StatusesAndThemesHaveDistinctAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CombatVisualCatalog>("Assets/Resources/CombatEffects/Catalog.asset");
            Assert.That(catalog.Statuses.All(s => s.Profile != null && s.Profile.Icon != null));
            Assert.That(catalog.Statuses.Select(s => s.Profile.Icon).Distinct().Count(), Is.EqualTo(catalog.Statuses.Count));
            var themes = AssetDatabase.LoadAssetAtPath<DungeonThemeCatalog>("Assets/Resources/DungeonThemes/Catalog.asset");
            Assert.That(themes.FallbackMusic, Is.Not.Null);
            foreach (var theme in themes.Themes)
            {
                Assert.That(theme.Music, Is.Not.Null, theme.Name);
                Assert.That(theme.BossMusic, Is.Not.Null, theme.Name);
                Assert.That(theme.Decorations.Length, Is.GreaterThanOrEqualTo(4), theme.Name);
            }
        }
    }
}
