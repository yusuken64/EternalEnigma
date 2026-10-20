using System.Linq;

/// <summary>Uses the same costs and target fields as casting; no duplicated ability numbers.</summary>
public static class SkillPreviewText
{
    public static string Usage(Skill skill)
    {
        if (skill.ActivationType == ActivationType.Passive) return "Passive - always active once learned.";
        string arrows = !skill.UsesArrows ? "" : skill.ActionEffects?.OfType<RandomHitsAction>().Any(e => e.ArrowPerHit) == true
            ? ", 1 arrow per hit" : skill.ArrowCostMode == ArrowCostMode.PerTarget
                ? ", 1 arrow per target" : $", {skill.ArrowCost} arrow{(skill.ArrowCost == 1 ? "" : "s")}";
        string charge = skill.CastTime > 0 ? $"\nCharges for {skill.CastTime} action{(skill.CastTime == 1 ? "" : "s")}." : "";
        return $"Use: {skill.CostText()}{arrows}\nTarget: {Target(skill)}{charge}";
    }

    public static string Target(Skill skill)
    {
        if (skill.Targeting == SkillTargeting.InventoryItem) return "one eligible item in your bag.";
        if (skill.Targeting == SkillTargeting.LockedDoor) return $"a visible locked door within {skill.MissileRange} tiles.";
        if (skill.Targeting == SkillTargeting.Tile) return $"a visible floor tile within {skill.MissileRange} tiles.";
        if (skill.Targeting == SkillTargeting.Missile)
            return $"aim in one of 8 directions, up to {skill.MissileRange} tiles. Walls block the shot.";
        var selector = skill.TargetSelector;
        if (selector == null) return "see the skill description.";
        if ((skill.Targeting == SkillTargeting.Self || selector.Team == TargetTeam.Self || selector.Area == TargetArea.Self) && skill.AreaRadius == 0)
            return "yourself.";
        string team = selector.Team == TargetTeam.Enemies ? "enemies" : selector.Team == TargetTeam.Allies ? "allies" : "characters";
        string life = selector.Life == TargetLife.Downed ? "downed " : "";
        string area = selector.Area == TargetArea.Melee ? "in weapon reach" : selector.Area == TargetArea.Visible ? "in sight" : "on this floor";
        if (skill.Targeting == SkillTargeting.Self)
            return $"{life}{team} within {skill.AreaRadius} tile{(skill.AreaRadius == 1 ? "" : "s")} of you.";
        if (skill.Targeting == SkillTargeting.AllTargets) return $"all {life}{team} {area}.";
        string one = selector.Team == TargetTeam.Enemies ? "enemy" : selector.Team == TargetTeam.Allies ? "ally" : "character";
        string splash = skill.AreaRadius > 0 ? $" Also affects {team} within {skill.AreaRadius} tiles." : "";
        return $"one {life}{one} {area}.{splash}";
    }

    public static string Offer(TrainerOffer offer, bool firstLesson)
    {
        string price = offer.UsesPoints ? $"{offer.NextCost} Skill Point{(offer.NextCost == 1 ? "" : "s")}" : $"{offer.NextCost} gold";
        string decision = offer.IsMaxed ? "Maximum rank learned." : offer.CanLearn ? $"Learn for {price}." : offer.LockReason;
        string requirements = $"Rank {offer.CurrentRank} of {offer.MaxRank}";
        if (offer.HasClass && !offer.IsMaxed) requirements += $"  |  Next rank: level {offer.NextRequiredLevel}";
        string prerequisites = string.IsNullOrWhiteSpace(offer.Prerequisites) ? "" : "\n" + offer.Prerequisites;
        string practice = firstLesson ? "\n\nTomorrow, practise this skill safely at the dungeon entrance." : "";
        return $"<b>{offer.Skill.DisplayName}</b>\n{offer.Skill.Description}\n\n{Usage(offer.Skill)}\n\n<b>{decision}</b>{practice}\n\n<size=90%>{requirements}{prerequisites}</size>";
    }
}
