using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CrowdControlEvaluator : IAllySkillEvaluator
{
    public string Name => "CrowdControl";

    public AllySkillChoice Evaluate(AllySkillContext context)
    {
        if (context == null || context.VisibleEnemies.Count == 0) return null;
        AllySkillChoice best = null;
        foreach (var skill in context.Castable.Where(context.CanAfford))
        {
            var intent = SkillIntents.Classify(skill);
            if (!intent.Has(SkillIntent.CrowdControl) && !intent.Has(SkillIntent.Debuff)) continue;
            foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
            {
                if (SkillIntents.TargetsEnemies(skill) && option.Affected.Any(c => !context.VisibleEnemies.Contains(c))) continue;
                float score = Value(context, skill, option);
                if (score > 0 && (best == null || score > best.Score))
                    best = new AllySkillChoice(Name, option, score, skill.SkillName);
            }
        }
        return best;
    }

    internal static float Danger(Character e) =>
        AllySkillBudget.DangerScore(e.FinalStats.Strength, e.Vitals.HP, e.FinalStats.HPMax, EnemyRank.IsBoss(e));

    private static float Value(AllySkillContext context, Skill skill, SkillCastOption option)
    {
        float value = 0;
        var spread = new HashSet<(Character, string)>();
        foreach (var enemy in option.Affected.Where(context.VisibleEnemies.Contains))
            foreach (var effect in skill.ActionEffects)
            {
                if (effect is DominateAction)
                    value += DominateAction.CanDominate(enemy) ? Danger(enemy) * 1.5f : 0;
                else if (effect is ApplyStatusEffectAction apply)
                    value += StatusValue(enemy, apply.StatusEffect);
                else if (effect is ApplyStatusChanceAction chance && !chance.OnCaster)
                    value += chance.Probability(context.Ally, skill.RankContext) * StatusValue(enemy, chance.StatusEffect);
                else if (effect is CleanseAction cleanse && cleanse.Buffs)
                    value += enemy.StatusEffects.Count(s => s != null && !s.IsExpired() && StatusCategories.IsBuff(s)) * Danger(enemy) * .5f;
                else if (effect is SpreadAilmentsAction plague)
                {
                    foreach (var ailment in enemy.StatusEffects.Where(s => s != null && !s.IsExpired() && StatusCategories.IsAilment(s)))
                    {
                        var prefab = context.Game.StatusEffectPrefabs.FirstOrDefault(p => p != null && p.StackKey == ailment.StackKey);
                        if (prefab == null) continue;
                        foreach (var neighbor in context.VisibleEnemies.Where(c => c != enemy && c.Team == enemy.Team &&
                            TileWorldDungeon.ChevDistance(c.TilemapPosition, enemy.TilemapPosition) <= plague.Radius))
                            if (spread.Add((neighbor, prefab.StackKey))) value += StatusValue(neighbor, prefab);
                    }
                }
            }
        return value;
    }

    private static float StatusValue(Character enemy, StatusEffect status)
    {
        if (status == null || ClassPassives.IsImmune(enemy, status) ||
            enemy.StatusEffects.Any(s => s != null && !s.IsExpired() && s.StackKey == status.StackKey)) return 0;
        if (SkillIntents.DisablingStatuses.Contains(status.GetEffectName())) return Danger(enemy);
        if (SkillIntents.DebuffStatuses.Contains(status.GetEffectName()) || status is FollowUpMarkStatusEffect)
            return enemy.Vitals.HP >= enemy.FinalStats.HPMax * .5f ? Danger(enemy) * .5f : 0;
        return 0;
    }
}
