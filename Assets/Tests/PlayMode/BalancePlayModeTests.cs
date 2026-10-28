#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class BalancePlayModeTests
    {
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup()
        {
            if (!SessionState.GetBool("EternalEnigma.Balance.ManualChecks",false)) Assert.Ignore("Use the explicit Balance harness.");
            harness = new GameTestHarness { TimeoutSeconds=120 };
            yield return harness.LoadMainMenu(new TestScenario { Gold=321 }.CreateSave());
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (harness == null) yield break;
            if (AutoplayRunner.Active != null)
            { AutoplayRunner.Active.ExitDemo(); yield return harness.WaitUntil(()=>AutoplayRunner.Active==null,"balance cleanup"); }
            yield return harness.Cleanup();
        }

        [UnityTest]
        public IEnumerator RealOpeningWritesProgressionAndRestoresSaveAndExperimentalCosts()
        {
            string original = harness.Store.Json;
            var adjusted = Resources.LoadAll<Skill>("Classes/Skills").First(s=>s.ManaCost>=3);
            int originalCost=adjusted.ManaCost;
            var options = new AutoplayOptions { Seed=42, Speed=32, NoAnimations=true, Godmode=false, InfiniteResources=false,
                InfiniteStrength=false, TimeLimitMinutes=2, ReportRoot="Temp/Balance/Integration",
                Balance=new BalanceExperimentOptions {ExperimentId="integration",ContentId="synthetic-validation",Cohort="pilot"} };
            options.Balance.AbilityMana.Add(new BalanceAdjustment { Name=adjusted.SkillName,Multiplier=.75f });
            AutoplayRunner.WatchDemo(options);
            var runner=AutoplayRunner.Active; runner.Report.ValidationOnly=true; runner.Report.EligibleForBalance=false;
            yield return harness.WaitUntil(()=>runner.Report.Balance.Stages.Any(s=>s.Floor>=2)||!runner.Running,"cautious opening progress");
            Assert.That(runner.Report.Balance.Stages.Any(s=>s.Floor>=2),Is.True,runner.Status);
            Assert.That(runner.Report.Balance.Stages[0].Outcome,Is.EqualTo("Cleared"));
            Assert.That(runner.Report.Balance.Stages[0].EntryParty.All(a=>a.Level==1),Is.True);
            Assert.That(runner.Report.Balance.Stages[0].GenerationFingerprint,Is.Not.Empty);
            Assert.That(adjusted.ManaCost,Is.LessThan(originalCost));
            Assert.That(runner.Report.Balance.Effects.Any(e=>e.Skill=="Heal" && e.ManaRemoved==1),Is.True,"Opening practice must record one real Mana spend, without replay duplication.");
            runner.Stop();
            yield return harness.WaitUntil(()=>!runner.Finalizing,"balance report finalization");
            Assert.That(File.Exists(Path.Combine(runner.DirectoryPath,"decisions.jsonl")),Is.True);
            Assert.That(runner.Report.ArtifactsComplete,Is.True);
            Assert.That(harness.Store.Json,Is.EqualTo(original));
            runner.ExitDemo();yield return harness.WaitUntil(()=>AutoplayRunner.Active==null,"balance restoration");
            Assert.That(adjusted.ManaCost,Is.EqualTo(originalCost));
            Assert.That(harness.Store.Json,Is.EqualTo(original));
        }
    }
}
#endif
