#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
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
        private IEnumerator Click(Selectable button)
        {
            var pos = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = 1 }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }); yield return null;
        }
        private GameObject Selected => EventSystem.current.currentSelectedGameObject;

        [UnityTest] public IEnumerator DungeonOptionsRemainsClickableAbovePartyDockAndResumeRestoresIt()
        {
            settings.Exit_Clicked();
            yield return harness.Cleanup();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems = true, Skills = new[] { "Damage" } });
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard, mouse };
            settings = Common.Instance.GlobalSettings;
            var manager = MenuManager.Instance;
            manager.OpenPartyMenu(PartyMenuTab.Skills);
            yield return null;
            var party = manager.PartyMenu;
            var launcher = party.HudTabs;
            var options = DungeonHud.Ensure(harness.Game).OptionsButton;
            Assert.That(launcher.OptionsButton, Is.SameAs(options), "Use the existing HUD control, without an extra Options button.");
            Assert.That(options.interactable, Is.True);
            Assert.That(options.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(party.GetComponent<Canvas>().sortingOrder));
            Assert.That(MenuUIInputModule.Active.Allows(options.gameObject), Is.True, "Options belongs to the party's focus scope.");
            var rect = (RectTransform)options.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.87f, .935f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(.985f, .985f)));
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(options.gameObject),
                "The party input shield must not intercept the visible Options control.");
            int actions = harness.Ally.Vitals.ActionsPerTurnLeft;
            yield return Click(options);
            Assert.That(settings.IsOpen, Is.True, "One actual mouse click opens Options from the party dock.");
            Assert.That(manager.CurrentDialog, Is.SameAs(party));
            System.IO.Directory.CreateDirectory("Temp/Settings");
            ScreenCapture.CaptureScreenshot("Temp/Settings/dungeon-options-over-party.png");
            yield return null;
            yield return Click(settings.ResumeButton);
            Assert.That(settings.IsOpen, Is.False, "One Resume click returns to the open party dock.");
            Assert.That(manager.CurrentDialog, Is.SameAs(party));
            Assert.That(party.Tab, Is.EqualTo(PartyMenuTab.Skills));
            Assert.That(MenuUIInputModule.Active.OwnsFocus(party), Is.True);
            Assert.That(MenuUIInputModule.Active.Allows(Selected), Is.True);
            Assert.That(harness.Ally.Vitals.ActionsPerTurnLeft, Is.EqualTo(actions));
        }

        [UnityTest] public IEnumerator ConfirmationFeedbackOnlyPlaysForAcceptedActivation()
        {
            var button = settings.TabGroup.TabContents[1].TabButton as SelectToActivateButton;
            Assert.That(button, Is.Not.Null);
            var audio = Common.Instance.AudioManager;
            foreach (var source in audio.UIAudioSources) source.Stop();
            settings.ResumeButton.Select();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            button.OnPointerDown(pointer); button.OnPointerClick(pointer);
            Assert.That(audio.UIAudioSources.Any(s => s.isPlaying), Is.False, "A selection click is not an activation.");
            button.OnPointerDown(pointer); button.OnPointerClick(pointer);
            Assert.That(audio.UIAudioSources.Any(s => s.isPlaying), Is.True, "Accepted actions have confirmation feedback.");
            foreach (var source in audio.UIAudioSources) source.Stop();
            bool interactable = button.interactable;
            button.interactable = false;
            button.OnSubmit(new BaseEventData(EventSystem.current));
            Assert.That(audio.UIAudioSources.Any(s => s.isPlaying), Is.False, "Disabled actions stay silent.");
            button.interactable = interactable;
            yield return null;
        }

        [UnityTest] public IEnumerator NavigationSoundsOnceForInputAndFocusRestorationStaysSilent()
        {
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group)
            {
                Assert.That(group, Is.SameAs(audio.UIAudioMixerGroup));
                cues.Add(cue);
            }
            audio.CuePlayed += Heard;
            try
            {
                settings.ResumeButton.Select();
                yield return null;
                EventSystem.current.SetSelectedGameObject(null);
                MenuUIInputModule.Active.RestoreFocus(settings.ResumeButton.gameObject);
                yield return null;
                Assert.That(cues, Is.Empty, "Programmatic focus and arrow updates are silent.");
                settings.ResumeButton.Select();
                yield return new WaitForSecondsRealtime(.06f);
                yield return Press(GamepadButton.DpadDown);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Navigate }), "One input change emits one navigation cue regardless of arrows.");
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest] public IEnumerator PointerSelectionAndActivationHaveSeparateCues()
        {
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            var button = settings.TabGroup.TabContents[1].TabButton;
            settings.ResumeButton.Select();
            yield return new WaitForSecondsRealtime(.06f);
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group) => cues.Add(cue);
            audio.CuePlayed += Heard;
            try
            {
                yield return Click(button);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Navigate }), "A selection-only click still sounds navigation.");
                cues.Clear();
                yield return Click(button);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }), "The next click activates without another navigation cue.");
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest] public IEnumerator AuthoredNativeButtonsPreserveOneClickAndPlaySemanticCues()
        {
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group) => cues.Add(cue);
            audio.CuePlayed += Heard;
            try
            {
                bool confirmed = false;
                CampaignChoice.Show("Audio test", "Confirm", () => confirmed = true);
                yield return null;
                var choice = AuthoredUI.Require<CampaignChoice>();
                Assert.That(choice.Confirm, Is.TypeOf<SelectToActivateButton>());
                Assert.That(((SelectToActivateButton)choice.Confirm).ActivateOnFirstPointerPress, Is.True,
                    "Migrating a native Button must preserve its one-click behavior.");
                cues.Clear();
                yield return Click(choice.Confirm);
                Assert.That(confirmed, Is.True);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
                CampaignChoice.Show("Audio test", "Confirm", () => { });
                yield return null;
                cues.Clear();
                yield return Click(choice.Back);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Cancel }));
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest] public IEnumerator NativeDropdownPointerOpeningHasOnlyConfirmFeedback()
        {
            var tab = settings.TabGroup.TabContents.Single(t => t.Content.GetComponent<DisplayOptions>() != null);
            tab.TabButton.Select();
            yield return null;
            var display = tab.Content.GetComponent<DisplayOptions>();
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group) => cues.Add(cue);
            audio.CuePlayed += Heard;
            try
            {
                yield return Click(display.WindowMode);
                Assert.That(display.WindowMode.IsExpanded, Is.True);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
                cues.Clear();
                yield return Press(GamepadButton.East);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Cancel }));
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest] public IEnumerator TerminalToggleUsesOneUICueForButtonAndShortcuts()
        {
            bool original = TerminalMode.Requested;
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group)
            {
                Assert.That(group, Is.SameAs(audio.UIAudioMixerGroup));
                cues.Add(cue);
            }
            try
            {
                TerminalMode.SetRequested(false);
                var tab = settings.TabGroup.TabContents.Single(t => t.Content.GetComponent<DisplayOptions>() != null);
                tab.TabButton.Select();
                yield return null;
                var button = tab.Content.GetComponentInChildren<TerminalModeButton>().GetComponent<SelectToActivateButton>();
                Assert.That(button, Is.Not.Null);
                Assert.That(button.ActivateOnFirstPointerPress, Is.True);
                audio.CuePlayed += Heard;
                yield return Click(button);
                Assert.That(TerminalMode.Requested, Is.True);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
                cues.Clear();
                yield return Key(UnityEngine.InputSystem.Key.F11);
                Assert.That(TerminalMode.Requested, Is.False);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
                cues.Clear();
                yield return Press(GamepadButton.Select);
                Assert.That(TerminalMode.Requested, Is.True);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
            }
            finally { audio.CuePlayed -= Heard; TerminalMode.SetRequested(original); }
        }

        [UnityTest] public IEnumerator TerminalHistoryButtonAndShortcutHaveOneUICue()
        {
            bool original = TerminalMode.Requested;
            settings.Exit_Clicked();
            yield return harness.Cleanup();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { IncludeStartingItems = true });
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard, mouse };
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group)
            {
                Assert.That(group, Is.SameAs(audio.UIAudioMixerGroup));
                cues.Add(cue);
            }
            try
            {
                TerminalMode.SetRequested(true);
                yield return new WaitForSecondsRealtime(.2f);
                var runtime = Common.Instance.GetComponent<TerminalRuntime>();
                var button = runtime.GetComponentsInChildren<SelectToActivateButton>().Single(b => b.name == "History");
                Assert.That(button.ActivateOnFirstPointerPress, Is.True);
                audio.CuePlayed += Heard;
                yield return Click(button);
                Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<EventHistoryDialog>());
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Confirm }));
                MenuManager.Instance.CurrentDialog.CloseDialog();
                yield return null;
                cues.Clear();
                yield return Key(UnityEngine.InputSystem.Key.H);
                Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<EventHistoryDialog>());
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.MenuOpen }));
            }
            finally { audio.CuePlayed -= Heard; TerminalMode.SetRequested(original); }
        }

        [UnityTest] public IEnumerator ResumeAndBackUseOnlyCancelFeedback()
        {
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group) => cues.Add(cue);
            audio.CuePlayed += Heard;
            try
            {
                Assert.That(((SelectToActivateButton)settings.ResumeButton).AudioRole,
                    Is.EqualTo(SelectToActivateButton.SoundRole.Cancel));
                yield return Click(settings.ResumeButton);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Cancel }), "Resume must not also confirm or play a duplicate close.");
                settings.ShowDialog();
                settings.ResumeButton.Select();
                yield return null;
                cues.Clear();
                yield return Key(UnityEngine.InputSystem.Key.Escape);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Cancel }));
            }
            finally { audio.CuePlayed -= Heard; }
        }

        [UnityTest] public IEnumerator VolumeRefreshIsSilentAndAdjustmentUsesUIChannel()
        {
            var slider = settings.GetComponentsInChildren<VolumeSlider>(true).First(s => s.VolumeParameterName == "EffectVolume");
            var audio = Common.Instance.AudioManager;
            var cues = new List<SoundCue>();
            float original = AudioPreferences.Read(slider.AudioMixerGroup, slider.VolumeParameterName);
            bool hadPreference = PlayerPrefs.HasKey(AudioPreferences.Key(slider.VolumeParameterName));
            void Heard(SoundCue cue, AudioClip clip, AudioMixerGroup group)
            {
                Assert.That(group, Is.SameAs(audio.UIAudioMixerGroup));
                cues.Add(cue);
            }
            audio.CuePlayed += Heard;
            try
            {
                slider.Refresh();
                Assert.That(cues, Is.Empty);
                yield return new WaitForSecondsRealtime(.06f);
                slider.OnSliderChanged(original > .5f ? .4f : .6f);
                Assert.That(cues, Is.EqualTo(new[] { SoundCue.Navigate }));
            }
            finally
            {
                audio.CuePlayed -= Heard;
                AudioPreferences.Set(slider.AudioMixerGroup, slider.VolumeParameterName, original);
                if (!hadPreference) PlayerPrefs.DeleteKey(AudioPreferences.Key(slider.VolumeParameterName));
                slider.Refresh();
            }
        }

        [UnityTest] public IEnumerator ControlsGuideScrollsAndReturnsToCategoryAt960By600()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var sizeProperty = view.GetType().GetProperty("selectedSizeIndex", flags);
            int originalSize = (int)sizeProperty.GetValue(view);
            try
            {
                view.GetType().GetMethod("SetCustomResolution", flags).Invoke(view, new object[] { new Vector2(960, 600), "Controls 960x600" });
                var tab = settings.TabGroup.TabContents.Single(t => t.Content.name == "Controls");
                tab.TabButton.Select(); yield return null; yield return null;
                var reader = tab.Content.GetComponentInChildren<TrainerPreviewScroll>();
                Assert.That(reader, Is.Not.Null);
                var scroll = reader.Scroll;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                var labels = scroll.content.GetComponentsInChildren<TMP_Text>();
                foreach (var label in labels)
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.text);
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24));
                }
                var viewportCorners = new Vector3[4];
                scroll.viewport.GetWorldCorners(viewportCorners);
                Assert.That(viewportCorners.All(corner => corner.x >= 0 && corner.x <= Screen.width && corner.y >= 0 && corner.y <= Screen.height), Is.True);

                scroll.verticalNormalizedPosition = 1;
                yield return Press(GamepadButton.DpadRight);
                Assert.That(Selected, Is.EqualTo(reader.gameObject));
                yield return Key(UnityEngine.InputSystem.Key.DownArrow);
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1), "Keyboard arrows scroll the guide.");
                for (int step = 0; step < 30 && scroll.verticalNormalizedPosition > .0001f; step++)
                    yield return Press(GamepadButton.DpadDown);
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThanOrEqualTo(.0001f));
                Canvas.ForceUpdateCanvases();
                foreach (var label in labels)
                {
                    label.ForceMeshUpdate();
                    var last = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Last(character => character.isVisible);
                    var point = RectTransformUtility.WorldToScreenPoint(null, label.transform.TransformPoint(last.bottomLeft));
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point), Is.True,
                        "The final guide line must be reachable inside the masked viewport.");
                }
                yield return Key(UnityEngine.InputSystem.Key.Escape);
                Assert.That(Selected, Is.EqualTo(tab.TabButton.gameObject));

                scroll.verticalNormalizedPosition = 1;
                var pointerPosition = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointerPosition, scroll = new Vector2(0, -120) });
                yield return null; yield return null;
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1), "Mouse wheel scrolls the guide without requiring directional navigation.");
            }
            finally { sizeProperty.SetValue(view, originalSize); }
        }

        [UnityTest] public IEnumerator GameplayExplanationChangesWhileDialogRemainsOpen()
        {
            var gameplay = settings.TabGroup.TabContents.Single(t => t.TabButton.GetComponentInChildren<TMP_Text>().text == "Gameplay");
            gameplay.TabButton.Select(); yield return null;
            var explanation = gameplay.Content.GetComponentsInChildren<TMP_Text>(true)
                .Single(t => t.text.Contains("Full Control lets"));
            Assert.That(explanation.text, Does.Contain("Press F"));
            yield return Press(GamepadButton.South);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(explanation.text, Does.Contain("Press Right stick press"));
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
            Assert.That(settings.ReturntoMainButton.gameObject.activeSelf, Is.False);
            Assert.That(settings.ResumeButton.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Close"));
            tabs.Last().TabButton.Select(); yield return Press(GamepadButton.DpadDown);
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
            Assert.That(sliders.Select(s => s.VolumeParameterName), Is.EquivalentTo(new[] { "MasterVolume", "MusicVolume", "EffectVolume", "UIVolume" }));
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
                foreach (var source in manager.UIAudioSources) source.Stop();
                manager.PlayUISound(manager.SoundEffects.Hover);
                Assert.That(manager.UIAudioSources.Any(s => s.isPlaying), Is.True, "A one-shot cue plays without replacing AudioSource.clip.");
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
