#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [Explicit("Run manually with Tools > Eternal Enigma > Tests > Run FinaleAutoplay")]
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FinaleAutoplayTests
    {
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { if (!SessionState.GetBool("EternalEnigma.Finale.ManualChecks", false)) Assert.Ignore("Use the manual finale harness."); harness = new GameTestHarness { TimeoutSeconds = 3500 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            if (AutoplayRunner.Active != null)
            { AutoplayRunner.Active.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "finale autoplay cleanup"); }
            yield return harness.Cleanup();
        }
        [UnityTest, Timeout(3600000)]
        public IEnumerator DirectDebugClearsTwoFloorsAndWakesThroughProductionGameplay()
        {
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "main menu");
            var original = Common.Instance.GameSaveData; string saved = harness.Store.Json;
            var options = AutoplayOptions.FinaleDebug(); options.NoAnimations = true; options.Speed = 32;
            AutoplayRunner.WatchDemo(options);
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => run.Report.Finale != null || !run.Running, "direct finale floor one");
            Assert.That(run.Report.Finale?.PristineStart, Is.True, run.Report.Reason);
            run.SetPaused(true); int actions = run.Report.Actions;
            yield return null; yield return null; Assert.That(run.Report.Actions, Is.EqualTo(actions)); run.SetPaused(false);
            yield return harness.WaitUntil(() => !run.SessionRunning, "both production finale floors and homecoming");
            Assert.That(run.Report.Outcome, Is.EqualTo("Victory"), run.Report.Reason);
            Assert.That(run.Report.Finale.HomecomingCompleted, Is.True);
            Assert.That(run.Report.Finale.StairTransitions, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(run.Report.Finale.Floors.SelectMany(f => f.Regions).All(r => r.Cleared && r.Remaining == 0), Is.True);
            Assert.That(run.Report.Campaign.Finished, Is.True); Assert.That(run.Report.EligibleForBalance, Is.False);
            Assert.That(run.Report.InfiniteStrength, Is.False); Assert.That(run.Report.IssueCount, Is.Zero);
            foreach (var file in new[] { "report.json", "save.json", "actions.jsonl", "issues.jsonl", "final.png" })
                Assert.That(File.Exists(Path.Combine(run.DirectoryPath, file)), Is.True, file);
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            run.ExitDemo(); yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "restore original session");
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(original)); Assert.That(harness.Store.Json, Is.EqualTo(saved));
        }
    }
}
#endif
