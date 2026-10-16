using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    public sealed class TerminalMenuPresentationTests
    {
        [Test]
        public void ControlFeedbackAndValueIndicatorsStayVisibleAndRestore()
        {
            var root = new GameObject("Terminal style fixture", typeof(RectTransform));
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            var style = new TerminalMenuStyle();
            try
            {
                var buttonImage = Image("Button", root.transform);
                buttonImage.sprite = sprite;
                buttonImage.color = Color.blue;
                var button = buttonImage.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonImage;
                var originalColors = button.colors;
                var close = Image("Close symbol", button.transform);
                close.sprite = sprite;
                var toggleImage = Image("Toggle", root.transform);
                var toggle = toggleImage.gameObject.AddComponent<Toggle>();
                toggle.targetGraphic = toggleImage;
                var checkmark = Image("Checkmark", toggle.transform);
                checkmark.sprite = sprite;
                toggle.graphic = checkmark;
                var bar = Image("HP fill", root.transform);
                bar.sprite = sprite;
                bar.type = UnityEngine.UI.Image.Type.Filled;
                bar.fillAmount = .35f;
                bar.color = Color.red;
                var portrait = Image("Portrait", root.transform);
                portrait.sprite = sprite;
                portrait.preserveAspect = true;
                portrait.color = Color.cyan;
                var sliderImage = Image("Slider handle", root.transform);
                var slider = sliderImage.gameObject.AddComponent<Slider>();
                slider.targetGraphic = sliderImage;

                Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                Assert.That(buttonImage.color, Is.EqualTo(Color.white), "Button tints must not multiply a dark background color.");
                Assert.That(button.colors.selectedColor.g, Is.GreaterThan(.3f));
                Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.selectedColor));
                Assert.That(slider.colors.normalColor.g, Is.GreaterThan(.5f), "Unfocused slider handles remain visible.");
                Assert.That(close.sprite, Is.SameAs(sprite), "The close symbol must not become a solid rectangle.");
                Assert.That(checkmark.sprite, Is.SameAs(sprite));
                Assert.That(checkmark.color.g, Is.GreaterThan(toggle.colors.normalColor.g));
                Assert.That(bar.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
                Assert.That(bar.fillAmount, Is.EqualTo(.35f));
                Assert.That(bar.color, Is.EqualTo(Color.red));
                Assert.That(portrait.sprite, Is.SameAs(sprite));
                Assert.That(portrait.color, Is.EqualTo(Color.cyan));

                style.Restore();
                Assert.That(buttonImage.sprite, Is.SameAs(sprite));
                Assert.That(buttonImage.color, Is.EqualTo(Color.blue));
                Assert.That(button.colors, Is.EqualTo(originalColors));
                Assert.That(checkmark.color, Is.EqualTo(Color.white));
            }
            finally
            {
                style.Restore();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AuthoredDungeonCloseSymbolKeepsReadableFeedbackAndRestores()
        {
            var source = GameUITheme.Current.DungeonMenuPrefab.GetComponentsInChildren<DungeonDialogClose>(true).Single();
            var root = Object.Instantiate(source.gameObject);
            var style = new TerminalMenuStyle();
            try
            {
                var button = root.GetComponent<Button>();
                var symbol = (UnityEngine.UI.Image)button.targetGraphic;
                var sprite = symbol.sprite;
                var colors = button.colors;
                var color = symbol.color;
                Assert.That(symbol.GetComponent<DungeonUIRole>().Role, Is.EqualTo(DungeonVisualRole.Close));
                Assert.That(sprite, Is.Not.Null);

                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                Assert.That(symbol.sprite, Is.SameAs(sprite), "The shared close artwork remains the button's visible symbol.");
                Assert.That(symbol.color, Is.EqualTo(Color.white));
                Assert.That(button.colors.normalColor.g, Is.GreaterThan(.8f), "The cross cannot inherit the near-black tint of a plain button panel.");
                Assert.That(button.colors.selectedColor, Is.Not.EqualTo(button.colors.normalColor));
                Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.selectedColor));
                Assert.That(button.colors.disabledColor.g, Is.GreaterThan(.4f));

                style.Restore();
                Assert.That(button.colors, Is.EqualTo(colors));
                Assert.That(symbol.sprite, Is.SameAs(sprite));
                Assert.That(symbol.color, Is.EqualTo(color));
            }
            finally { style.Restore(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredPartyTurnHighlightKeepsItsBorderAndLiveColor()
        {
            var card = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dungeon/UI/CharacterStatsDisplay-3e44211b.prefab");
            Assert.That(card.GetComponent<DungeonPartyCard>(), Is.Not.Null);
            var source = card.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(image => image.name == "Turn highlight");
            var border = Object.Instantiate(source);
            var style = new TerminalMenuStyle();
            try
            {
                var sprite = border.sprite;
                Assert.That(sprite, Is.Not.Null);
                Assert.That(border.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(border.fillCenter, Is.False);
                border.enabled = true;
                border.color = new Color(1f, .84f, .30f, 1f);
                style.Refresh(border.transform, TMP_Settings.defaultFontAsset);
                Assert.That(border.sprite, Is.SameAs(sprite));
                Assert.That(border.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced), "A live portrait outline must not become a filled rectangle.");
                Assert.That(border.fillCenter, Is.False);
                Assert.That(border.color, Is.EqualTo(new Color(1f, .84f, .30f, 1f)));

                var liveColor = new Color(1f, .84f, .30f, .78f);
                border.color = liveColor;
                style.Refresh(border.transform, TMP_Settings.defaultFontAsset);
                Assert.That(border.color, Is.EqualTo(liveColor), "Periodic styling cannot replace the turn indicator's live pulse.");
                style.Restore();
                Assert.That(border.sprite, Is.SameAs(sprite));
                Assert.That(border.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(border.color, Is.EqualTo(liveColor), "Mode exit cannot restore a stale turn highlight color.");
                Assert.That(border.enabled, Is.True);
            }
            finally { style.Restore(); Object.DestroyImmediate(border.gameObject); }
        }

        [Test]
        public void AspectPreservingTextPanelsBecomeDarkAndRestoreWhilePortraitsStayIntact()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bamao/BamaoUIPack/Prefabs/Frame/ItemSheetFrame.prefab");
            var root = Object.Instantiate(source);
            var style = new TerminalMenuStyle();
            try
            {
                var sheet = root.GetComponent<UnityEngine.UI.Image>();
                var sprite = sheet.sprite;
                var color = sheet.color;
                Assert.That(sheet.preserveAspect, Is.True, "The recruitment description uses this aspect-preserving sheet.");
                var label = new GameObject("Recruitment description", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(root.transform, false);
                label.text = "Bard / Rogue\nNot enough gold.";
                var portrait = Image("Portrait", root.transform);
                portrait.sprite = sprite;
                portrait.preserveAspect = true;
                var portraitColor = portrait.color;

                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                Assert.That(sheet.sprite, Is.Null, "A parchment text surface cannot remain behind light terminal ink.");
                Assert.That(sheet.color.r + sheet.color.g + sheet.color.b, Is.LessThan(.15f));
                Assert.That(sheet.color.a, Is.EqualTo(1));
                Assert.That(portrait.sprite, Is.SameAs(sprite));
                Assert.That(portrait.color, Is.EqualTo(portraitColor));
                style.Restore();
                Assert.That(sheet.sprite, Is.SameAs(sprite));
                Assert.That(sheet.color, Is.EqualTo(color));
                Assert.That(sheet.preserveAspect, Is.True);
            }
            finally { style.Restore(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ModalBackingIsOpaqueWithoutChangingLayoutOrPointerRouting()
        {
            var root = new GameObject("Modal fixture", typeof(RectTransform));
            var hud = new GameObject("HUD fixture", typeof(RectTransform));
            var style = new TerminalMenuStyle();
            try
            {
                Image("Existing modal surface", root.transform);
                Image("Existing HUD surface", hud.transform);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset, opaqueBackdrop: true);
                style.Refresh(hud.transform, TMP_Settings.defaultFontAsset);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset, opaqueBackdrop: true);
                var backing = root.transform.Find("Terminal modal background");
                Assert.That(backing, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(2), "Repeated styling cannot add duplicate backings.");
                Assert.That(backing.GetSiblingIndex(), Is.Zero);
                Assert.That(backing.GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(1));
                Assert.That(backing.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
                Assert.That(backing.GetComponent<LayoutElement>().ignoreLayout, Is.True);
                Assert.That(hud.transform.childCount, Is.EqualTo(1), "HUD styling cannot cover the ASCII viewport.");
                style.Restore();
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally { style.Restore(); Object.DestroyImmediate(root); Object.DestroyImmediate(hud); }
        }

        [Test]
        public void LiveWarningsAndActiveTabContrastRestoreTheirLatestState()
        {
            var root = new GameObject("Live terminal styling", typeof(RectTransform));
            var style = new TerminalMenuStyle();
            try
            {
                var surface = Image("Inventory tab", root.transform);
                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                var label = new GameObject("Tab label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(button.transform, false);
                label.text = "Inventory";
                var warning = new GameObject("Bag warning", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                warning.transform.SetParent(root.transform, false);
                warning.color = GameUITheme.Ink;
                GameUITheme.StyleActiveTab(button, false);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                var inactive = button.colors.normalColor;

                warning.color = new Color(.59f, .23f, .13f);
                GameUITheme.StyleActiveTab(button, true);
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                Assert.That(warning.color.r, Is.GreaterThan(warning.color.g));
                Assert.That(warning.color.r, Is.GreaterThan(.8f), "A warning remains legible on dark panels.");
                Assert.That(button.colors.normalColor.g, Is.GreaterThan(inactive.g), "The active tab remains distinct when focus moves into its content.");
                Assert.That((label.fontStyle & FontStyles.Underline) != 0, Is.True);

                var almostFull = new Color(.62f, .39f, .08f);
                warning.color = almostFull;
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset);
                Assert.That(warning.color.g, Is.GreaterThan(.8f));
                var latest = new Color(.59f, .23f, .13f);
                warning.color = latest;
                GameUITheme.StyleActiveTab(button, false);
                style.Restore();
                Assert.That(warning.color, Is.EqualTo(latest), "A resource update just before mode exit must not be replaced by a cached color.");
                Assert.That(button.colors.normalColor, Is.EqualTo(Color.white));
            }
            finally { style.Restore(); Object.DestroyImmediate(root); }
        }

        [TestCase(8, 5)]
        [TestCase(22, 15)]
        [TestCase(82, 30)]
        public void MainMenuAsciiStaysInsideItsRequestedGrid(int columns, int rows)
        {
            var root = new GameObject("Terminal title fixture");
            try
            {
                var view = root.AddComponent<TerminalMainMenuView>();
                var lines = Regex.Replace(view.CurrentTitleMarkup(columns, rows), "<[^>]+>", "").Split('\n');
                Assert.That(lines.Length, Is.EqualTo(rows), "The final newline must not create an extra row.");
                foreach (var line in lines) Assert.That(line.Length, Is.LessThanOrEqualTo(columns));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DockBackingLeavesTheGameplayViewportUncovered()
        {
            var root = new GameObject("Dock fixture", typeof(RectTransform));
            var style = new TerminalMenuStyle();
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 600);
                var dock = Image("Panel", root.transform);
                dock.rectTransform.anchorMin = new Vector2(.73f, .03f);
                dock.rectTransform.anchorMax = new Vector2(.98f, .88f);
                dock.rectTransform.offsetMin = dock.rectTransform.offsetMax = Vector2.zero;
                style.Refresh(root.transform, TMP_Settings.defaultFontAsset, opaqueBackdrop: true);
                Assert.That(root.transform.Find("Terminal modal background"), Is.Null);
                Assert.That(dock.transform.Find("Terminal modal background"), Is.Not.Null,
                    "A full-screen input owner must not imply a full-screen visual background.");
            }
            finally { style.Restore(); Object.DestroyImmediate(root); }
        }

        private static UnityEngine.UI.Image Image(string name, Transform parent)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(parent, false);
            return image;
        }
    }
}
