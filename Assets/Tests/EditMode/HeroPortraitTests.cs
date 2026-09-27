using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    public class HeroPortraitTests
    {
        [Test] public void EveryHeroHasItsOwnSavedPortraitAndStudioCycles()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/HeroPortraitStudio.unity", OpenSceneMode.Additive);
            try
            {
                var studio = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HeroPortraitStudio>()).Single();
                Assert.That(studio.Heroes.Length, Is.EqualTo(24));
                Assert.That(studio.Heroes.Select(h => h.Portrait).Distinct().Count(), Is.EqualTo(24));
                foreach (var hero in studio.Heroes)
                {
                    Assert.That(hero.Portrait, Is.Not.Null, hero.name);
                    Assert.That(hero.Portrait.texture.width, Is.EqualTo(512));
                    Assert.That(AssetDatabase.GetAssetPath(hero.Portrait), Does.EndWith(hero.name + ".png"));
                }
                studio.Show(-1); Assert.That(studio.Index, Is.EqualTo(23));
                studio.Show(24); Assert.That(studio.Index, Is.Zero);
                Assert.That(studio.PortraitCamera.targetTexture, Is.SameAs(studio.Output));
                Assert.That(studio.transform.Cast<Transform>().Count(t => t.name == "Portrait model"), Is.EqualTo(1));
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }

        [Test] public void PortraitDisplayUsesSavedImageWithoutChangingModelLayers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<TownAlly>("Assets/Prefabs/Town/Allies/Ally_MC01.prefab");
            var hero = new GameObject("Hero"); hero.SetActive(false);
            var displayObject = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
            try
            {
                var data = hero.AddComponent<TownAlly>(); data.Portrait = prefab.Portrait;
                var visual = new GameObject("Visual"); visual.transform.SetParent(hero.transform); visual.layer = 4;
                var display = displayObject.AddComponent<FaceCamDisplay>();
                var image = displayObject.GetComponent<RawImage>();
                image.texture = Texture2D.blackTexture;
                display.SetFollow(visual);
                Assert.That(image.texture, Is.SameAs(prefab.Portrait.texture));
                Assert.That(visual.layer, Is.EqualTo(4));
                display.Unfollow(visual);
                Assert.That(image.texture, Is.SameAs(Texture2D.blackTexture));
            }
            finally { Object.DestroyImmediate(hero); Object.DestroyImmediate(displayObject); }
        }
    }
}
