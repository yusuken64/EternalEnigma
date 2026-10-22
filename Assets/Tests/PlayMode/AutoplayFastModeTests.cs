#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class AutoplayFastModeTests
    {
        GameTestHarness harness;
        System.IDisposable preferences;
        DungeonAnimationMode? originalAnimation;
        UnityEditor.EditorWindow view;
        int originalResolution;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Setup()
        {
            preferences = GamePreferences.Isolate();
            originalAnimation = DungeonPreferences.AnimationOverride;
            DungeonPreferences.AnimationOverride = null;
            DungeonPreferences.AnimationMode = DungeonAnimationMode.AnimateAlliedActions;
            TerminalMode.SetRequested(false);
            harness = new GameTestHarness { TimeoutSeconds = 120 };
            Directory.CreateDirectory("Temp/FastAutoplay");
            view = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            originalResolution = (int)view.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(view);
            view.GetType().GetMethod("SetCustomResolution", Flags).Invoke(view, new object[] { new Vector2(1920,1080), "Fast autoplay controls" });
            view.Repaint();
            yield return harness.LoadMainMenu(null);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            try
            {
                if (AutoplayRunner.Active != null)
                {
                    AutoplayRunner.Active.Report.ValidationOnly = true;
                    AutoplayRunner.Active.ExitDemo();
                    yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "fast autoplay cleanup");
                }
                yield return harness.Cleanup();
            }
            finally
            {
                if (view != null) view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, originalResolution);
                DungeonPreferences.AnimationOverride = originalAnimation; preferences.Dispose();
            }
        }

        static IEnumerator Capture(AutoplaySetup setup, string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var label in setup.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, label.text);
            }
            Assert.That(Screen.width, Is.EqualTo(1920)); Assert.That(Screen.height, Is.EqualTo(1080));
            ScreenCapture.CaptureScreenshot("Temp/FastAutoplay/" + name + "-1920x1080.png");
            yield return null;
        }

        [UnityTest] public IEnumerator DebugAutoplayMenuLaunchesFastPresetAndPreservesSelectedOptions()
        {
            var originalSave = Common.Instance.GameSaveData;
            string saved = harness.Store.Json;
            var menu = Object.FindFirstObjectByType<MainMenu>();
            menu.DebugAutoplay_Clicked();
            var setup = Object.FindFirstObjectByType<AutoplaySetup>();
            Assert.That(setup.FastMode, Is.Not.Null, "The shipped Debug autoplay menu needs an authored Fast mode control.");
            Assert.That(setup.FastModeLabel.text, Is.EqualTo("Fast mode Off"));
            yield return Capture(setup, "debug-autoplay-normal");
            setup.Speed.onClick.Invoke(); // 1x -> 2x, restored when Fast mode is disabled.
            setup.Seed.text = "73421"; setup.RunCount.text = "3";
            setup.Coverage.onClick.Invoke(); setup.InfiniteResources.onClick.Invoke();
            setup.FastMode.onClick.Invoke();
            Assert.That(setup.SpeedLabel.text, Does.Contain("32"));
            Assert.That(setup.Speed.interactable, Is.False);
            setup.Speed.onClick.Invoke();
            Assert.That(setup.SpeedLabel.text, Does.Contain("32"), "A stale disabled speed callback must not change the preset.");
            setup.FastMode.onClick.Invoke();
            Assert.That(setup.SpeedLabel.text, Does.Contain("2"));
            Assert.That(setup.Speed.interactable, Is.True);
            setup.FastMode.onClick.Invoke();
            setup.Seed.text = int.MaxValue.ToString(); setup.RunCount.text = "2";
            Assert.That(setup.Launch.interactable, Is.False, "Fast mode still validates the selected seed range.");
            setup.Seed.text = "73421"; setup.RunCount.text = "3";
            yield return Capture(setup, "debug-autoplay-fast");
            setup.Launch.onClick.Invoke();
            var run = AutoplayRunner.Active;
            Assert.That(run, Is.Not.Null);
            run.SetPaused(true); run.Report.ValidationOnly = true;
            Assert.That(run.Options.Speed, Is.EqualTo(32));
            Assert.That(run.Options.NoAnimations, Is.True);
            Assert.That(run.Options.Seed, Is.EqualTo(73421));
            Assert.That(run.Options.RunCount, Is.EqualTo(3));
            Assert.That(run.Options.DebugCoverage || run.Options.InfiniteResources, Is.False);
            Assert.That(run.Options.DebugPlaythrough && run.Options.Godmode, Is.True);
            Assert.That(AutoplayRunner.SuppressesAnimations, Is.True);
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.NoAnimations));
            Assert.That(GamePreferences.GetInt("Dungeon.AnimationModeV2", -1), Is.EqualTo((int)DungeonAnimationMode.AnimateAlliedActions));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            run.ExitDemo();
            yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "fast preset exit");
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(originalSave));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.AnimateAlliedActions));
        }

        [UnityTest] public IEnumerator PlaybackCanLeaveAndReenterNoAnimationsWithoutChangingPlayerPreference()
        {
            AutoplayRunner.WatchDemo(new AutoplayOptions { Speed = 32, NoAnimations = true });
            var run = AutoplayRunner.Active; run.SetPaused(true); run.Report.ValidationOnly = true;
            var panel = Object.FindFirstObjectByType<AutoplayPanel>();
            panel.Animations.onClick.Invoke(); panel.Refresh();
            Assert.That(run.Options.NoAnimations, Is.False);
            Assert.That(AutoplayRunner.SuppressesAnimations, Is.False);
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.Normal));
            Assert.That(panel.AnimationLabel.text, Does.Contain("Normal"));
            Assert.That(Time.timeScale, Is.Zero, "Changing animation mode must not unpause the run.");
            foreach (var expected in new[] { DungeonAnimationMode.AnimateAlliedActions,
                DungeonAnimationMode.AnimateControlledHeroActions, DungeonAnimationMode.NoAnimations })
            {
                panel.Animations.onClick.Invoke();
                Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(expected));
            }
            Assert.That(run.Options.NoAnimations && AutoplayRunner.SuppressesAnimations, Is.True,
                "The live No animations control must enable the same fast turn path as the launch preset.");
            var report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(Path.Combine(run.DirectoryPath, "report.json")));
            Assert.That(report.Options.NoAnimations, Is.True);
            Assert.That(GamePreferences.GetInt("Dungeon.AnimationModeV2", -1), Is.EqualTo((int)DungeonAnimationMode.AnimateAlliedActions));
            run.ExitDemo();
            yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "restore player animations");
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.AnimateAlliedActions));
        }

        [UnityTest] public IEnumerator StandaloneBatchWaitsForItsInitialMenuReloadBeforeStartingCampaign()
        {
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(menu.IsReady, Is.True);
            var originalSave = menu.CreateNewSave(73420);
            Common.Instance.GameSaveData = originalSave;
            SaveSystem.SaveData(originalSave);
            string saved = harness.Store.Json;
            AutoplayRunner.StartBatch(new AutoplayOptions { Seed = 42, RunCount = 1,
                Speed = 32, NoAnimations = true, DebugPlaythrough = true, DebugCoverage = true,
                TimeLimitMinutes = 5, StallSeconds = 300, ReportRoot = "Temp/FastAutoplay" });
            var run = AutoplayRunner.Active;
            run.Report.ValidationOnly = true;
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                run.Report.Actions > 1, "standalone fast batch opening actions after the initial menu reload");
            run.SetPaused(true);
            Assert.That(run.Running, Is.True, run.Status);
            Assert.That(run.Report.IssueCount, Is.Zero, run.Report.Reason);
            Assert.That(Object.FindFirstObjectByType<MainMenu>(), Is.Null);
            Assert.That(Common.Instance.CampaignContext.State.LocationId, Is.EqualTo("town-0"));
            Assert.That(run.Options.Speed, Is.EqualTo(32));
            Assert.That(run.Options.NoAnimations, Is.True);
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.NoAnimations));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            run.ExitDemo();
            yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "standalone fast batch restores the original session");
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(originalSave));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(DungeonAnimationMode.AnimateAlliedActions));
        }
    }
}
#endif
