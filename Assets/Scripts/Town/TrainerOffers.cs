using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;

// One row in the trainer: a skill, its rank state and whether the hero may buy the next rank now (gold aside).
public sealed class TrainerOffer
{
    public Skill Skill;
    public int Tier = 1;
    public int CurrentRank;          // 0 = not learned
    public int MaxRank = 1;
    public int NextCost;             // gold for the next rank
    public bool CanLearn;            // rules allow the next rank (gold is checked separately)
    public string LockReason = "";   // player-readable; "" when CanLearn or IsMaxed
    public bool HasClass;            // false = classless fallback
    public ClassSource Source = ClassSource.Primary;
    public bool IsMaxed => CurrentRank >= MaxRank;
    public string Label => !HasClass ? Skill.SkillName
        : $"{Skill.SkillName} {CurrentRank}/{MaxRank}" + (Tier > 1 ? $" (T{Tier})" : "");
}

public static class TrainerOffers
{
    public static List<TrainerOffer> Build(TownAlly ally, TownConfiguration configuration)
    {
        if (ally == null || configuration == null) return new List<TrainerOffer>();
        return ally.PrimaryClass == null ? BuildFallback(ally, configuration) : BuildForClass(ally, configuration);
    }

    public static TrainerOffer Find(TownAlly ally, TownConfiguration configuration, Skill skill) =>
        skill == null ? null : Build(ally, configuration).FirstOrDefault(o => o.Skill.SkillName == skill.SkillName);

    // Old behaviour: the town list, one rank each.
    private static List<TrainerOffer> BuildFallback(TownAlly ally, TownConfiguration configuration)
    {
        var result = new List<TrainerOffer>();
        foreach (var skill in configuration.LearnableSkills.Where(s => s != null))
        {
            int rank = ally.GetRank(skill.SkillName) > 0 ? 1 : 0;
            bool validCost = skill.LearnCost >= 0;
            result.Add(new TrainerOffer
            {
                Skill = skill, Tier = 1, CurrentRank = rank, MaxRank = 1, NextCost = skill.LearnCost,
                CanLearn = rank == 0 && validCost,
                LockReason = rank == 0 && !validCost ? "This skill cannot be learned." : "",
                HasClass = false,
            });
        }
        return result;
    }

    private static List<TrainerOffer> BuildForClass(TownAlly ally, TownConfiguration configuration)
    {
        var kit = HeroClass.ToKit(ally.PrimaryClass, ally.SecondaryClass);
        var learned = ally.ToLearnedSkills();
        var allow = configuration.LearnableSkills.Where(s => s != null).Select(s => s.SkillName).ToHashSet();
        var rows = new List<(int order, TrainerOffer offer)>();
        int order = 0;
        foreach (var offer in SkillLearningRules.Offers(kit))
        {
            var skill = Resolve(offer.Source == ClassSource.Primary ? ally.PrimaryClass : ally.SecondaryClass, offer.SkillId);
            if (skill == null) continue;
            if (allow.Count > 0 && !allow.Contains(skill.SkillName)) continue;
            var check = SkillLearningRules.CheckNextRank(kit, learned, ally.HighestLevel, offer.SkillId, skill.LearnCost);
            int current = ally.GetRank(offer.SkillId);
            rows.Add((order++, new TrainerOffer
            {
                Skill = skill, Tier = offer.Tier, CurrentRank = current, MaxRank = offer.MaxRank,
                NextCost = check.Cost, CanLearn = check.Allowed,
                LockReason = check.Allowed || current >= offer.MaxRank ? "" : check.Reason,
                HasClass = true, Source = offer.Source,
            }));
        }
        // Grouped by tier; author order inside a tier.
        return rows.OrderBy(r => r.offer.Tier).ThenBy(r => r.order).Select(r => r.offer).ToList();
    }

    private static Skill Resolve(ClassDefinition definition, string skillId) =>
        definition?.Skills?.FirstOrDefault(e => e != null && e.Skill != null && e.Skill.SkillName == skillId)?.Skill;
}
