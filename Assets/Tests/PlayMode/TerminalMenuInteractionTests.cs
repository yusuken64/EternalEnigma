#if UNITY_EDITOR
using System.Collections;
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
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TerminalMenuInteractionTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private bool hadPreference;
        private int oldPreference;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            hadPreference = PlayerPrefs.HasKey(TerminalMode.PreferenceKey);
            oldPreference = PlayerPrefs.GetInt(TerminalMode.PreferenceKey);
            TerminalMode.SetRequested(false);
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            TerminalMode.SetRequested(false);
            yield return harness.Cleanup();
            if (hadPreference) PlayerPrefs.SetInt(TerminalMode.PreferenceKey, oldPreference);
            else PlayerPrefs.DeleteKey(TerminalMode.PreferenceKey);
            PlayerPrefs.Save();
            inputScope.Dispose();
        }

        [UnityTest]
        public IEnumerator MainMenuBoundsTerminalTypeAndRestoresDeveloperControls()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var label = menu.StartButton.GetComponentInChildren<TMP_Text>();
            var font = label.font;
            var min = label.fontSizeMin;
            var max = label.fontSizeMax;
            var auto = label.enableAutoSizing;
            var developer = menu.GetComponent<MainMenuDeveloperControls>();
            bool developerEnabled = developer.enabled;
            TerminalMode.SetRequested(true);
            yield return null; yield return null;
            label.ForceMeshUpdate();
            Assert.That(label.enableAutoSizing, Is.True);
            Assert.That(label.fontSizeMin, Is.EqualTo(24));
            Assert.That(label.fontSizeMax, Is.EqualTo(40));
            Assert.That(label.isTextOverflowing, Is.False);
            var button = menu.StartButton.GetComponent<Button>();
            Assert.That(button.colors.pressedColor.a, Is.EqualTo(1), "Pressing a terminal menu action gives visible feedback.");
            Assert.That(button.targetGraphic.color, Is.EqualTo(Color.white));
            Assert.That(developer.enabled, Is.False);
            Assert.That(developer.Toggle.gameObject.activeSelf, Is.False, "LateUpdate cannot reveal a parchment developer button.");

            menu.Options_Clicked();
            yield return new WaitForSecondsRealtime(.15f);
            var hint = menu.StartButton.GetComponentInParent<Canvas>().transform.Find("Terminal menu hint");
            Assert.That(hint.gameObject.activeSelf, Is.False);
            var settings = Common.Instance.GlobalSettings;
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(settings.SettingsCanvas.transform.Find("Terminal modal background"), Is.Not.Null);
            settings.Exit_Clicked();
            yield return null;
            Assert.That(hint.gameObject.activeSelf, Is.True, "Closing Options restores the terminal menu footer.");
            TerminalMode.SetRequested(false);
            yield return null;
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.fontSizeMin, Is.EqualTo(min));
            Assert.That(label.fontSizeMax, Is.EqualTo(max));
            Assert.That(label.enableAutoSizing, Is.EqualTo(auto));
            Assert.That(developer.enabled, Is.EqualTo(developerEnabled));
        }

        [UnityTest]
        public IEnumerator PointerClicksDrainDuringHeldTerminalNavigationAndSubmitOnlyOnce()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var module = MenuUIInputModule.Active;
            module.actionsAsset.devices = new InputDevice[] { keyboard, mouse };
            TerminalMode.SetRequested(true);
            yield return null; yield return null;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var options = menu.StartButton.transform.parent.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentInChildren<TMP_Text>().text.Contains("OPTIONS"));
            int optionsClicks = 0;
            options.onClick.AddListener(() => optionsClicks++);
            menu.StartButton.GetComponent<Button>().Select();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            yield return null;
            var point = RectTransformUtility.WorldToScreenPoint(null, options.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Assert.That(optionsClicks, Is.EqualTo(1), "A held navigation key cannot delay a completed pointer click.");
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(optionsClicks, Is.EqualTo(1), "Releasing navigation cannot replay a cached click.");

            var close = Common.Instance.GlobalSettings.ResumeButton;
            int closeClicks = 0;
            close.onClick.AddListener(() => closeClicks++);
            close.Select();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(closeClicks, Is.EqualTo(1), "Terminal submit and native submit must not both activate the control.");
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(options.gameObject));
        }
    }
}
#endif
