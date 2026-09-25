namespace EternalEnigma.Core.Classes;

public static class SkillLearningRules
{
    public const int MaxRank = 5;
    public const int SecondaryMaxRank = 3;
    public const int SecondaryMaxTier = 2;
    public const int SkillsRequiredPerTier = 3;

    public static int TierUnlockLevel(int tier)
    {
        return tier switch
        {
            1 => 1,
            2 => 10,
            3 => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };
    }

    public static int RequiredLevel(int tier, int rank)
    {
        if (rank < 1 || rank > MaxRank)
            throw new ArgumentOutOfRangeException(nameof(rank));

        return TierUnlockLevel(tier) + 3 * (rank - 1);
    }

    public static int RankCost(int learnCost, int rank)
    {
        if (learnCost < 0)
            throw new ArgumentOutOfRangeException(nameof(learnCost));

        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank));

        return checked(learnCost * rank);
    }

    public static IReadOnlyList<SkillOffer> Offers(ClassKit kit)
    {
        if (kit == null)
            throw new ArgumentNullException(nameof(kit));

        var list = new List<SkillOffer>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        // Process Primary skills
        foreach (var e in kit.Primary.Skills)
        {
            if (!ids.Contains(e.SkillId))
            {
                list.Add(new SkillOffer(e.SkillId, e.Tier, e.MaxRank, e.Kind, ClassSource.Primary));
                ids.Add(e.SkillId);
            }
        }

        // Process Secondary skills if present
        if (kit.Secondary != null)
        {
            foreach (var e in kit.Secondary.Skills)
            {
                if (e.Tier > SecondaryMaxTier)
                    continue;

                if (e.Kind == SkillKind.Mastery)
                    continue;

                if (ids.Contains(e.SkillId))
                    continue;

                list.Add(new SkillOffer(e.SkillId, e.Tier, Math.Min(e.MaxRank, SecondaryMaxRank), e.Kind, ClassSource.Secondary));
                ids.Add(e.SkillId);
            }
        }

        return list.AsReadOnly();
    }

    public static SkillOffer? FindOffer(ClassKit kit, string skillId)
    {
        if (kit == null)
            throw new ArgumentNullException(nameof(kit));

        if (skillId == null)
            return null;

        var offers = Offers(kit);
        foreach (var offer in offers)
        {
            if (string.Equals(offer.SkillId, skillId, StringComparison.Ordinal))
                return offer;
        }

        return null;
    }

    public static int CurrentRank(IEnumerable<LearnedSkill> learned, string skillId)
    {
        if (learned == null)
            throw new ArgumentNullException(nameof(learned));

        int maxRank = 0;
        foreach (var skill in learned)
        {
            if (string.Equals(skill.SkillId, skillId, StringComparison.Ordinal))
            {
                if (skill.Rank > maxRank)
                    maxRank = skill.Rank;
            }
        }

        return maxRank;
    }

    public static LearnCheck CheckNextRank(ClassKit kit, IEnumerable<LearnedSkill> learned, int level, string skillId, int learnCost)
    {
        if (kit == null)
            throw new ArgumentNullException(nameof(kit));

        if (learned == null)
            throw new ArgumentNullException(nameof(learned));

        // Materialize learned once
        var learnedArray = learned.ToArray();

        // Calculate current and next rank
        var current = CurrentRank(learnedArray, skillId);
        var next = current + 1;

        // Step 1: Check learn cost
        if (learnCost < 0)
            return new LearnCheck(LearnRefusal.InvalidCost, "This skill cannot be learned.", next, 0, 0);

        // Step 2: Find offer
        var offer = FindOffer(kit, skillId);
        if (offer == null)
            return new LearnCheck(LearnRefusal.NotInKit, "This hero's class can't learn this skill.", next, 0, 0);

        // Step 3: Check max rank
        if (current >= offer.MaxRank)
            return new LearnCheck(LearnRefusal.MaxRankReached, "Already at max rank.", next, 0, 0);

        // Compute cost and required level for remaining checks
        var cost = RankCost(learnCost, next);
        var required = RequiredLevel(offer.Tier, next);

        // Step 4: If mastery and tier > 1, check prerequisites
        if (offer.Kind == SkillKind.Mastery && offer.Tier > 1)
        {
            // Check previous mastery
            var previous = kit.Primary.MasteryForTier(offer.Tier - 1);
            if (previous != null && CurrentRank(learnedArray, previous.SkillId) == 0)
                return new LearnCheck(LearnRefusal.PreviousMasteryMissing, $"Requires {previous.SkillId}.", next, cost, required);

            // Count lower tier skills
            var lowerTierSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var learned_skill in learnedArray)
            {
                if (learned_skill.Rank >= 1)
                {
                    var skill_offer = FindOffer(kit, learned_skill.SkillId);
                    if (skill_offer != null && skill_offer.Tier == offer.Tier - 1 && skill_offer.Kind != SkillKind.Mastery)
                    {
                        lowerTierSkills.Add(learned_skill.SkillId);
                    }
                }
            }

            if (lowerTierSkills.Count < SkillsRequiredPerTier)
                return new LearnCheck(LearnRefusal.NotEnoughLowerTierSkills,
                    $"Requires {SkillsRequiredPerTier} tier {offer.Tier - 1} skills.", next, cost, required);
        }

        // Step 5: If not mastery and tier > 1, check tier mastery gate
        if (offer.Kind != SkillKind.Mastery && offer.Tier > 1)
        {
            var mastery = kit.Primary.MasteryForTier(offer.Tier);
            if (mastery != null && CurrentRank(learnedArray, mastery.SkillId) == 0)
                return new LearnCheck(LearnRefusal.TierLocked, $"Requires {mastery.SkillId}.", next, cost, required);
        }

        // Step 6: Check level requirement
        if (level < required)
            return new LearnCheck(LearnRefusal.LevelTooLow, $"Requires level {required}.", next, cost, required);

        // Step 7: Success
        return new LearnCheck(LearnRefusal.None, "", next, cost, required);
    }

    public static IReadOnlyList<string> StartingSkills(ClassKit kit)
    {
        if (kit == null)
            throw new ArgumentNullException(nameof(kit));

        var mastery = kit.Primary.MasteryForTier(1);
        if (mastery != null)
            return Array.AsReadOnly(new[] { mastery.SkillId });

        return Array.AsReadOnly(Array.Empty<string>());
    }
}
