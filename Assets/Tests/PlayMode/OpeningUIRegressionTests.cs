#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class OpeningUIRegressionTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameTestHarness harness;
        TestInputScope inputs;
        Gamepad pad;
        DungeonAnimationMode? animation;
        UnityEditor.EditorWindow view;
        int size;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputs = new TestInputScope();
            harness = new GameTestHarness();
            animation = DungeonPreferences.AnimationOverride;
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            size = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] {new Vector2(960, 600), "Opening UI 960x600"});
            Directory.CreateDirectory("Temp/OpeningUIFixes");
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            yield return harness.Cleanup();
            DungeonPreferences.AnimationOverride = animation;
            view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, size);
            inputs.Dispose();
        }

        [UnityTest] public IEnumerator EmptyThirdSlotActivatesWithPointerAndControllerWithoutWriting()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var save = menu.CreateNewSave(42);
            save.Summary = new CampaignSaveSummary { Town = "Opening town", Region = "Starter region", RequiredTotal = 5 };
            SaveSystem.SaveData(0, save); SaveSystem.SaveData(1, save);
            string first = harness.Store.Read(0), second = harness.Store.Read(1);
            int writes = harness.Store.Writes;
            menu.StartGame_Clicked(); yield return null;
            var browser = Object.FindFirstObjectByType<CampaignSlots>();
            Assert.That(browser.Slots.All(slot => slot.GetComponents<CampaignSlotView>().Length == 1), Is.True);
            Assert.That(browser.Slots[2].Label.text, Does.Contain("Empty"));
            Assert.That(browser.Slots[2].Portraits.All(portrait => !portrait.gameObject.activeSelf), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            Click(browser.Slots[2].Button, pointer);
            Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Null, "First press selects the empty slot.");
            Assert.That(browser.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("Choose this slot")), Is.True);
            yield return Capture("empty-third-slot");
            Click(browser.Slots[2].Button, pointer); yield return null;
            Assert.That(SaveSystem.ActiveSlot, Is.EqualTo(2));
            var picker = Object.FindFirstObjectByType<ProtagonistHeroPicker>();
            Assert.That(picker, Is.Not.Null);
            picker.BackButton.onClick.Invoke(); yield return null;

            SaveSystem.ActiveSlot = 0;
            menu.StartGame_Clicked(); yield return null;
            pad = InputSystem.AddDevice<Gamepad>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad };
            yield return Press(GamepadButton.DpadRight); yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(browser.Slots[2].Button.gameObject));
            yield return Press(GamepadButton.South);
            Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Not.Null);
            yield return Press(GamepadButton.East);
            Assert.That(harness.Store.Writes, Is.EqualTo(writes));
            Assert.That(harness.Store.Read(0), Is.EqualTo(first));
            Assert.That(harness.Store.Read(1), Is.EqualTo(second));
            Assert.That(harness.Store.Read(2), Is.Null);
        }

        [UnityTest] public IEnumerator UnreadableSlotRequiresReplacementConfirmationAndCancelPreservesIt()
        {
            yield return harness.LoadMainMenu(null);
            harness.Store.Write(2, "invalid campaign bytes");
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.StartGame_Clicked(); yield return null;
            var browser = Object.FindFirstObjectByType<CampaignSlots>();
            browser.Slots[2].Button.onClick.Invoke(); yield return null;
            var choice = Object.FindFirstObjectByType<CampaignChoice>();
            Assert.That(choice, Is.Not.Null);
            choice.Back.onClick.Invoke(); yield return null;
            Assert.That(harness.Store.Read(2), Is.EqualTo("invalid campaign bytes"));
            Assert.That(browser.gameObject.activeInHierarchy, Is.True);
            browser.BackButton.onClick.Invoke();
            menu.Continue_Clicked(); yield return null;
            browser.Slots[2].Button.onClick.Invoke(); yield return null;
            Assert.That(browser.gameObject.activeInHierarchy, Is.True);
            Assert.That(browser.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("cannot be continued")), Is.True);
        }

        [UnityTest] public IEnumerator SettingsExplanationsFitEverySpeedModeAt960By600()
        {
            yield return harness.LoadMainMenu(null);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.Options_Clicked(); yield return null;
            var settings = Common.Instance.GlobalSettings;
            var tab = settings.TabGroup.TabContents.Single(tab => tab.Content.name == "Gameplay options");
            tab.TabButton.Select(); yield return null;
            for (int mode = 0; mode < 4; mode++)
            {
                DungeonPreferences.AnimationOverride = (DungeonAnimationMode)mode;
                settings.GetComponent<DungeonOptions>().SendMessage("Refresh");
                Canvas.ForceUpdateCanvases();
                foreach (var label in tab.Content.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.text);
                }
                var explanation = tab.Content.GetComponentsInChildren<TMP_Text>().Single(text => text.text.StartsWith("Full Control lets"));
                var history = tab.Content.GetComponentsInChildren<Button>().Single(button => button.GetComponentInChildren<TMP_Text>().text == "Event history");
                var parent = (RectTransform)tab.Content.transform;
                var descriptionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, explanation.transform);
                var historyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, history.transform);
                Assert.That(descriptionBounds.min.y, Is.GreaterThan(historyBounds.max.y));
                yield return Capture("settings-mode-" + mode);
            }
        }

        [UnityTest] public IEnumerator TownSkillPreviewFollowsFocusWithoutSpendingResources()
        {
            yield return harness.LoadTown(new TestScenario {Skills = new[] {"Cure", "Double Strike"}}.CreateSave());
            var hero = Object.FindFirstObjectByType<Town>().TownPlayer.ControllingTownAlly;
            int sp = hero.Sp;
            var manager = Object.FindFirstObjectByType<TownMenuManager>();
            manager.OpenPartyMenu(PartyMenuTab.Skills); yield return null;
            var menu = manager.PartyMenu;
            Assert.That(menu.DetailsControl.Scroll.gameObject.activeInHierarchy, Is.True);
            foreach (string name in new[] {"Cure", "Double Strike"})
            {
                menu.EntryButtons.Single(button => button.GetComponentInChildren<TMP_Text>().text.StartsWith(name)).Select();
                yield return null;
                Assert.That(menu.Details.text, Does.Contain(Common.Instance.SkillManager.GetSkillByName(name).Description));
                Assert.That(menu.Details.text, Does.Contain(name));
                Assert.That(hero.Sp, Is.EqualTo(sp));
                yield return Capture("town-skill-" + name.Replace(" ", "-"));
            }
        }

        static void Click(Button button, PointerEventData pointer)
        {
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
        }
        static IEnumerator Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot("Temp/OpeningUIFixes/" + name + ".png");
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
#endif
