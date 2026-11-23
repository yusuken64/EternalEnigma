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
                    Assert.That(hero.Portrait.rect.width, Is.EqualTo(512));
                    Assert.That(AssetDatabase.GetAssetPath(hero.Portrait), Does.EndWith(hero.name + ".png"));
                }
                studio.Show(-1); Assert.That(studio.Index, Is.EqualTo(23));
                studio.Show(24); Assert.That(studio.Index, Is.Zero);
                Assert.That(studio.PortraitCamera.targetTexture, Is.SameAs(studio.Output));
                Assert.That(studio.transform.Cast<Transform>().Count(t => t.name == "Portrait model"), Is.EqualTo(1));
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(320, 180)]
        [TestCase(160, 360)]
        public void PortraitDisplayFitsSpriteRegionAndRestoresLiveView(int width, int height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<TownAlly>("Assets/Prefabs/Town/Allies/Ally_MC01.prefab");
            var hero = new GameObject("Hero"); hero.SetActive(false);
            var display = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FaceCamDisplay>("Assets/Prefabs/Menu/AllyImage.prefab"));
            var slot = (RectTransform)display.transform;
            slot.sizeDelta = new Vector2(width, height);
            var portrait = Sprite.Create(prefab.Portrait.texture, new Rect(12, 16, 60, 90), Vector2.one * .5f,
                100, 0, SpriteMeshType.FullRect);
            try
            {
                var data = hero.AddComponent<TownAlly>(); data.Portrait = portrait;
                var visual = new GameObject("Visual"); visual.transform.SetParent(hero.transform); visual.layer = 4;
                var image = display.GetComponentInChildren<RawImage>();
                var fitter = image.GetComponent<AspectRatioFitter>();
                float liveAspect = fitter.aspectRatio;
                image.texture = Texture2D.blackTexture;
                image.uvRect = new Rect(.1f,.2f,.7f,.6f);
                display.SetFollow(visual);
                Assert.That(image.texture, Is.SameAs(portrait.texture));
                var rect = portrait.textureRect;
                Assert.That(image.uvRect, Is.EqualTo(new Rect(rect.x / portrait.texture.width,
                    rect.y / portrait.texture.height, rect.width / portrait.texture.width, rect.height / portrait.texture.height)));
                float scale = Mathf.Min(width / rect.width, height / rect.height);
                Assert.That(image.rectTransform.rect.width, Is.EqualTo(rect.width * scale).Within(.01f));
                Assert.That(image.rectTransform.rect.height, Is.EqualTo(rect.height * scale).Within(.01f));
                Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(visual.layer, Is.EqualTo(4));
                display.Unfollow(visual);
                Assert.That(fitter.aspectRatio, Is.EqualTo(liveAspect));
                Assert.That(image.texture, Is.SameAs(Texture2D.blackTexture));
                Assert.That(image.uvRect, Is.EqualTo(new Rect(.1f,.2f,.7f,.6f)));
            }
            finally
            {
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(display.gameObject);
                Object.DestroyImmediate(portrait);
            }
        }
    }
}
