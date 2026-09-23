using EternalEnigma.Core.Classes;
using Xunit;

namespace EternalEnigma.Core.Tests.Classes;

public sealed class ClassDataTests
{
    [Fact]
    public void EntryStoresValues()
    {
        var entry = new ClassSkillEntry("Cleave", 2, 5, SkillKind.Normal);
        Assert.Equal("Cleave", entry.SkillId);
        Assert.Equal(2, entry.Tier);
        Assert.Equal(5, entry.MaxRank);
        Assert.Equal(SkillKind.Normal, entry.Kind);
    }

    [Fact]
    public void EntryRejectsBlankId()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ClassSkillEntry("", 1, 1, SkillKind.Normal));
        Assert.ThrowsAny<ArgumentException>(() => new ClassSkillEntry("  ", 1, 1, SkillKind.Normal));
        Assert.ThrowsAny<ArgumentException>(() => new ClassSkillEntry(null!, 1, 1, SkillKind.Normal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void EntryRejectsTierOutOfRange(int tier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassSkillEntry("Test", tier, 1, SkillKind.Normal));
    }

    [Fact]
    public void EntryRejectsRankOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassSkillEntry("Test", 1, 0, SkillKind.Normal));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClassSkillEntry("Test", 1, 6, SkillKind.Normal));
    }

    [Fact]
    public void NonNormalKindsMustBeSingleRank()
    {
        // Mastery with maxRank 2 should throw
        Assert.Throws<ArgumentException>(() => new ClassSkillEntry("Test", 1, 2, SkillKind.Mastery));

        // Gathering with maxRank 2 should throw
        Assert.Throws<ArgumentException>(() => new ClassSkillEntry("Test", 1, 2, SkillKind.Gathering));

        // SingleRank with maxRank 2 should throw
        Assert.Throws<ArgumentException>(() => new ClassSkillEntry("Test", 1, 2, SkillKind.SingleRank));

        // Mastery with maxRank 1 should succeed
        var masteryEntry = new ClassSkillEntry("Mastery", 1, 1, SkillKind.Mastery);
        Assert.NotNull(masteryEntry);

        // Gathering with maxRank 1 should succeed
        var gatheringEntry = new ClassSkillEntry("Gathering", 1, 1, SkillKind.Gathering);
        Assert.NotNull(gatheringEntry);

        // SingleRank with maxRank 1 should succeed
        var singleRankEntry = new ClassSkillEntry("SingleRank", 1, 1, SkillKind.SingleRank);
        Assert.NotNull(singleRankEntry);
    }

    [Fact]
    public void TableCopiesAndPreservesOrder()
    {
        var list = new List<ClassSkillEntry>
        {
            new ClassSkillEntry("First", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("Second", 2, 5, SkillKind.Normal),
            new ClassSkillEntry("Third", 3, 1, SkillKind.Mastery),
        };

        var table = new ClassSkillTable("test", list);
        list.Clear();

        Assert.Equal(3, table.Skills.Count);
        Assert.Equal("First", table.Skills[0].SkillId);
        Assert.Equal("Second", table.Skills[1].SkillId);
        Assert.Equal("Third", table.Skills[2].SkillId);
    }

    [Fact]
    public void TableRejectsBadInput()
    {
        var validSkills = new[] { new ClassSkillEntry("Test", 1, 1, SkillKind.Mastery) };

        // Blank classId
        Assert.Throws<ArgumentException>(() => new ClassSkillTable("", validSkills));
        Assert.Throws<ArgumentException>(() => new ClassSkillTable("  ", validSkills));

        // Null skills
        Assert.Throws<ArgumentNullException>(() => new ClassSkillTable("test", null!));

        // Null element in skills
        var skillsWithNull = new ClassSkillEntry?[] { null };
        Assert.Throws<ArgumentException>(() => new ClassSkillTable("test", skillsWithNull!));
    }

    [Fact]
    public void TableFindAndMastery()
    {
        var warrior = ClassTestData.Warrior();

        // Find should be ordinal
        var cleave = warrior.Find("Cleave");
        Assert.NotNull(cleave);
        Assert.Equal(2, cleave.Tier);

        // Case-sensitive lookup should return null
        Assert.Null(warrior.Find("cleave"));

        // Null lookup should return null
        Assert.Null(warrior.Find(null!));

        // MasteryForTier tests
        var novice = warrior.MasteryForTier(1);
        Assert.NotNull(novice);
        Assert.Equal("Novice Training", novice.SkillId);

        var adept = warrior.MasteryForTier(2);
        Assert.NotNull(adept);
        Assert.Equal("Adept Training", adept.SkillId);

        var master = warrior.MasteryForTier(3);
        Assert.NotNull(master);
        Assert.Equal("Master Training", master.SkillId);

        // Tier 4 should return null
        Assert.Null(warrior.MasteryForTier(4));
    }

    [Fact]
    public void KitCombination()
    {
        // Single class should not be a combination
        var singleClassKit = new ClassKit(ClassTestData.Warrior());
        Assert.False(singleClassKit.IsCombination);
        Assert.Null(singleClassKit.Secondary);

        // Two different classes should be a combination
        var combinationKit = new ClassKit(ClassTestData.Warrior(), ClassTestData.Scout());
        Assert.True(combinationKit.IsCombination);
        Assert.NotNull(combinationKit.Secondary);
    }

    [Fact]
    public void KitRejectsNullPrimaryAndSameClass()
    {
        // Null primary should throw
        Assert.Throws<ArgumentNullException>(() => new ClassKit(null!));

        // Same class for both primary and secondary should throw
        var warrior = ClassTestData.Warrior();
        Assert.Throws<ArgumentException>(() => new ClassKit(warrior, warrior));
    }

    [Fact]
    public void LearnedSkillEqualityIsOrdinal()
    {
        var skill1 = new LearnedSkill("A", 1);
        var skill2 = new LearnedSkill("A", 1);
        var skill3 = new LearnedSkill("a", 1);
        var skill4 = new LearnedSkill("A", 2);

        // Same values should be equal
        Assert.Equal(skill1, skill2);
        Assert.Equal(skill1.GetHashCode(), skill2.GetHashCode());

        // Different case should not be equal
        Assert.NotEqual(skill1, skill3);

        // Different rank should not be equal
        Assert.NotEqual(skill1, skill4);
    }

    [Fact]
    public void LearnedSkillRejectsBadInput()
    {
        // Blank id should throw
        Assert.Throws<ArgumentException>(() => new LearnedSkill("", 1));
        Assert.Throws<ArgumentException>(() => new LearnedSkill("  ", 1));

        // Rank 0 should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => new LearnedSkill("Test", 0));
    }
}
