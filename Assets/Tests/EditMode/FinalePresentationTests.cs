using EternalEnigma.Core.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public sealed class FinalePresentationTests
    {
        [Test]
        public void MimicsPreserveTheSharedCombatScaleAndNativeModelProportions()
        {
            var ordinary = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Skeleton.prefab");
            foreach (var family in FinaleRoster.Required)
            {
                bool town = System.Linq.Enumerable.Contains(FinaleRoster.Towns, family);
                string stem = FinaleRoster.PrefabStem(family);
                var enemy = Resources.Load<Enemy>("Finale/" + (town ? "Boss_" : "Enemy_") + stem);
                Assert.That(enemy, Is.Not.Null, family);
                Assert.That(enemy.VisualParent.transform.localScale, Is.EqualTo(ordinary.VisualParent.transform.localScale), family);
                Assert.That(enemy.VisualParent.transform.Find(stem).localScale, Is.EqualTo(Vector3.one), family);
                Assert.That(enemy.FootPrint, Is.EqualTo(FootPrint.Size1x1), family);
            }
        }
    }
}
