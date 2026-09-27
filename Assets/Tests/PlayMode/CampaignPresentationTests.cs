#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class CampaignPresentationTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness(); yield return harness.LoadMainMenu(null); }
        [UnityTearDown] public IEnumerator Cleanup() { yield return harness.Cleanup(); }

        [UnityTest]
        public IEnumerator PickerUsesRealArtworkAndBackDoesNotStartCampaign()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.StartGame_Clicked();
            yield return null;
            var picker = Object.FindFirstObjectByType<ProtagonistClassPicker>();
            Assert.That(picker, Is.Not.Null);
            Assert.That(picker.GetComponentInChildren<RawImage>().texture, Is.TypeOf<RenderTexture>());
            var classes = ClassCatalog.Load().Classes;
            Assert.That(picker.GetComponentsInChildren<Button>().Length, Is.EqualTo(classes.Count + 1));
            foreach (var cls in classes) Assert.That(Resources.Load<Sprite>("UI/" + cls.DisplayName), Is.Not.Null, cls.DisplayName);
            Canvas.ForceUpdateCanvases();
            Directory.CreateDirectory("Temp/UIValidation");
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/primary.png");
            yield return new WaitForSecondsRealtime(.5f);
            var first = picker.GetComponentsInChildren<Button>().First(b => b.name == classes[0].DisplayName);
            first.onClick.Invoke();
            yield return null;
            Assert.That(picker.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Choose a secondary class"), Is.True);
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/secondary.png");
            yield return new WaitForSecondsRealtime(.5f);
            picker.GetComponentsInChildren<Button>().Single(b => b.name == "Back").onClick.Invoke();
            yield return null;
            Assert.That(picker.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Choose your primary class"), Is.True);
            picker.GetComponentsInChildren<Button>().Single(b => b.name == "Back").onClick.Invoke();
            yield return null;
            Assert.That(Object.FindFirstObjectByType<ProtagonistClassPicker>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<ProtagonistPreview>(), Is.Null);
            Assert.That(menu.NavigationHandler.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PrimaryAndSecondaryConfirmExactlyOnce()
        {
            int confirmations = 0;
            var classes = ClassCatalog.Load().Classes;
            ClassDefinition primary = null, secondary = null;
            var picker = ProtagonistClassPicker.Show(classes, (p, s) => { primary = p; secondary = s; confirmations++; }, () => Assert.Fail("Unexpected cancel"));
            picker.GetComponentsInChildren<Button>().Single(b => b.name == classes[0].DisplayName).onClick.Invoke();
            yield return null;
            var second = picker.GetComponentsInChildren<Button>().Single(b => b.name == classes[1].DisplayName);
            second.onClick.Invoke(); second.onClick.Invoke();
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(primary, Is.SameAs(classes[0])); Assert.That(secondary, Is.SameAs(classes[1]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneAndInstantiatedPrefabButtonsHaveSerializedStyles()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Directory.CreateDirectory("Temp/UIValidation");
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/main-menu.png");
            yield return new WaitForSecondsRealtime(.3f);
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                Assert.That((button.targetGraphic as Image)?.sprite, Is.EqualTo(Resources.Load<Sprite>("UI/Button")), button.name);
            }
            var prefab = Resources.Load<Button>("UI/GameButton");
            Assert.That((prefab.targetGraphic as Image)?.sprite, Is.EqualTo(Resources.Load<Sprite>("UI/Button")));
            var dynamicButton = GameUISkin.Button(Object.FindFirstObjectByType<Canvas>().transform,
                "Dynamic test button", Vector2.zero, Vector2.one, null);
            Assert.That((dynamicButton.targetGraphic as Image)?.sprite, Is.EqualTo((prefab.targetGraphic as Image)?.sprite));
            Assert.That(dynamicButton.colors, Is.EqualTo(prefab.colors));
            Object.Destroy(dynamicButton.gameObject);
        }

        [UnityTest]
        public IEnumerator CampaignUsesTravelHudAndPartyCanCloseBeforeLeavingTown()
        {
            harness.TimeoutSeconds = 100;
            Object.FindFirstObjectByType<MainMenu>().StartGame(null, null);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "campaign town");
            yield return new WaitForSecondsRealtime(.5f);
            var town = Object.FindFirstObjectByType<Town>();
            var hud = town.GetComponent<CampaignHUD>();
            Assert.That(hud, Is.Not.Null);
            hud.GetComponentsInChildren<Button>().Single(b => b.name == "Party  [P]").onClick.Invoke();
            yield return null;
            Assert.That(hud.IsPartyOpen, Is.True);
            Directory.CreateDirectory("Temp/UIValidation");
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/town-party.png");
            yield return new WaitForSecondsRealtime(.3f);
            hud.GetComponentsInChildren<Button>().Single(b => b.name == "Done").onClick.Invoke();
            Assert.That(hud.IsPartyOpen, Is.False);
            var context = Common.Instance.CampaignContext;
            Assert.That(context.BeginTownDungeon("story-0"), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            Assert.That(Common.Instance.Travel.ExitTown(town), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<OverworldScene>()?.IsReady == true, "campaign overworld");
            yield return new WaitForSecondsRealtime(.5f);
            var world = Object.FindFirstObjectByType<OverworldScene>();
            Assert.That(world.Context.IsSandbox, Is.False);
            Assert.That(world.GetComponent<OverworldSandboxControls>().enabled, Is.False);
            Assert.That(world.GetComponent<CampaignHUD>(), Is.Not.Null);
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/overworld.png");
            yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
#endif
