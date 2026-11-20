using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class GameTypographyTests
    {
        [TestCase(GameTextRole.Interface, "Marcellus-Regular")]
        [TestCase(GameTextRole.Heading, "UncialAntiqua-Regular")]
        [TestCase(GameTextRole.Combat, "GermaniaOne-Regular")]
        [TestCase(GameTextRole.Utility, "RobotoCondensed-Regular")]
        public void FontsHaveTheirProjectSourceAndBakedGlyphs(GameTextRole role, string source)
        {
            var font = GameTypography.Font(role);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.sourceFontFile.name, Is.EqualTo(source));
            Assert.That(font.atlasTextures[0].width, Is.GreaterThan(1));
            Assert.That(font.material.mainTexture, Is.SameAs(font.atlasTextures[0]));
            Assert.That(font.HasCharacters("Rowan 0123456789"), Is.True);
        }

        [Test]
        public void ProductionPrefabLabelsRetainSemanticFonts()
        {
            int count = 0;
            var roles = new System.Collections.Generic.HashSet<GameTextRole>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var label in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TMP_Text>(true))
                {
                    Assert.That(label.GetComponent<GameTextStyle>(), Is.Not.Null, path + "/" + label.name);
                    Assert.That(label.font, Is.SameAs(GameTypography.Expected(label)), path + "/" + label.name);
                    roles.Add(label.GetComponent<GameTextStyle>().Role); count++;
                }
            }
            Assert.That(count, Is.GreaterThan(100));
            Assert.That(roles.Count, Is.EqualTo(4));
            Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(GameTypography.Font(GameTextRole.Interface)));
            Assert.That(GameUITheme.Current.HeadingFont, Is.SameAs(GameTypography.Font(GameTextRole.Heading)));
        }
    }
}
