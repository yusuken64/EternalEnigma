using System.Linq;

public sealed class CureEvaluator : IAllySkillEvaluator
{
	public string Name => "Cure";

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null) return null;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Cure))
		{
			var cures = skill.ActionEffects.OfType<ICureEffect>().ToList();
			if (cures.Count == 0) continue;
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				int afflicted = option.Affected.Count(c => c != null && c.Team == context.Ally.Team &&
					c.StatusEffects.Any(s => s != null && !s.IsExpired() && cures.Any(cure => cure.Cures(s))));
				if (afflicted == 0) continue;
				if (best == null || afflicted > best.Score)
					best = new AllySkillChoice(Name, option, afflicted, $"Cure {afflicted}");
			}
		}
		return best;
	}
}
