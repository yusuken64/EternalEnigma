using System;
using System.Linq;

public sealed class BuffUpkeepEvaluator : IAllySkillEvaluator
{
    public string Name => "BuffUpkeep";
    private const int RefreshAtTurns = 1;

    public AllySkillChoice Evaluate(AllySkillContext context)
    {
        if (context == null) return null;
        AllySkillChoice best = null;
        foreach (var skill in context.WithIntent(SkillIntent.Buff))
            foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
            {
                float score = Benefit(context, skill, option);
                if (score > 0 && (best == null || score > best.Score))
                    best = new AllySkillChoice(Name, option, score, $"Upkeep {skill.SkillName}");
            }
        return best;
    }

    private static float Benefit(AllySkillContext context, Skill skill, SkillCastOption option)
    {
        float score = 0;
        foreach (var effect in skill.ActionEffects)
        {
            if (effect is RestoreSPAction restore)
            {
                int amount = skill.RankContext.Scaling.ScalePower(restore.Amount, skill.RankContext.Rank);
                float restored = option.Affected.Where(c => context.Party.Contains(c) && !(restore.ExcludeCaster && c == context.Ally))
                    .Sum(c => Math.Max(0, Math.Min(amount, c.FinalStats.SPMax - c.Vitals.SP + (c == context.Ally ? skill.SPCost : 0))));
                // Do not spend SP merely to buy back the same (or a smaller) amount.
                score += Math.Max(0, restored - skill.SPCost);
                continue;
            }
            if (context.VisibleEnemies.Count == 0) continue;
            if (effect is StartSongAction song && song.SongId != null)
            {
                if (!SongRules.ActiveSongs(context.Ally).Any(s => s.SongId == song.SongId && s.TurnsLeft > RefreshAtTurns))
                    score += context.Party.Count;
            }
            else if (effect is ApplyCommandAction command)
            {
                string id = string.IsNullOrEmpty(command.CommandId) ? command.CommandName : command.CommandId;
                // Share execution recipients, including personally visible summons.
                score += ApplyCommandAction.Recipients(context.Game, context.Ally).Count(p => !p.StatusEffects.OfType<CommandStatusEffect>()
                    .Any(s => !s.IsExpired() && s.CommandId == id && s.TurnsLeft > RefreshAtTurns));
            }
            else if (effect is SummonCloneAction)
                score += SummonRules.ClonesOf(context.Game, context.Ally).Count < SummonRules.CloneLimit(context.Ally) ? 1 : 0;
            else if (effect is ApplyStatusEffectAction apply && apply.StatusEffect != null)
                score += option.Affected.Where(c => context.Party.Contains(c)).Sum(c => StatusBenefit(context, c, apply.StatusEffect));
            else if (effect is ApplyStatusChanceAction chance && chance.StatusEffect != null)
            {
                var recipients = chance.OnCaster ? new Character[] { context.Ally } : option.Affected;
                score += chance.Probability(context.Ally, skill.RankContext) *
                    recipients.Where(c => context.Party.Contains(c)).Sum(c => StatusBenefit(context, c, chance.StatusEffect));
            }
        }
        return score;
    }

    private static float StatusBenefit(AllySkillContext context, Character target, StatusEffect status)
    {
        if (!StatusCategories.IsBuff(status) || ClassPassives.IsImmune(target, status)) return 0;
        var existing = target.StatusEffects.FirstOrDefault(s => s != null && !s.IsExpired() && s.StackKey == status.StackKey);
        if (existing != null && (existing.TurnsLeft > RefreshAtTurns ||
            existing is TimedBuffStatusEffect current && status is TimedBuffStatusEffect incoming && current.Magnitude() > incoming.Magnitude())) return 0;
        if (status is HotStatusEffect && target.Vitals.HP >= target.FinalStats.HPMax * .7f) return 0;
        if (status is BarrierStatusEffect && !context.VisibleEnemies.Any(e => TileWorldDungeon.ChevDistance(e.TilemapPosition, target.TilemapPosition) <= 2)) return 0;
        return 1;
    }
}
