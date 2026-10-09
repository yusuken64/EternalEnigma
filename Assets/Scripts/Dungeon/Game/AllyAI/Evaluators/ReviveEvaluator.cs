using System.Linq;

public sealed class ReviveEvaluator : IAllySkillEvaluator
{
	public string Name => "Revive";

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null || context.Downed.Count == 0) return null;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Revive))
		{
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				var reachable = skill.ActionEffects.OfType<ReviveAction>().SelectMany(r => r.Targets(context.Ally))
					.Where(a => context.Downed.Contains(a)).Distinct();
				int count = option.Target is Ally selected && selected.IsDowned
					? (reachable.Contains(selected) ? 1 : 0) : reachable.Count();
				if (count > 0 && (best == null || count > best.Score))
					best = new AllySkillChoice(Name, option, count, $"Revive {count} downed");
			}
		}
		return best;
	}
}
