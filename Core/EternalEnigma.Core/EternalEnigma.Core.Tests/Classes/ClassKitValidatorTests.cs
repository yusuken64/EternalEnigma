using EternalEnigma.Core.Classes;
using Xunit;

namespace EternalEnigma.Core.Tests.Classes;

public sealed class ClassKitValidatorTests
{
    [Fact]
    public void SampleTablesAreValid()
    {
        // Warrior should be valid
        var warriorErrors = ClassKitValidator.Validate(ClassTestData.Warrior());
        Assert.Empty(warriorErrors);

        // Scout should be valid
        var scoutErrors = ClassKitValidator.Validate(ClassTestData.Scout());
        Assert.Empty(scoutErrors);

        // Guardian should be valid
        var guardianErrors = ClassKitValidator.Validate(ClassTestData.Guardian());
        Assert.Empty(guardianErrors);
    }

    [Fact]
    public void DuplicateReportedOnce()
    {
        // Create a table with duplicate skill 'X' appearing three times
        var table = new ClassSkillTable("t", new[]
        {
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
            new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
            new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
            new ClassSkillEntry("Master Training", 3, 1, SkillKind.Mastery),
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
        });

        var errors = ClassKitValidator.Validate(table);
        Assert.Single(errors);
        Assert.Equal("t: duplicate skill 'X'.", errors[0]);
    }

    [Fact]
    public void MissingMasteries()
    {
        // Create a table with only one skill (no masteries)
        var table = new ClassSkillTable("t", new[]
        {
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
        });

        var errors = ClassKitValidator.Validate(table);
        Assert.Equal(3, errors.Count);
        Assert.Equal("t: missing tier 1 mastery.", errors[0]);
        Assert.Equal("t: missing tier 2 mastery.", errors[1]);
        Assert.Equal("t: missing tier 3 mastery.", errors[2]);
    }

    [Fact]
    public void TwoMasteriesInOneTier()
    {
        // Create a table with two masteries in tier 1, one in tier 2, one in tier 3
        var table = new ClassSkillTable("t", new[]
        {
            new ClassSkillEntry("A", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("B", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("C", 2, 1, SkillKind.Mastery),
            new ClassSkillEntry("D", 3, 1, SkillKind.Mastery),
        });

        var errors = ClassKitValidator.Validate(table);
        Assert.Single(errors);
        Assert.Equal("t: more than one tier 1 mastery.", errors[0]);
    }

    [Fact]
    public void ErrorOrderDuplicatesFirst()
    {
        // Create a table with a duplicate and a missing tier 3 mastery
        var table = new ClassSkillTable("t", new[]
        {
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
            new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("X", 1, 5, SkillKind.Normal),
            new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
            // Missing tier 3 mastery
        });

        var errors = ClassKitValidator.Validate(table);
        Assert.Equal(2, errors.Count);
        Assert.Equal("t: duplicate skill 'X'.", errors[0]);
        Assert.Equal("t: missing tier 3 mastery.", errors[1]);
    }

    [Fact]
    public void KitConcatenatesPrimaryThenSecondary()
    {
        // Create primary table with missing tier 3 mastery
        var primary = new ClassSkillTable("p", new[]
        {
            new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
            new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
            // Missing tier 3 mastery
        });

        // Create secondary table with missing tier 1 mastery
        var secondary = new ClassSkillTable("s", new[]
        {
            new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
            new ClassSkillEntry("Master Training", 3, 1, SkillKind.Mastery),
            // Missing tier 1 mastery
        });

        // Create kit with invalid primary and secondary
        var kit = new ClassKit(primary, secondary);
        var errors = ClassKitValidator.Validate(kit);
        Assert.Equal(2, errors.Count);
        Assert.Equal("p: missing tier 3 mastery.", errors[0]);
        Assert.Equal("s: missing tier 1 mastery.", errors[1]);

        // Test with valid Warrior and Scout
        var validKit = new ClassKit(ClassTestData.Warrior(), ClassTestData.Scout());
        var validErrors = ClassKitValidator.Validate(validKit);
        Assert.Empty(validErrors);
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        // Validate(ClassSkillTable) with null should throw ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => ClassKitValidator.Validate((ClassSkillTable)null!));

        // Validate(ClassKit) with null should throw ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => ClassKitValidator.Validate((ClassKit)null!));
    }
}
