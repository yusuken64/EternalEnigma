using NUnit.Framework;
using TMPro;
using UnityEngine;
using EternalEnigma.Core.Terminal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EternalEnigma.Core.World;

namespace EternalEnigma.Tests
{
    public class TerminalIntegrationTests
    {
        private System.IDisposable preferences;
        [SetUp] public void SetUp()
        {
            preferences = GamePreferences.Isolate();
        }
        [TearDown] public void TearDown()
        {
            preferences.Dispose();
        }
        [Test] public void PreferenceDefaultsOffAndChangesOnlyOnce()
        {
            int changes = 0;
            void Changed() => changes++;
            TerminalMode.Changed += Changed;
            try
            {
                Assert.That(TerminalMode.Requested, Is.False);
                TerminalMode.SetRequested(true);
                TerminalMode.SetRequested(true);
                Assert.That(TerminalMode.Requested, Is.True);
                Assert.That(changes, Is.EqualTo(1));
            }
            finally { TerminalMode.Changed -= Changed; }
        }
        [Test] public void RuntimeScreenLoadsBundledMonospaceFont()
        {
            var owner = new GameObject("Terminal test screen");
            try
            {
                var screen = owner.AddComponent<TerminalScreen>();
                Assert.That(screen.Initialize(), Is.True);
                var text = owner.GetComponentInChildren<TextMeshProUGUI>();
                Assert.That(text.font, Is.Not.Null);
                Assert.That(Resources.Load<Font>("Terminal/TerminalMono"), Is.Not.Null);
                Assert.That(screen.Columns, Is.GreaterThan(0));
                Assert.That(screen.Rows, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void TmpTreatsHostileLookingGameTextAsLiteral()
        {
            var owner = new GameObject("Terminal markup test");
            try
            {
                var frame = new TerminalFrame();
                frame.Begin(36, 1);
                frame.Write(0, 0, "</noparse><color=#ff0000>&", TerminalPalette.White);
                frame.Commit();
                var screen = owner.AddComponent<TerminalScreen>();
                Assert.That(screen.Initialize(), Is.True);
                screen.Show(true);
                var text = owner.GetComponentInChildren<TextMeshProUGUI>();
                text.text = frame.RichText;
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate(true, true);
                Assert.That(text.GetParsedText(), Does.StartWith("</noparse><color=#ff0000>&"));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void RuntimeToggleButtonIsUniqueAndChangesPreference()
        {
            var parent = new GameObject("Terminal button test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var button = TerminalModeButton.Ensure(parent.transform);
                Assert.That(TerminalModeButton.Ensure(parent.transform), Is.SameAs(button));
                button.GetComponent<Button>().onClick.Invoke();
                Assert.That(TerminalMode.Requested, Is.True);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test] public void MenuAdapterRevalidatesRebuiltDisabledAndChangedRowsAndSubmitsOnlyOnce()
        {
            var root = new GameObject("Terminal menu test", typeof(RectTransform));
            var events = new GameObject("Terminal event test", typeof(EventSystem));
            int invoked = 0;
            try
            {
                var row = new GameObject("Buy potion", typeof(RectTransform), typeof(Button));
                row.transform.SetParent(root.transform, false);
                var button = row.GetComponent<Button>();
                button.onClick.AddListener(() => invoked++);
                var session = new TerminalMenuSession(root.transform, () => true);
                session.Select(button);
                button.interactable = false;
                Assert.That(session.Confirm(1), Is.False);
                Assert.That(invoked, Is.Zero);
                button.interactable = true;
                row.name = "Buy a different item";
                Assert.That(session.Confirm(2), Is.False, "Changed row must be read before confirming it.");
                Assert.That(session.Confirm(2), Is.False, "A rejected press cannot be replayed in the same frame.");
                Assert.That(session.Confirm(3), Is.True);
                Assert.That(session.Confirm(3), Is.False);
                Assert.That(invoked, Is.EqualTo(1));
                Object.DestroyImmediate(row);
                var replacement = new GameObject("Replacement", typeof(RectTransform), typeof(Button));
                replacement.transform.SetParent(root.transform, false);
                replacement.GetComponent<Button>().onClick.AddListener(() => invoked++);
                Assert.That(session.Confirm(4), Is.False, "A rebuilt row cannot inherit a stale confirm.");
                Assert.That(invoked, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        [Test] public void MenuAdapterCannotInvokeAControlBehindAnotherModalOrAfterClose()
        {
            var root = new GameObject("Terminal owner test", typeof(RectTransform));
            var events = new GameObject("Terminal event test", typeof(EventSystem));
            bool owns = true;
            int invoked = 0;
            try
            {
                var row = new GameObject("Rest", typeof(RectTransform), typeof(Button));
                row.transform.SetParent(root.transform, false);
                row.GetComponent<Button>().onClick.AddListener(() => invoked++);
                var session = new TerminalMenuSession(root.transform, () => owns);
                owns = false;
                Assert.That(session.Confirm(10), Is.False);
                owns = true;
                root.SetActive(false);
                Assert.That(session.Confirm(11), Is.False);
                Assert.That(invoked, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        [Test] public void TerminalPointerUsesWholeCellsForSmallGlyphsAndWhitespace()
        {
            var root = new GameObject("Terminal pointer cells");
            try
            {
                var screen = root.AddComponent<TerminalScreen>();
                Assert.That(screen.Initialize(), Is.True);
                var frame = new TerminalFrame();
                frame.Begin(8, 2);
                frame.Write(0, 0, ". ,_i@# ", TerminalPalette.White);
                frame.Write(0, 1, "  .:;<> ", TerminalPalette.Selected);
                frame.Commit(); screen.Show(true);
                var text = root.GetComponentInChildren<TextMeshProUGUI>();
                var bundledFont = screen.FontAsset;
                var ownedFontField = typeof(TerminalScreen).GetField("ownedFont",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
                foreach (bool fallback in new[] { false, true })
                {
                    try
                    {
                        ownedFontField.SetValue(screen, fallback ? null : bundledFont);
                        text.font = fallback ? TMP_Settings.defaultFontAsset : bundledFont;
                        screen.SetText(frame.RichText);
                        Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
                        float cellWidth = fallback ? text.fontSize : bundledFont.characterLookupTable['M'].glyph.metrics.horizontalAdvance *
                            text.fontSize / bundledFont.faceInfo.pointSize;
                        float left = text.rectTransform.rect.xMin + text.margin.x;
                        for (int row = 0; row < 2; row++)
                        for (int column = 0; column < 8; column++)
                        {
                            var line = text.textInfo.lineInfo[row];
                            float middle = (line.ascender + line.descender) * .5f;
                            Vector2 At(float fraction) => RectTransformUtility.WorldToScreenPoint(null,
                                text.transform.TransformPoint(new Vector3(left + (column + fraction) * cellWidth, middle)));
                            Assert.That(screen.TryGetFramePosition(column, row, out var center), Is.True);
                            Assert.That(Vector2.Distance(center, At(.5f)), Is.LessThan(.01f),
                                "The cell center must follow the fixed grid rather than the centered glyph's origin.");
                            foreach (float fraction in new[] { .25f, .75f })
                            {
                                Assert.That(screen.TryGetFrameCell(At(fraction), out int hitColumn, out int hitRow), Is.True,
                                    $"{(fallback ? "Fallback" : "Bundled")} cell {column},{row} ({frame[column, row].Glyph}) accepts both halves.");
                                Assert.That((hitColumn, hitRow), Is.EqualTo((column, row)));
                            }
                        }
                    }
                    finally { ownedFontField.SetValue(screen, bundledFont); }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void TerminalFontAndFramesStayAlignedAcrossDesktopAndSmallResizes()
        {
            var root = new GameObject("Terminal resize test");
            try
            {
                var screen = root.AddComponent<TerminalScreen>();
                Assert.That(screen.Initialize(), Is.True);
                var font = screen.FontAsset;
                float advance = font.characterLookupTable['M'].glyph.metrics.horizontalAdvance;
                foreach (char character in "i W@#.")
                    Assert.That(font.characterLookupTable[character].glyph.metrics.horizontalAdvance, Is.EqualTo(advance).Within(.01f),
                        "The bundled terminal font must give every glyph the same cell width.");
                var renderer = new TownTerminalRenderer();
                var snapshot = new TownTerminalSnapshot(12, 8, new GridPoint(6, 4), "Town <literal>",
                    _ => new TerminalCell('.', TerminalPalette.Dim),
                    new[] { new TerminalActor("hero", new GridPoint(6, 4), TerminalActorKind.Player) },
                    target: new TerminalTargetSnapshot(new GridPoint(6, 4), "Long target label wraps inside the terminal viewport"));
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(480, 320), new Vector2Int(1280, 720) })
                {
                    screen.Resize(size.x, size.y);
                    Assert.That(screen.Resize(size.x, size.y), Is.False, "Repeated size needs no new grid.");
                    renderer.Render(snapshot, screen.Columns, screen.Rows);
                    var rows = renderer.Frame.PlainText.Split('\n');
                    Assert.That(rows.Length, Is.EqualTo(screen.Rows));
                    foreach (string row in rows) Assert.That(row.Length, Is.EqualTo(screen.Columns));
                    Assert.That(renderer.Frame[screen.Columns - 1, screen.Rows - 1].Glyph, Is.EqualTo('+'));
                    screen.SetText(renderer.Frame.RichText);
                    var text = root.GetComponentInChildren<TextMeshProUGUI>();
                    text.ForceMeshUpdate(true, true);
                    Assert.That(text.GetParsedText(), Does.Contain("<literal>"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
