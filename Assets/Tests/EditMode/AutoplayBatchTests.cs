using System;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests
{
    public class AutoplayBatchTests
    {
        [Test]
        public void LegacyOptionsStaySingleRunAndCoverageIsExplicit()
        {
            var options = AutoplayOptions.FromJson("{\"Seed\":42,\"Speed\":4}");
            Assert.That(options.RunCount,Is.EqualTo(1));
            Assert.That(options.DebugCoverage,Is.False);
            Assert.That(options.Validate(out _),Is.True);
        }

        [TestCase(AutoplayMode.Campaign, false)]
        [TestCase(AutoplayMode.Campaign, true)]
        [TestCase(AutoplayMode.Finale, true)]
        [TestCase(AutoplayMode.FloorLootSweep, true)]
        public void EditorLaunchRoundTripDoesNotCreateABalanceExperiment(AutoplayMode mode, bool debug)
        {
            var original = new AutoplayOptions { Mode = mode, DebugPlaythrough = debug, Seed = 73421, RunCount = 2 };
            var options = AutoplayOptions.FromJson(JsonUtility.ToJson(original));
            Assert.That(options.Balance, Is.Null);
            Assert.That(options.Validate(out var reason), Is.True, reason);
            Assert.That(options.Mode, Is.EqualTo(mode));
            Assert.That(options.DebugPlaythrough, Is.EqualTo(debug));
            Assert.That(options.Seed, Is.EqualTo(73421));
            Assert.That(options.RunCount, Is.EqualTo(2));
        }

        [Test]
        public void JsonOptionsPreserveFinalePresetAndConfiguredBalanceExperiments()
        {
            var finale = AutoplayOptions.FromJson("{\"Seed\":73}", AutoplayOptions.FinaleDebug());
            Assert.That(finale.Mode, Is.EqualTo(AutoplayMode.Finale));
            Assert.That(finale.InfiniteStrength, Is.False);
            Assert.That(finale.Seed, Is.EqualTo(73));
            Assert.That(finale.Validate(out var reason), Is.True, reason);
            var original = new AutoplayOptions { Godmode = false, InfiniteStrength = false, InfiniteResources = false,
                Balance = new BalanceExperimentOptions { ExperimentId = "experiment", ContentId = "content" } };
            original.Balance.ItemPrices.Add(new BalanceAdjustment { Name = "Potion", Multiplier = .8f });
            var options = AutoplayOptions.FromJson(JsonUtility.ToJson(original));
            Assert.That(options.Validate(out reason), Is.True, reason);
            Assert.That(options.Balance.ExperimentId, Is.EqualTo("experiment"));
            Assert.That(options.Balance.ItemPrices[0].Multiplier, Is.EqualTo(.8f));
        }

        [TestCase("{\"Balance\":{\"ContentId\":\"content\"}}")]
        [TestCase("{\"Balance\":{\"CandidateId\":\"candidate\"}}")]
        [TestCase("{\"Balance\":{\"ItemPrices\":[{\"Name\":\"Potion\",\"Multiplier\":0.8}]}}")]
        public void IncompleteBalanceConfigurationIsNotSilentlyDiscarded(string json)
        {
            var options = AutoplayOptions.FromJson(json);
            Assert.That(options.Balance, Is.Not.Null);
            Assert.That(options.Validate(out _), Is.False);
        }

        [TestCase(42,3,true)]
        [TestCase(int.MaxValue,1,true)]
        [TestCase(int.MaxValue,2,false)]
        [TestCase(int.MaxValue-1,2,true)]
        [TestCase(int.MinValue,3,true)]
        [TestCase(42,0,false)]
        [TestCase(42,-1,false)]
        public void SeedRangeIsValidatedWithoutOverflow(int seed,int count,bool valid)
        {
            var options=new AutoplayOptions {Seed=seed,RunCount=count};
            Assert.That(options.Validate(out var reason),Is.EqualTo(valid),reason);
            if (!valid) return;
            Assert.That(options.SeedForRun(count-1),Is.EqualTo((int)((long)seed+count-1)));
            Assert.Throws<ArgumentOutOfRangeException>(()=>options.SeedForRun(count));
        }

        [TestCase("Victory",true)] [TestCase("Defeat",true)] [TestCase("Stalled",true)] [TestCase("TimeLimit",true)]
        [TestCase("Unsupported",false)] [TestCase("GameError",false)] [TestCase("AutomationError",false)]
        [TestCase("Stopped",false)] [TestCase("PlayerControl",false)] [TestCase("Running",false)]
        public void OnlyCampaignOutcomesAdvance(string outcome,bool expected) =>
            Assert.That(AutoplayRunner.ContinuesAfter(outcome),Is.EqualTo(expected));
    }
}
