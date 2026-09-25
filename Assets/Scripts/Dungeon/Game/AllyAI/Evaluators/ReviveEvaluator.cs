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
			var option = SkillCastOptions.Enumerate(context.Ally, skill).FirstOrDefault();
			if (option == null) continue;
			bool visibleScope = skill.ActionEffects.OfType<ReviveAction>().Any(r => r.Scope == ReviveScope.Visible);
			float score = visibleScope ? context.Downed.Count : 1f;
			if (best == null || score > best.Score)
				best = new AllySkillChoice(Name, option, score, $"Revive {context.Downed.Count} downed");
		}
		return best;
	}
}
