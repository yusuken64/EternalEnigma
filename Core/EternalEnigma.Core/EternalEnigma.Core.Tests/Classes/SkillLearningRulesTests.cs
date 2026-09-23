using EternalEnigma.Core.Classes;
using Xunit;
using static EternalEnigma.Core.Tests.Classes.ClassTestData;

namespace EternalEnigma.Core.Tests.Classes;

public sealed class SkillLearningRulesTests
{
    private static LearnedSkill[] Learned(params (string id, int rank)[] s)
    {
        return s.Select(x => new LearnedSkill(x.id, x.rank)).ToArray();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    public void TierUnlockLevels(int tier, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SkillLearningRules.TierUnlockLevel(tier));
    }

    [Fact]
    public void TierUnlockLevels_ZeroThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.TierUnlockLevel(0));
    }

    [Fact]
    public void TierUnlockLevels_FourThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.TierUnlockLevel(4));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 5, 13)]
    [InlineData(2, 1, 10)]
    [InlineData(3, 5, 32)]
    public void RequiredLevelCurve(int tier, int rank, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SkillLearningRules.RequiredLevel(tier, rank));
    }

    [Fact]
    public void RequiredLevelCurve_RankZeroThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.RequiredLevel(1, 0));
    }

    [Fact]
    public void RequiredLevelCurve_RankSixThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.RequiredLevel(1, 6));
    }

    [Theory]
    [InlineData(50, 1, 50)]
    [InlineData(50, 3, 150)]
    [InlineData(0, 5, 0)]
    public void RankCostCurve(int cost, int rank, int expectedCost)
    {
        Assert.Equal(expectedCost, SkillLearningRules.RankCost(cost, rank));
    }

    [Fact]
    public void RankCostCurve_NegativeCostThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.RankCost(-1, 1));
    }

    [Fact]
    public void RankCostCurve_RankZeroThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillLearningRules.RankCost(50, 0));
    }

    [Fact]
    public void RankCostCurve_OverflowThrows()
    {
        Assert.Throws<OverflowException>(() => SkillLearningRules.RankCost(int.MaxValue, 2));
    }

    [Fact]
    public void OffersSingleClassListsEveryEntry()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var offers = SkillLearningRules.Offers(kit);

        Assert.Equal(warrior.Skills.Count, offers.Count);

        for (int i = 0; i < offers.Count; i++)
        {
            Assert.Equal(warrior.Skills[i].SkillId, offers[i].SkillId);
            Assert.Equal(ClassSource.Primary, offers[i].Source);
        }
    }

    [Fact]
    public void OffersCombinationAddsSecondaryTierOneAndTwoOnly()
    {
        var warrior = Warrior();
        var scout = Scout();
        var kit = new ClassKit(warrior, scout);
        var offers = SkillLearningRules.Offers(kit);

        // Primary comes first
        var primaryOffers = offers.Where(o => o.Source == ClassSource.Primary).ToList();
        var secondaryOffers = offers.Where(o => o.Source == ClassSource.Secondary).ToList();

        // Secondary offers should be: Survey, Trap Sense, Disarm, Soft Step, Throwing Arm
        var expectedSecondaryIds = new[] { "Survey", "Trap Sense", "Disarm", "Soft Step", "Throwing Arm" };
        var actualSecondaryIds = secondaryOffers.Select(o => o.SkillId).ToList();

        Assert.Equal(expectedSecondaryIds, actualSecondaryIds);

        // Check specific properties
        var trapSenseOffer = secondaryOffers.First(o => o.SkillId == "Trap Sense");
        Assert.Equal(3, trapSenseOffer.MaxRank);

        var powerBoostOffer = primaryOffers.First(o => o.SkillId == "Power Boost");
        Assert.Equal(ClassSource.Primary, powerBoostOffer.Source);
        Assert.Equal(5, powerBoostOffer.MaxRank);
    }

    [Fact]
    public void FindOfferAndCurrentRank()
    {
        var warrior = Warrior();
        var scout = Scout();
        var kit = new ClassKit(warrior, scout);

        // FindOffer("Farsight") should be null (Farsight is tier 3 in Scout, not offered)
        var farsightOffer = SkillLearningRules.FindOffer(kit, "Farsight");
        Assert.Null(farsightOffer);

        // FindOffer(null) should be null
        var nullOffer = SkillLearningRules.FindOffer(kit, null!);
        Assert.Null(nullOffer);

        // CurrentRank with ("Cleave",2),("Cleave",4) should be 4
        var learned = Learned(("Cleave", 2), ("Cleave", 4));
        Assert.Equal(4, SkillLearningRules.CurrentRank(learned, "Cleave"));

        // Absent id should be 0
        Assert.Equal(0, SkillLearningRules.CurrentRank(learned, "Unknown"));
    }

    [Fact]
    public void StartingSkillsIsTierOneMastery()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var startingSkills = SkillLearningRules.StartingSkills(kit);

        Assert.Single(startingSkills);
        Assert.Equal("Novice Training", startingSkills[0]);
    }

    [Fact]
    public void TierOneSkillLearnableAtLevelOne()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Array.Empty<LearnedSkill>();

        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 1, "Double Strike", learnCost: 50);

        Assert.True(check.Allowed);
        Assert.Equal(LearnRefusal.None, check.Refusal);
        Assert.Equal(1, check.NextRank);
        Assert.Equal(50, check.Cost);
        Assert.Equal(1, check.RequiredLevel);
        Assert.Equal("", check.Reason);
    }

    [Fact]
    public void InvalidCostWinsFirst()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Array.Empty<LearnedSkill>();

        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 1, "UnknownSkill", learnCost: -1);

        Assert.Equal(LearnRefusal.InvalidCost, check.Refusal);
        Assert.Equal(0, check.Cost);
        Assert.Equal(0, check.RequiredLevel);
    }

    [Fact]
    public void NotInKit()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Array.Empty<LearnedSkill>();

        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 1, "Farsight", learnCost: 50);

        Assert.Equal(LearnRefusal.NotInKit, check.Refusal);
        Assert.Equal(0, check.Cost);
        Assert.Equal(0, check.RequiredLevel);
    }

    [Fact]
    public void MaxRankReached_FullyRanked()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Learned(("Double Strike", 5));

        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 1, "Double Strike", learnCost: 50);

        Assert.Equal(LearnRefusal.MaxRankReached, check.Refusal);
        Assert.Equal(0, check.Cost);
        Assert.Equal(0, check.RequiredLevel);
    }

    [Fact]
    public void MaxRankReached_Mastery()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Learned(("Novice Training", 1));

        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 1, "Novice Training", learnCost: 50);

        Assert.Equal(LearnRefusal.MaxRankReached, check.Refusal);
    }

    [Fact]
    public void RankGateByLevel()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Learned(("Double Strike", 1));

        // Level 3: Should fail
        var checkLevel3 = SkillLearningRules.CheckNextRank(kit, learned, level: 3, "Double Strike", learnCost: 50);
        Assert.Equal(LearnRefusal.LevelTooLow, checkLevel3.Refusal);
        Assert.Equal("Requires level 4.", checkLevel3.Reason);
        Assert.Equal(2, checkLevel3.NextRank);
        Assert.Equal(100, checkLevel3.Cost);

        // Level 4: Should succeed
        var checkLevel4 = SkillLearningRules.CheckNextRank(kit, learned, level: 4, "Double Strike", learnCost: 50);
        Assert.True(checkLevel4.Allowed);
    }

    [Fact]
    public void AdeptTrainingNeedsNoviceAndThreeTierOneSkills()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);

        // Case 1: Only 2 non-mastery tier-1 skills (Double Strike, Vanguard) - not enough
        var learned1 = Learned(("Novice Training", 1), ("Double Strike", 1), ("Vanguard", 1));
        var check1 = SkillLearningRules.CheckNextRank(kit, learned1, level: 10, "Adept Training", learnCost: 100);
        Assert.Equal(LearnRefusal.NotEnoughLowerTierSkills, check1.Refusal);
        Assert.Equal("Requires 3 tier 1 skills.", check1.Reason);

        // Case 2: 3 tier-1 skills including Mining (Gathering counts) - should be allowed
        var learned2 = Learned(("Novice Training", 1), ("Double Strike", 1), ("Vanguard", 1), ("Mining", 1));
        var check2 = SkillLearningRules.CheckNextRank(kit, learned2, level: 10, "Adept Training", learnCost: 100);
        Assert.True(check2.Allowed);
        Assert.Equal(100, check2.Cost);
        Assert.Equal(10, check2.RequiredLevel);

        // Case 3: No Novice Training - should fail with PreviousMasteryMissing
        var learned3 = Learned(("Double Strike", 1), ("Vanguard", 1), ("Power Boost", 1));
        var check3 = SkillLearningRules.CheckNextRank(kit, learned3, level: 10, "Adept Training", learnCost: 100);
        Assert.Equal(LearnRefusal.PreviousMasteryMissing, check3.Refusal);
        Assert.Equal("Requires Novice Training.", check3.Reason);
    }

    [Fact]
    public void MasteryCountIgnoresMasteriesAndForeignSkills()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);

        // Novice Training (mastery - ignored for count), Double Strike, Vanguard, Unknown Skill, Unknown 2
        // Only 2 skills count (Double Strike, Vanguard), so should fail
        var learned = Learned(("Novice Training", 1), ("Double Strike", 1), ("Vanguard", 1), ("Unknown Skill", 1), ("Unknown 2", 1));
        var check = SkillLearningRules.CheckNextRank(kit, learned, level: 10, "Adept Training", learnCost: 100);

        Assert.Equal(LearnRefusal.NotEnoughLowerTierSkills, check.Refusal);
    }

    [Fact]
    public void AdeptTrainingLevelGate()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learned = Learned(("Novice Training", 1), ("Double Strike", 1), ("Vanguard", 1), ("Mining", 1));

        // At level 9: Should fail with LevelTooLow
        var checkLevel9 = SkillLearningRules.CheckNextRank(kit, learned, level: 9, "Adept Training", learnCost: 100);
        Assert.Equal(LearnRefusal.LevelTooLow, checkLevel9.Refusal);
        Assert.Equal("Requires level 10.", checkLevel9.Reason);
    }

    [Fact]
    public void TierTwoSkillLockedUntilAdept()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);

        // Without Adept Training: Should fail with TierLocked
        var checkNoAdept = SkillLearningRules.CheckNextRank(kit, Array.Empty<LearnedSkill>(), level: 30, "Cleave", learnCost: 50);
        Assert.Equal(LearnRefusal.TierLocked, checkNoAdept.Refusal);
        Assert.Equal("Requires Adept Training.", checkNoAdept.Reason);

        // With Adept Training: Should be allowed
        var learnedWithAdept = Learned(("Adept Training", 1));
        var checkWithAdept = SkillLearningRules.CheckNextRank(kit, learnedWithAdept, level: 30, "Cleave", learnCost: 50);
        Assert.True(checkWithAdept.Allowed);
        Assert.Equal(10, checkWithAdept.RequiredLevel);
    }

    [Fact]
    public void TierThreeSkillLockedUntilMaster()
    {
        var warrior = Warrior();
        var kit = new ClassKit(warrior);
        var learnedWithAdept = Learned(("Adept Training", 1));

        // With Adept only: Should fail with TierLocked (needs Master)
        var check = SkillLearningRules.CheckNextRank(kit, learnedWithAdept, level: 30, "Whirlwind", learnCost: 50);
        Assert.Equal(LearnRefusal.TierLocked, check.Refusal);
        Assert.Equal("Requires Master Training.", check.Reason);
    }

    [Fact]
    public void SecondarySkillUsesPrimaryMasteryAndCap()
    {
        var warrior = Warrior();
        var scout = Scout();
        var kit = new ClassKit(warrior, scout);

        // "Throwing Arm" is a secondary skill (tier 2) from Scout
        // Without Adept: Should fail with TierLocked
        var checkNoAdept = SkillLearningRules.CheckNextRank(kit, Array.Empty<LearnedSkill>(), level: 20, "Throwing Arm", learnCost: 50);
        Assert.Equal(LearnRefusal.TierLocked, checkNoAdept.Refusal);
        Assert.Equal("Requires Adept Training.", checkNoAdept.Reason);

        // With Adept and already rank 3: Should be MaxRankReached (secondary max is 3)
        var learnedWithAdeptAndRank3 = Learned(("Adept Training", 1), ("Throwing Arm", 3));
        var checkMaxReached = SkillLearningRules.CheckNextRank(kit, learnedWithAdeptAndRank3, level: 20, "Throwing Arm", learnCost: 50);
        Assert.Equal(LearnRefusal.MaxRankReached, checkMaxReached.Refusal);

        // "Trap Sense" from Scout is tier 1, secondary - should be learnable at level 1
        var checkTrapSense = SkillLearningRules.CheckNextRank(kit, Array.Empty<LearnedSkill>(), level: 1, "Trap Sense", learnCost: 50);
        Assert.True(checkTrapSense.Allowed);
    }

    [Fact]
    public void ArgumentsAreValidated()
    {
        var warrior = Warrior();
        var learned = Array.Empty<LearnedSkill>();

        // null kit for CheckNextRank
        Assert.Throws<ArgumentNullException>(() =>
            SkillLearningRules.CheckNextRank(null!, learned, 1, "Double Strike", 50));

        // null learned for CheckNextRank
        Assert.Throws<ArgumentNullException>(() =>
            SkillLearningRules.CheckNextRank(new ClassKit(warrior), null!, 1, "Double Strike", 50));

        // null kit for Offers
        Assert.Throws<ArgumentNullException>(() =>
            SkillLearningRules.Offers(null!));

        // null learned for CurrentRank
        Assert.Throws<ArgumentNullException>(() =>
            SkillLearningRules.CurrentRank(null!, "Double Strike"));
    }
}
