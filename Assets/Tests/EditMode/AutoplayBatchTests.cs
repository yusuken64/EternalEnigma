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
            var options = JsonUtility.FromJson<AutoplayOptions>("{\"Seed\":42,\"Speed\":4}");
            Assert.That(options.RunCount,Is.EqualTo(1));
            Assert.That(options.DebugCoverage,Is.False);
            Assert.That(options.Validate(out _),Is.True);
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
