using System.Linq;
public sealed class CastingSupportEvaluator : IAllySkillEvaluator
{
    public string Name => "Casting support";
    public AllySkillChoice Evaluate(AllySkillContext context)
    {
        if (context == null) return null;
        var best = context.WithIntent(SkillIntent.CastingSupport).SelectMany(s => SkillCastOptions.Enumerate(context.Ally, s))
            .Select(o => new { Option = o, Saved = o.Affected.Count(c => c.PendingCast?.Remaining > 0) })
            .Where(x => x.Saved > 0).OrderByDescending(x => x.Saved).ThenBy(x => x.Option.Skill.SPCost).FirstOrDefault();
        return best == null ? null : new AllySkillChoice(Name, best.Option, best.Saved, "Reduce charging actions");
    }
}
