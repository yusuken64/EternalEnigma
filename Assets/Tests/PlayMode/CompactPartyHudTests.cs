#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class CompactPartyHudTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Folder = "Temp/CompactPartyHud";
        internal static readonly Vector2[] Resolutions = { new(960,600), new(1280,720), new(1280,800), new(1920,1080), new(2560,1080) };
        GameTestHarness harness;
        TestInputScope inputs;
        IDisposable preferences;
        EditorWindow view;
        int oldSize;
        bool? fullControl;
        DungeonAnimationMode? animation;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope(); preferences = GamePreferences.Isolate(); TerminalMode.SetRequested(false);
            fullControl = DungeonPreferences.FullControlOverride; animation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.FullControlOverride = false; DungeonPreferences.AnimationOverride = DungeonAnimationMode.NoAnimations;
            view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            harness = new GameTestHarness(); Directory.CreateDirectory(Folder);
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Avery", "Reese", "Sage" } });
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            try { yield return harness.Cleanup(); }
            finally
            {
                inputs?.Dispose(); preferences?.Dispose();
                DungeonPreferences.FullControlOverride = fullControl; DungeonPreferences.AnimationOverride = animation;
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, oldSize);
            }
        }
        IEnumerator Resolution(Vector2 resolution)
        {
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { resolution, "Compact party HUD" });
            view.Repaint(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases(); yield return null;
        }
        static TMP_Text Order(CharacterStatsDisplay display) =>
            (TMP_Text)typeof(DungeonPartyCard).GetField("order", Flags).GetValue(display.GetComponent<DungeonPartyCard>());

        [UnityTest] public IEnumerator AllInformationSurvivesCompactRowsAtEveryPartySize()
        {
            var hud = DungeonHud.Ensure(harness.Game);
            // Freeze only automatic charging while comparing identical presentation states.
            harness.Game.TurnManager.enabled = false;
            var displays = hud.PartySlots;
            var actor = (Ally)displays[0].Character;
            actor.Equipment.ClassFilter = null;
            actor.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName("Bow"));
            actor.Equipment.Equip((EquipableInventoryItem)Common.Instance.ItemManager.GetAsInventoryItemByName("Wooden Arrows", 137));
            actor.Vitals.Level = actor.DisplayedVitals.Level = 12;
            actor.PendingCast = new PendingCast(new WaitAction(), "Rain of Arrows", 13);
            foreach (var type in new[] { typeof(FrailtyStatusEffect), typeof(StrengthStatusEffect), typeof(DamageReductionStatusEffect), typeof(BarrierStatusEffect), typeof(EndureStatusEffect), typeof(HotStatusEffect) })
            {
                var go = new GameObject(type.Name); go.transform.SetParent(actor.transform, false);
                var effect = (StatusEffect)go.AddComponent(type); effect.TurnsLeft = 7; effect.Apply(); actor.StatusEffects.Add(effect);
                if (effect is FrailtyStatusEffect) { effect.ReApply(effect); effect.ReApply(effect); }
            }
            var downed = (Ally)displays[1].Character;
            downed.Vitals.Level = downed.DisplayedVitals.Level = 8;
            downed.Vitals.HP = downed.DisplayedVitals.HP = 0;
            // Use the real downed roster path; the controlled hero remains alive.
            PartyRules.MarkDowned(harness.Game, downed);
            ((Ally)displays[2].Character).PendingCast = new PendingCast(new WaitAction(), "Full Heal", 1);
            ((Ally)displays[3].Character).AllyStrategy = AllyStrategy.HoldPosition;
            HudRefresh.PartyChanged(); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            // The roster may reorder downed allies; keep comparisons keyed by name.
            displays = hud.PartySlots;
            var casterCard = displays.Single(d => d.Character == actor);
            var downedCard = displays.Single(d => d.Character == downed);
            Assert.That(Order(casterCard).text, Is.EqualTo(GameplayText.Format($"Rain of Arrows  Charging for 12 more actions\nWooden Arrows x137  ▲ {actor.PendingAttributePoints}")));
            Assert.That(downedCard.NameText.text, Does.Contain("Lv 8").And.Contain("Downed"));
            var statuses = casterCard.GetComponent<DungeonPartyCard>().StatusButtons;
            Assert.That(statuses.Count(b => b.gameObject.activeSelf), Is.EqualTo(4));
            Assert.That(statuses[0].GetComponentInChildren<TMP_Text>().text, Is.EqualTo("x3"));
            Assert.That(statuses[3].GetComponentInChildren<TMP_Text>().text, Is.EqualTo("+3"));
            statuses[3].onClick.Invoke();
            Assert.That(hud.ShowingTargetDetails, Is.True);
            Assert.That(hud.GetComponentsInChildren<TMP_Text>().Any(t => t.text == GameplayText.Format(actor.CharacterName + "\n" + DungeonHud.StatusSummary(actor))), Is.True);

            bool compact = casterCard.PortraitImage.GetComponentInParent<Mask>() != null;
            string phase = compact ? "after" : "before";
            var snapshot = string.Join("\n", displays.OrderBy(d => d.Character.CharacterName).Select(Snapshot));
            File.WriteAllText($"{Folder}/{phase}-information.txt", snapshot);
            if (compact && File.Exists($"{Folder}/before-information.txt"))
                Assert.That(snapshot, Is.EqualTo(File.ReadAllText($"{Folder}/before-information.txt")), "Every displayed string and slider value must match the same baseline hero states.");
            foreach (var resolution in Resolutions)
            {
                yield return Resolution(resolution);
                for (int count = 4; count >= 1; count--)
                {
                    for (int i = 0; i < displays.Length; i++) displays[i].gameObject.SetActive(i < count);
                    yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                    Assert.That(displays.Count(d => d.gameObject.activeSelf), Is.EqualTo(count));
                    if (compact) AssertLayout(displays.Take(count));
                    ScreenCapture.CaptureScreenshot($"{Folder}/{phase}-dungeon-{count}-{resolution.x}x{resolution.y}.png");
                    yield return new WaitForSecondsRealtime(.1f);
                }
            }
            for (int i = 0; i < displays.Length; i++) displays[i].gameObject.SetActive(true);

            // Exercise partially filled bars as well as the baseline full/empty states.
            actor.Vitals.Exp = (LevelSystem.ExperienceAtLevel(12) + LevelSystem.ExperienceAtLevel(13)) / 2;
            actor.Vitals.HP = 13; actor.Vitals.Mana = 1; actor.Vitals.Hunger = 57;
            actor.SyncDisplayedStats(); HudRefresh.PartyChanged();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(casterCard.HpDisplay.ValueText.text, Is.EqualTo($"HP 13/{actor.DisplayedStats.HPMax}"));
            Assert.That(casterCard.ManaDisplay.ValueText.text, Is.EqualTo($"Mana 1/{actor.DisplayedStats.ManaMax}"));
            Assert.That(casterCard.HungerDisplay.ValueText.text, Is.EqualTo($"Food 57/{actor.DisplayedStats.HungerMax}"));
            Assert.That(casterCard.HpDisplay.StatValueSlider.value, Is.EqualTo(13f / actor.DisplayedStats.HPMax).Within(.001f));
            Assert.That(casterCard.ManaDisplay.StatValueSlider.value, Is.EqualTo(1f / actor.DisplayedStats.ManaMax).Within(.001f));
            Assert.That(casterCard.HungerDisplay.StatValueSlider.value, Is.EqualTo(57f / actor.DisplayedStats.HungerMax).Within(.001f));
            Assert.That(casterCard.LevelDisplay.StatValueSlider.value, Is.InRange(.49f, .51f));
            if (compact) AssertLayout(displays);
        }

        static string Snapshot(CharacterStatsDisplay display)
        {
            var bars = new[] { display.HpDisplay, display.ManaDisplay, display.HungerDisplay, display.LevelDisplay };
            return display.NameText.text + "\n" + Order(display).text + "\n" +
                string.Join("\n", bars.Select(b => b.ValueText.text + "=" + b.StatValueSlider.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "\n" +
                string.Join("|", display.GetComponent<DungeonPartyCard>().StatusButtons.Where(b => b.gameObject.activeSelf).Select(b => b.GetComponentInChildren<TMP_Text>().text));
        }

        internal static void AssertLayout(IEnumerable<CharacterStatsDisplay> cards)
        {
            Rect previous = default;
            foreach (var card in cards.Where(c => c.gameObject.activeInHierarchy))
            {
                var row = (RectTransform)card.transform;
                var bounds = ScreenRect(row);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width + 1));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height + 1));
                if (previous != default) Assert.That(bounds.yMax, Is.LessThan(previous.yMin), "Content-sized rows must not overlap.");
                previous = bounds;
                var mask = card.PortraitImage.GetComponentInParent<Mask>();
                Assert.That(mask, Is.Not.Null);
                Assert.That(((RectTransform)mask.transform).rect.size, Is.EqualTo(new Vector2(72,72)));
                Assert.That(card.PortraitImage.GetComponent<Canvas>(), Is.Null, "Override sorting must be above the mask.");
                var highlight = card.GetComponentsInChildren<Image>(true).Single(i => i.name == "Turn highlight");
                Assert.That(highlight.transform.IsChildOf(mask.transform), Is.False);
                foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.font, Is.SameAs(GameTypography.Expected(text)), text.text);
                    Assert.That(text.isTextOverflowing, Is.False, text.text);
                    // Use rendered vertices as well as TMP's overflow flag.
                    foreach (var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible))
                    {
                        foreach (var point in new[] { c.bottomLeft, c.topRight })
                        {
                            var local = text.rectTransform.rect;
                            Assert.That(point.x, Is.InRange(local.xMin - 1, local.xMax + 1), text.text);
                            Assert.That(point.y, Is.InRange(local.yMin - 1, local.yMax + 1), text.text);
                            var screen = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(point));
                            Assert.That(bounds.Contains(screen), Is.True, "Text outside its card: " + text.text);
                        }
                    }
                }
            }
        }
        static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest] public IEnumerator PortraitMasksSurviveSortingAndEveryHeroCrop()
        {
            var cards = DungeonHud.Ensure(harness.Game).PartySlots;
            if (cards[0].PortraitImage.GetComponentInParent<Mask>() == null) Assert.Ignore("Run after authoring compact cards.");
            yield return Resolution(new Vector2(1920,1080));
            var heroes = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Town" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<TownAlly>())
                .Where(h => h != null && h.Portrait != null).OrderBy(h => h.name).ToArray();
            // The roster's dedicated test owns the exact count; this capture must still cover every portrait.
            Assert.That(heroes.Length, Is.GreaterThanOrEqualTo(24));
            var atlas = new Texture2D(6 * 180, Mathf.CeilToInt(heroes.Length / 6f) * 210, TextureFormat.RGB24, false);
            var pixels = Enumerable.Repeat(new Color(.07f,.10f,.10f), atlas.width * atlas.height).ToArray(); atlas.SetPixels(pixels);
            try
            {
                for (int i = 0; i < heroes.Length; i++)
                {
                    cards[0].PortraitImage.sprite = heroes[i].Portrait;
                    yield return null; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
                    var portrait = cards[0].PortraitImage.GetComponentInParent<Mask>().transform.parent as RectTransform;
                    var crop = ScreenRect(portrait); crop.xMin -= 4; crop.xMax += 4; crop.yMin -= 4; crop.yMax += 4;
                    var image = new Texture2D((int)crop.width, (int)crop.height, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(crop, 0, 0); image.Apply();
                        File.WriteAllBytes($"{Folder}/portrait-{i + 1:00}-{heroes[i].Name}.png", image.EncodeToPNG());
                        for (int y = 0; y < 160; y++) for (int x = 0; x < 160; x++)
                            atlas.SetPixel(i % 6 * 180 + x + 10, atlas.height - (i / 6 + 1) * 210 + y + 25, image.GetPixelBilinear(x / 159f, y / 159f));
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                atlas.Apply(); File.WriteAllBytes(Folder + "/all-portrait-crops.png", atlas.EncodeToPNG());
                File.WriteAllLines(Folder + "/portrait-order.txt", heroes.Select((h,i) => $"{i + 1}: {h.Name} ({h.name})"));
            }
            finally { Object.DestroyImmediate(atlas); }

            cards[0].PortraitImage.sprite = ((Ally)cards[0].Character).Portrait;
            var mask = cards[0].PortraitImage.GetComponentInParent<Mask>();
            var sorting = mask.GetComponentInParent<Canvas>();
            var manager = MenuManager.Instance;
            manager.OpenPartyMenu(PartyMenuTab.Stats); yield return null; yield return null;
            Assert.That(sorting.overrideSorting, Is.True);
            Assert.That(MaskUtilities.FindRootSortOverrideCanvas(cards[0].PortraitImage.transform), Is.EqualTo(sorting.transform));
            Assert.That(MaskUtilities.GetStencilDepth(cards[0].PortraitImage.transform, sorting.transform), Is.EqualTo(1));
            Assert.That(cards[0].PortraitImage.materialForRendering.GetInt("_Stencil"), Is.EqualTo(1));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Folder + "/sorted-portrait-mask.png");
            Common.Instance.GlobalSettings.ShowDialog(); yield return null; yield return null;
            Assert.That(sorting.overrideSorting, Is.False, "Settings must cover portrait input.");
        }
    }
}
#endif
