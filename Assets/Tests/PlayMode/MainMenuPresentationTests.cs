#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class MainMenuPresentationTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator SetUp() { harness = new GameTestHarness(); yield return null; }
        [UnityTearDown] public IEnumerator TearDown() { Time.timeScale = 1; yield return harness.Cleanup(); }

        [UnityTest]
        public IEnumerator VisualsLoopWithoutGameplayAndMenuRemainsUsable()
        {
            yield return harness.LoadMainMenu(null);
            var stage = GameObject.Find("Menu Stage");
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.GetComponentsInChildren<Character>(true), Is.Empty);
            Assert.That(stage.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(stage.GetComponentsInChildren<MonoBehaviour>(true).All(m => m is MenuSceneMotion), Is.True);
            var motions = stage.GetComponentsInChildren<MenuSceneMotion>();
            Assert.That(motions.Length, Is.EqualTo(5));
            Assert.That(motions.All(m => m.Clip != null && m.Animator != null && !m.Animator.applyRootMotion), Is.True);
            var positions = motions.Select(m => m.transform.position).ToArray();
            var bat = motions.First(m => m.Hover > 0);
            var skin = bat.GetComponentInChildren<SkinnedMeshRenderer>();
            var before = new Mesh(); var after = new Mesh();
            skin.BakeMesh(before);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.23f);
            skin.BakeMesh(after);
            Assert.That(after.vertices.Where((v, i) => (v - before.vertices[i]).sqrMagnitude > .000001f).Any(), Is.True, "Wingbeats continue on unscaled time.");
            Object.Destroy(before); Object.Destroy(after);
            for (int i = 0; i < motions.Length; i++)
            {
                motions[i].Sample(600);
                Assert.That(Vector3.Distance(motions[i].transform.position, positions[i]), Is.LessThan(.2f), "No accumulated root drift.");
            }
            Time.timeScale = 1;
            Directory.CreateDirectory("Temp/MainMenuValidation");
            ScreenCapture.CaptureScreenshot("Temp/MainMenuValidation/animated-menu.png");
            yield return null;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(menu.StartButton.GetComponentInParent<Canvas>().rootCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(menu.ContinueButton.activeSelf, Is.False);
            menu.Options_Clicked(); yield return null;
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            Common.Instance.GlobalSettings.Exit_Clicked(); yield return null;
            Assert.That(menu.NavigationHandler.gameObject.activeInHierarchy, Is.True);
            menu.StartGame_Clicked(); yield return null;
            Assert.That(Object.FindFirstObjectByType<ProtagonistClassPicker>(), Is.Not.Null);
            // Exit is only invoked in a player; verify the retained serialized handler in editor.
            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i =>
                    b.onClick.GetPersistentTarget(i) == menu && b.onClick.GetPersistentMethodName(i) == nameof(MainMenu.Exit_Clicked))), Is.True);
            var picker = Object.FindFirstObjectByType<ProtagonistClassPicker>();
            picker.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Back").onClick.Invoke();
            yield return null;
            Assert.That(menu.NavigationHandler.gameObject.activeInHierarchy, Is.True, "Cancelling class selection restores the menu.");
            menu.StartGame_Clicked(); yield return null;
            picker = Object.FindFirstObjectByType<ProtagonistClassPicker>();
            picker.transform.Find("Classes").GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            picker.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Begin with primary only").onClick.Invoke();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "new campaign from class selection");
            Assert.That(Common.Instance.GameSaveData.TownSaveData.RecruitedAlliesData[0].PrimaryClassId, Is.Not.Empty);
            Assert.That(Common.Instance.Travel.ReturnToMenu(), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>() != null && !Common.Instance.Travel.IsTransitioning, "return to animated menu");
            Assert.That(GameObject.Find("Menu Stage"), Is.Not.Null);
        }
    }
}
#endif
