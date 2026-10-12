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
    [Explicit("Run manually with Tools > Eternal Enigma > Tests > Run FinaleControls")]
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class FinaleAutoplayControlTests
    {
        GameTestHarness harness;
        GameSaveData original;
        string saved;
        float timeScale, fixedStep;
        DungeonAnimationMode animation;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (!SessionState.GetBool("EternalEnigma.Finale.ManualChecks", false)) Assert.Ignore("Use the manual finale harness.");
            harness = new GameTestHarness { TimeoutSeconds = 300 };
            timeScale = Time.timeScale; fixedStep = Time.fixedDeltaTime;
            animation = DungeonPreferences.AnimationMode;
            yield return harness.LoadMainMenu(new TestScenario { Gold = 321 }.CreateSave());
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>()?.IsReady == true, "main menu");
            original = Common.Instance.GameSaveData; saved = harness.Store.Json;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            if (AutoplayRunner.Active != null)
            {
                AutoplayRunner.Active.ExitDemo();
                yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "isolated finale cleanup");
            }
            yield return harness.Cleanup();
        }

        IEnumerator StartFinale(int seed = 42)
        {
            var options = AutoplayOptions.FinaleDebug();
            options.Seed = seed; options.NoAnimations = true; options.Speed = 32;
            options.ReportRoot = "Temp/FinaleDebug/ControlChecks";
            AutoplayRunner.WatchDemo(options);
            var run = AutoplayRunner.Active;
            yield return harness.WaitUntil(() => Game.Instance?.IsReady == true && run.Report.Finale != null, "direct finale ready");
            run.SetPaused(true);
            Assert.That(run.Report.Finale.PristineStart, Is.True);
            Assert.That(Game.Instance.Finale.Session.CurrentFloor, Is.EqualTo(1));
            Assert.That(Common.Instance.CampaignContext.State.Finished, Is.False);
            Assert.That(Common.Instance.GameSaveData, Is.Not.SameAs(original));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
        }

        IEnumerator ExitAndCheckRestoration()
        {
            AutoplayRunner.Active.ExitDemo();
            yield return harness.WaitUntil(() => AutoplayRunner.Active == null, "restore player session");
            Assert.That(Common.Instance.GameSaveData, Is.SameAs(original));
            Assert.That(harness.Store.Json, Is.EqualTo(saved));
            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(.000001f));
            Assert.That(DungeonPreferences.AnimationMode, Is.EqualTo(animation));
            Assert.That(AutoplayRunner.BlocksPlayerInput, Is.False);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator StopKeepsIncompleteEvidenceAndRestoresOriginalSave()
        {
            yield return StartFinale();
            var run = AutoplayRunner.Active;
            int actions = run.Report.Actions;
            run.SetSpeed(4);
            Assert.That(Time.timeScale, Is.Zero);
            run.Stop();
            yield return harness.WaitUntil(() => !run.Finalizing, "stop report");
            Assert.That(run.Report.Outcome, Is.EqualTo("Stopped"));
            Assert.That(run.Report.ArtifactsComplete, Is.True);
            Assert.That(run.Report.Finale.FinalExitCompleted || run.Report.Finale.HomecomingCompleted, Is.False);
            run.SetPaused(false);
            yield return null; yield return null;
            Assert.That(run.Report.Actions, Is.EqualTo(actions));
            foreach (var file in new[] { "report.json", "save.json", "actions.jsonl", "issues.jsonl", "final.png" })
                Assert.That(File.Exists(Path.Combine(run.DirectoryPath, file)), Is.True, file);
            string path = Path.Combine(run.DirectoryPath, "report.json");
            yield return ExitAndCheckRestoration();
            Assert.That(JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(path)).Outcome, Is.EqualTo("Stopped"));
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TakeoverRetainsTheLiveFinaleAndIsolatedSave()
        {
            yield return StartFinale(43);
            var run = AutoplayRunner.Active; var game = Game.Instance; var session = game.Finale.Session;
            var party = game.Allies.ToArray();
            run.TakeControl();
            yield return harness.WaitUntil(() => run.PlayerControlled, "manual control at a turn boundary");
            Assert.That(Game.Instance, Is.SameAs(game));
            Assert.That(game.Finale.Session, Is.SameAs(session));
            CollectionAssert.AreEquivalent(party, game.Allies);
            Assert.That(session.CurrentFloor, Is.EqualTo(1));
            Assert.That(run.Report.Seed, Is.EqualTo(43));
            Assert.That(run.Report.Outcome, Is.EqualTo("PlayerControl"));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(AutoplayRunner.BlocksPlayerInput, Is.False);
            Assert.That(AutoplayRunner.GodmodeFor(harness.Ally), Is.False);
            Assert.That(AutoplayRunner.InfiniteResourcesFor(harness.Ally), Is.False);
            Assert.That(AutoplayRunner.InfiniteStrengthFor(harness.Ally), Is.False);
            int actions = run.Report.Actions;
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(run.Report.Actions, Is.EqualTo(actions), "Manual input does not restart autoplay.");
            SaveSystem.SaveData(Common.Instance.GameSaveData);
            Assert.That(harness.Store.Json, Is.EqualTo(saved), "Manual demo play still uses the isolated store.");
            yield return ExitAndCheckRestoration();
        }
    }
}
#endif

