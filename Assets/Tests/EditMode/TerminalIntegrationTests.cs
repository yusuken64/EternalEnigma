using NUnit.Framework;
using TMPro;
using UnityEngine;
using EternalEnigma.Core.Terminal;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    public class TerminalIntegrationTests
    {
        private bool hadSetting;
        private int savedSetting;
        [SetUp] public void SetUp()
        {
            hadSetting = PlayerPrefs.HasKey(TerminalMode.PreferenceKey);
            savedSetting = PlayerPrefs.GetInt(TerminalMode.PreferenceKey);
            PlayerPrefs.DeleteKey(TerminalMode.PreferenceKey);
        }
        [TearDown] public void TearDown()
        {
            if (hadSetting) PlayerPrefs.SetInt(TerminalMode.PreferenceKey, savedSetting);
            else PlayerPrefs.DeleteKey(TerminalMode.PreferenceKey);
            PlayerPrefs.Save();
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
    }
}
