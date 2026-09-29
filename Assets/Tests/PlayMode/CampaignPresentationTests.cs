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
        public IEnumerator HeroPickerUsesRealArtworkAndBackDoesNotStartCampaign()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.StartGame_Clicked();
            yield return null;
            var picker = Object.FindFirstObjectByType<ProtagonistHeroPicker>();
            Assert.That(picker, Is.Not.Null);
            Assert.That(picker.GetComponentInChildren<RawImage>().texture, Is.TypeOf<RenderTexture>());
            var heroes = menu.TownConfiguration != null ? menu.TownConfiguration.AllyCatalog : TownSceneLoader.Default.AllyCatalog;
            Assert.That(picker.GetComponentsInChildren<Button>().Length, Is.EqualTo(8 + 4));
            Assert.That(picker.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Choose your hero"), Is.True);
            Canvas.ForceUpdateCanvases();
            Directory.CreateDirectory("Temp/UIValidation");
            ScreenCapture.CaptureScreenshot("Temp/UIValidation/hero-picker.png");
            yield return new WaitForSecondsRealtime(.5f);
            picker.GetComponentsInChildren<Button>().Single(b => b.name == "Next").onClick.Invoke();
            yield return null;
            Assert.That(picker.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "2 / 3"), Is.True);
            Assert.That(picker.GetComponentsInChildren<Button>().Any(b => b.name.Contains(heroes[8].Name)), Is.True);
            picker.GetComponentsInChildren<Button>().Single(b => b.name == "Previous").onClick.Invoke();
            yield return null;
            Assert.That(picker.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "1 / 3"), Is.True);
            picker.GetComponentsInChildren<Button>().Single(b => b.name == "Back").onClick.Invoke();
            yield return null;
            Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<ProtagonistPreview>(), Is.Null);
            Assert.That(menu.NavigationHandler.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator SelectedHeroConfirmsExactlyOnce()
        {
            int confirmations = 0;
            var heroes = TownSceneLoader.Default.AllyCatalog;
            TownAlly chosen = null;
            var picker = ProtagonistHeroPicker.Show(heroes, hero => { chosen = hero; confirmations++; }, () => Assert.Fail("Unexpected cancel"));
            picker.GetComponentsInChildren<Button>().Single(b => b.name.StartsWith(heroes[1].Name + "  ")).onClick.Invoke();
            yield return null;
            var begin = picker.GetComponentsInChildren<Button>().Single(b => b.name == "Begin journey");
            begin.onClick.Invoke(); begin.onClick.Invoke();
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(chosen, Is.SameAs(heroes[1]));
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
                Assert.That((button.targetGraphic as Image)?.sprite, Is.EqualTo(GameUITheme.Current.Button), button.name);
            }
            var prefab = Resources.Load<Button>("UI/GameButton");
            Assert.That((prefab.targetGraphic as Image)?.sprite, Is.EqualTo(GameUITheme.Current.Button));
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
            Object.FindFirstObjectByType<MainMenu>().StartGame();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "campaign town");
            yield return new WaitForSecondsRealtime(.5f);
            var town = Object.FindFirstObjectByType<Town>();
            var hud = town.GetComponent<CampaignHUD>();
            Assert.That(hud, Is.Not.Null);
            hud.GetComponentsInChildren<Button>().Single(b => b.name == "Party  [P / B]").onClick.Invoke();
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
