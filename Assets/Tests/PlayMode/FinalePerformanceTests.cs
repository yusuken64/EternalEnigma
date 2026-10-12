#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [Explicit("Run manually with Tools > Eternal Enigma > Tests > Run FinalePerformance")]
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FinalePerformanceTests
    {
        [Serializable] sealed class Report
        {
            public int Seed, Actions, Width, Height;
            public double InitializationSeconds, ActionSeconds;
            public FinalePerformance.Sample[] Samples;
        }
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { if (!SessionState.GetBool("EternalEnigma.Finale.ManualChecks", false)) Assert.Ignore("Use the manual finale harness."); harness = new GameTestHarness { TimeoutSeconds = 600 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            FinalePerformance.Enabled = false;
            if (AutoplayRunner.Active != null)
            { AutoplayRunner.Active.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "profile cleanup"); }
            yield return harness.Cleanup();
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator MeasureProductionFinaleTurns()
        {
            yield return harness.LoadMainMenu(new TestScenario().CreateSave());
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "main menu");
            var options = AutoplayOptions.FinaleDebug();
            options.NoAnimations = true; options.Speed = 32; options.ReportRoot = "Temp/FinaleDebug/PerformanceRuns";
            var start = Time.realtimeSinceStartupAsDouble;
            AutoplayRunner.WatchDemo(options);
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Game.Instance?.IsReady == true && run.Report.Finale != null, "profile floor one");
            var report = new Report { Seed = 42, Width = Game.Instance.CurrentDungeon.dungeonWidth,
                Height = Game.Instance.CurrentDungeon.dungeonHeight, InitializationSeconds = Time.realtimeSinceStartupAsDouble - start };
            int actions = run.Report.Actions; start = Time.realtimeSinceStartupAsDouble;
            FinalePerformance.Begin();
            yield return harness.WaitUntil(() => run.Report.Actions >= actions + 300 || !run.Running, "300 production commands");
            report.ActionSeconds = Time.realtimeSinceStartupAsDouble - start; report.Actions = run.Report.Actions - actions;
            report.Samples = FinalePerformance.Finish();
            string label = SessionState.GetString("EternalEnigma.Finale.ProfileLabel", "latest");
            Directory.CreateDirectory("Temp/FinaleDebug");
            File.WriteAllText("Temp/FinaleDebug/performance-" + label + ".json", JsonUtility.ToJson(report, true));
            Assert.That(run.Running, Is.True, run.Report.Reason); Assert.That(run.Report.IssueCount, Is.Zero);
            run.Stop(); yield return harness.WaitUntil(() => !run.Finalizing, "profile artifacts");
            Game.Instance.UpdateMiniMap();
            var minimap = Object.FindFirstObjectByType<Minimap>();
            var mapPixels = minimap.minimapTexture.GetPixels32();
            var fogPixels = minimap.FogOverlay.VisibilityTexture.GetPixels32();
            minimap.InvalidateTerrain(); Game.Instance.UpdateMiniMap();
            CollectionAssert.AreEqual(mapPixels, minimap.minimapTexture.GetPixels32(), "Incremental markers and terrain match a complete redraw.");
            CollectionAssert.AreEqual(fogPixels, minimap.FogOverlay.VisibilityTexture.GetPixels32(), "Incremental fog matches a complete redraw.");
        }
    }
}
#endif
