using System;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public class BalanceHarnessTests
    {
        static AutoplayOptions Options() => new() { Godmode=false, InfiniteResources=false, InfiniteStrength=false,
            Balance = new BalanceExperimentOptions { ExperimentId="test", ContentId="test" } };

        [Test]
        public void BalanceRejectsEveryCheatAndUnboundedOrDuplicateAdjustments()
        {
            var o = Options(); Assert.That(o.Validate(out _), Is.True);
            o.DebugPlaythrough=true; Assert.That(o.Validate(out _), Is.False); o.DebugPlaythrough=false;
            o.Godmode=true; Assert.That(o.Validate(out _), Is.False); o.Godmode=false;
            o.InfiniteStrength=true; Assert.That(o.Validate(out _), Is.False); o.InfiniteStrength=false;
            o.InfiniteResources=true; Assert.That(o.Validate(out _), Is.False); o.InfiniteResources=false;
            o.DebugCoverage=true; Assert.That(o.Validate(out _), Is.False); o.DebugCoverage=false;
            o.Balance.ItemPrices.Add(new BalanceAdjustment { Name="Potion", Multiplier=.5f });
            Assert.That(o.Validate(out _), Is.False);
            o.Balance.ItemPrices[0].Multiplier=.8f; Assert.That(o.Validate(out _), Is.True);
            o.Balance.ItemPrices.Add(new BalanceAdjustment { Name="Potion" }); Assert.That(o.Validate(out _), Is.False);
        }

        [Test]
        public void CautiousScoringValuesKillsAndDoesNotRewardOverkillOrLongExpensiveCasts()
        {
            Assert.That(AutoplayRunner.CautiousDamageValue(500,10,4,0,0), Is.EqualTo(14));
            Assert.That(AutoplayRunner.CautiousDamageValue(10,10,4,0,0), Is.GreaterThan(AutoplayRunner.CautiousDamageValue(9,10,4,0,0)));
            Assert.That(AutoplayRunner.CautiousDamageValue(20,30,4,8,2), Is.LessThan(AutoplayRunner.CautiousDamageValue(10,30,4,0,0)));
        }

        [TestCase(0,.75f,0)] [TestCase(1,.75f,1)] [TestCase(10,.85f,9)] [TestCase(100,1.25f,125)]
        public void CostScalingPreservesFreeOffersAndPositiveCosts(int authored,float multiplier,int expected) =>
            Assert.That(BalanceExperimentScope.ScaleCost(authored,multiplier), Is.EqualTo(expected));

        [Test]
        public void UnknownTargetsFailBeforeChangingSharedAssets()
        {
            var o = Options().Balance;
            o.AbilityMana.Add(new BalanceAdjustment { Name="__missing_balance_skill__", Multiplier=.8f });
            Assert.Throws<ArgumentException>(() => new BalanceExperimentScope(o));
        }
    }
}
