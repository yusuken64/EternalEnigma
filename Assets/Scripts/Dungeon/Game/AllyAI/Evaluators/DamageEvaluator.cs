using System.Linq;

public sealed class DamageEvaluator : IAllySkillEvaluator
{
	public string Name => "Damage";
	public const float NormalAttackMargin = 1.25f;

	public AllySkillChoice Evaluate(AllySkillContext context)
		=> Evaluate(context, false);

    internal AllySkillChoice EvaluateCoverage(AllySkillContext context) => Evaluate(context, true);

    private AllySkillChoice Evaluate(AllySkillContext context, bool coverage)
	{
		if (context == null || context.VisibleEnemies.Count == 0) return null;
		float threshold = !coverage && context.NormalAttackValue > 0f ? context.NormalAttackValue * NormalAttackMargin : 0f;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Damage))
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				// Never hit our own side.
				if (option.Affected.Any(c => !context.VisibleEnemies.Contains(c))) continue;
				float score = option.Affected.Where(context.VisibleEnemies.Contains)
					.Sum(c => SkillEstimates.EstimateDamage(skill, context.Ally, c));
				if (score <= threshold) continue;
				if (best == null || score > best.Score)
					best = new AllySkillChoice(Name, option, score, $"{skill.SkillName} ~{score:0} dmg");
			}
		return best;
	}
}
