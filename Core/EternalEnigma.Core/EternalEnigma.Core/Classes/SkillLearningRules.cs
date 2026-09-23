namespace EternalEnigma.Core.Classes;

public static class SkillLearningRules
{
    public const int MaxRank = 5;
    public const int SecondaryMaxRank = 3;
    public const int SecondaryMaxTier = 2;
    public const int SkillsRequiredPerTier = 3;

    public static int TierUnlockLevel(int tier)
    {
        throw new NotImplementedException();
    }

    public static int RequiredLevel(int tier, int rank)
    {
        throw new NotImplementedException();
    }

    public static int RankCost(int learnCost, int rank)
    {
        throw new NotImplementedException();
    }

    public static IReadOnlyList<SkillOffer> Offers(ClassKit kit)
    {
        throw new NotImplementedException();
    }

    public static SkillOffer? FindOffer(ClassKit kit, string skillId)
    {
        throw new NotImplementedException();
    }

    public static int CurrentRank(IEnumerable<LearnedSkill> learned, string skillId)
    {
        throw new NotImplementedException();
    }

    public static LearnCheck CheckNextRank(ClassKit kit, IEnumerable<LearnedSkill> learned, int level, string skillId, int learnCost)
    {
        throw new NotImplementedException();
    }

    public static IReadOnlyList<string> StartingSkills(ClassKit kit)
    {
        throw new NotImplementedException();
    }
}
