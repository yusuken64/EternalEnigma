#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class SettingsInteractionTests
    {
        private GameTestHarness harness;
        private TestInputScope input;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private GlobalSettings settings;
        [UnitySetUp] public IEnumerator SetUp()
        {
            input = new TestInputScope(); harness = new GameTestHarness();
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard, mouse };
            settings = Common.Instance.GlobalSettings;
            settings.ShowDialog(); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            yield return harness.Cleanup(); input.Dispose();
        }
        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
        }
        private IEnumerator Key(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private IEnumerator Click(Button button)
        {
            var pos = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = 1 }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }); yield return null;
        }
        private GameObject Selected => EventSystem.current.currentSelectedGameObject;
        [UnityTest] public IEnumerator GameplayExplanationChangesWhileDialogRemainsOpen()
        {
            var gameplay = settings.TabGroup.TabContents.Single(t => t.Content.GetComponent<DungeonOptions>() != null);
            gameplay.TabButton.Select(); yield return null;
            var explanation = gameplay.Content.GetComponentsInChildren<TMP_Text>(true)
                .Single(t => t.text.Contains("Full Control asks"));
            Assert.That(explanation.text, Does.Contain("Press F"));
            yield return Press(GamepadButton.South);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(explanation.text, Does.Contain("Press Right stick (press)"));
            Assert.That(explanation.text, Does.Not.Contain("Press F"));
            yield return Key(UnityEngine.InputSystem.Key.K);
            Assert.That(explanation.text, Does.Contain("Press F"));
        }
        [UnityTest] public IEnumerator PreviewEntryCancelAndReopeningWorkWhilePaused()
        {
            Time.timeScale = 0;
            var tabs = settings.TabGroup.TabContents;
            Assert.That(tabs.Select(t => t.TabButton.GetComponentInChildren<TMP_Text>().text).ToArray(), Is.EqualTo(new[] { "Audio", "Display", "Controls", "Gameplay" }));
            var gameplay = tabs.Last();
            gameplay.TabButton.Select(); yield return null;
            Assert.That(settings.TabGroup.SelectedTab, Is.SameAs(gameplay));
            var first = gameplay.Content.GetComponentsInChildren<Selectable>().First(s => s.IsInteractable());
            var labelBefore = first.GetComponentInChildren<TMP_Text>().text;
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            Assert.That(Selected, Is.EqualTo(first.gameObject));
            Assert.That(first.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(labelBefore), "Entering must not activate the first button.");
            yield return new WaitForSecondsRealtime(.15f);
            var arrow = settings.NavigationHandler.selectionArrow;
            Assert.That(arrow.gameObject.activeInHierarchy, Is.True);
            var corners = new Vector3[4]; ((RectTransform)first.transform).GetWorldCorners(corners);
            Assert.That(arrow.position.x, Is.GreaterThan(corners[2].x));
            var arrowCorners = new Vector3[4]; arrow.GetWorldCorners(arrowCorners);
            Assert.That(arrowCorners.Min(c => c.x), Is.GreaterThan(corners[2].x + 2), "Arrow bounds must leave space beside the focused control.");
            Assert.That(arrowCorners.Max(c => c.x), Is.LessThan(Screen.width), "Arrow must remain on screen.");
            Assert.That(arrow.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
            yield return Key(UnityEngine.InputSystem.Key.Escape); Assert.That(Selected, Is.EqualTo(gameplay.TabButton.gameObject));
            yield return Key(UnityEngine.InputSystem.Key.Escape); Assert.That(Selected, Is.EqualTo(settings.ResumeButton.gameObject));
            yield return Key(UnityEngine.InputSystem.Key.Escape); Assert.That(settings.IsOpen, Is.False);
            Assert.That(arrow.gameObject.activeInHierarchy, Is.False);
            settings.ShowDialog(); yield return null;
            Assert.That(Selected, Is.EqualTo(settings.FirstSelected));
            settings.ReturntoMainButton.Select(); yield return Press(GamepadButton.East);
            Assert.That(Selected, Is.EqualTo(settings.ResumeButton.gameObject));
            EventSystem.current.SetSelectedGameObject(null); yield return Press(GamepadButton.East);
            Assert.That(settings.IsOpen, Is.True); Assert.That(Selected, Is.EqualTo(settings.ResumeButton.gameObject));
            // A modal owns selection even if its root lives under the settings canvas.
            var modal = settings.ResumeButton;
            MenuUIInputModule.Active.PushDialog(modal, modal.transform, modal.gameObject);
            yield return null; Assert.That(arrow.gameObject.activeSelf, Is.False);
            MenuUIInputModule.Active.PopDialog(modal); yield return null;
            Assert.That(arrow.gameObject.activeInHierarchy, Is.True);
        }
        [UnityTest] public IEnumerator MousePreviewsThenEntersAndDropdownCancelDoesNotLeaveDetails()
        {
            var tab = settings.TabGroup.TabContents.Single(t => t.Content.GetComponent<DisplayOptions>() != null);
            yield return Click(tab.TabButton);
            Assert.That(Selected, Is.EqualTo(tab.TabButton.gameObject));
            Assert.That(settings.TabGroup.SelectedTab, Is.SameAs(tab));
            yield return Click(tab.TabButton);
            var display = tab.Content.GetComponent<DisplayOptions>();
            Assert.That(Selected, Is.EqualTo(display.WindowMode.gameObject));
            Assert.That(display.WindowMode.IsExpanded, Is.False);
            var originalMode = Screen.fullScreenMode;
            yield return Press(GamepadButton.South);
            Assert.That(display.WindowMode.IsExpanded, Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            System.IO.Directory.CreateDirectory("Temp/Settings");
            ScreenCapture.CaptureScreenshot("Temp/Settings/dropdown.png");
            yield return new WaitForSecondsRealtime(.2f);
            yield return Press(GamepadButton.DpadDown);
            Assert.That(Screen.fullScreenMode, Is.EqualTo(originalMode));
            yield return Press(GamepadButton.East);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(display.WindowMode.IsExpanded, Is.False);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(Selected, Is.EqualTo(display.WindowMode.gameObject));
            yield return Press(GamepadButton.East); Assert.That(Selected, Is.EqualTo(tab.TabButton.gameObject));
            yield return Press(GamepadButton.DpadRight); Assert.That(Selected, Is.EqualTo(display.WindowMode.gameObject));
            System.IO.Directory.CreateDirectory("Temp/Settings");
            yield return new WaitForSecondsRealtime(.15f);
            ScreenCapture.CaptureScreenshot("Temp/Settings/display.png"); yield return new WaitForSecondsRealtime(.2f);
            var audioTab = settings.TabGroup.TabContents.First(); audioTab.TabButton.Select();
            yield return Press(GamepadButton.South);
            yield return new WaitForSecondsRealtime(.15f);
            var slider = Selected.GetComponent<Slider>();
            var handleCorners = new Vector3[4]; slider.handleRect.GetWorldCorners(handleCorners);
            var cursorCorners = new Vector3[4]; settings.NavigationHandler.selectionArrow.GetWorldCorners(cursorCorners);
            Assert.That(cursorCorners.Min(c => c.x), Is.GreaterThan(handleCorners.Max(c => c.x) + 2), "Cursor must clear the slider handle.");
            Assert.That(cursorCorners.Max(c => c.x), Is.LessThan(Screen.width));
            ScreenCapture.CaptureScreenshot("Temp/Settings/audio.png"); yield return new WaitForSecondsRealtime(.2f);
        }
        [UnityTest] public IEnumerator AudioChannelsAreIndependentPersistAndRefreshLabels()
        {
            var sliders = settings.GetComponentsInChildren<VolumeSlider>(true);
            Assert.That(sliders.Length, Is.EqualTo(3));
            var saved = sliders.Where(s => PlayerPrefs.HasKey(AudioPreferences.Key(s.VolumeParameterName))).ToDictionary(s => s.VolumeParameterName, s => PlayerPrefs.GetFloat(AudioPreferences.Key(s.VolumeParameterName)));
            var original = sliders.ToDictionary(s => s.VolumeParameterName, s => AudioPreferences.Read(s.AudioMixerGroup, s.VolumeParameterName));
            try
            {
                foreach (var slider in sliders) slider.OnSliderChanged(slider.VolumeParameterName == "UIVolume" ? 0 : slider.VolumeParameterName == "MusicVolume" ? .37f : .62f);
                foreach (var slider in sliders)
                {
                    var expected = slider.VolumeParameterName == "UIVolume" ? 0 : slider.VolumeParameterName == "MusicVolume" ? .37f : .62f;
                    Assert.That(AudioPreferences.Read(slider.AudioMixerGroup, slider.VolumeParameterName), Is.EqualTo(expected).Within(.001));
                    slider.AudioMixerGroup.audioMixer.SetFloat(slider.VolumeParameterName, -10);
                    AudioPreferences.Restore(slider.AudioMixerGroup, slider.VolumeParameterName);
                    slider.Refresh(); Assert.That(slider.Slider.value, Is.EqualTo(expected).Within(.001));
                    Assert.That(slider.VolumeValueText.text, Is.EqualTo(Mathf.RoundToInt(expected * 100) + "%"));
                }
                var manager = Common.Instance.AudioManager;
                Assert.That(manager.UIAudioSources.All(s => s.outputAudioMixerGroup == manager.UIAudioMixerGroup), Is.True);
                Assert.That(manager.EffectAudioSources.All(s => s.outputAudioMixerGroup == manager.EffectAudioMixerGroup), Is.True);
                manager.PlayUISound(manager.SoundEffects.Hover);
                Assert.That(manager.UIAudioSources.Any(s => s.clip == manager.SoundEffects.Hover), Is.True);
                settings.Exit_Clicked(); settings.ShowDialog(); yield return null;
                var ui = sliders.Single(s => s.VolumeParameterName == "UIVolume"); Assert.That(ui.VolumeValueText.text, Is.EqualTo("0%"));
            }
            finally
            {
                foreach (var slider in sliders)
                {
                    var parameter = slider.VolumeParameterName;
                    slider.AudioMixerGroup.audioMixer.SetFloat(parameter, AudioPreferences.ToDecibels(original[parameter]));
                    if (saved.TryGetValue(parameter, out var value)) PlayerPrefs.SetFloat(AudioPreferences.Key(parameter), value); else PlayerPrefs.DeleteKey(AudioPreferences.Key(parameter));
                }
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
