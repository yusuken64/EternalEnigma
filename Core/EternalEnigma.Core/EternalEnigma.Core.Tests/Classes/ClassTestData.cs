namespace EternalEnigma.Core.Tests.Classes;

using EternalEnigma.Core.Classes;

internal static class ClassTestData
{
    public static LearnedSkill L(string id, int rank = 1) => new(id, rank);

    public static ClassSkillTable Warrior() => new("warrior", new[]
    {
        new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
        new ClassSkillEntry("Mining", 1, 1, SkillKind.Gathering),
        new ClassSkillEntry("Double Strike", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Vanguard", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Power Boost", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Iron Skin", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
        new ClassSkillEntry("Cleave", 2, 5, SkillKind.Normal),
        new ClassSkillEntry("Piercing Thrust", 2, 5, SkillKind.Normal),
        new ClassSkillEntry("Initiative", 2, 1, SkillKind.SingleRank),
        new ClassSkillEntry("Master Training", 3, 1, SkillKind.Mastery),
        new ClassSkillEntry("Whirlwind", 3, 5, SkillKind.Normal),
        new ClassSkillEntry("Follow-up Mastery", 3, 1, SkillKind.SingleRank),
    });

    public static ClassSkillTable Scout() => new("scout", new[]
    {
        new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
        new ClassSkillEntry("Survey", 1, 1, SkillKind.Gathering),
        new ClassSkillEntry("Trap Sense", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Disarm", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Power Boost", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
        new ClassSkillEntry("Soft Step", 2, 1, SkillKind.SingleRank),
        new ClassSkillEntry("Throwing Arm", 2, 5, SkillKind.Normal),
        new ClassSkillEntry("Master Training", 3, 1, SkillKind.Mastery),
        new ClassSkillEntry("Farsight", 3, 5, SkillKind.Normal),
    });

    public static ClassSkillTable Guardian() => new("guardian", new[]
    {
        new ClassSkillEntry("Novice Training", 1, 1, SkillKind.Mastery),
        new ClassSkillEntry("Mining", 1, 1, SkillKind.Gathering),
        new ClassSkillEntry("HP Up", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Provoke", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Shield Smite", 1, 5, SkillKind.Normal),
        new ClassSkillEntry("Adept Training", 2, 1, SkillKind.Mastery),
        new ClassSkillEntry("Cover", 2, 5, SkillKind.Normal),
        new ClassSkillEntry("Master Training", 3, 1, SkillKind.Mastery),
        new ClassSkillEntry("Bulwark", 3, 5, SkillKind.Normal),
    });
}
