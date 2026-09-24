using System.Linq;

public sealed class DamageEvaluator : IAllySkillEvaluator
{
	public string Name => "Damage";
	public const float NormalAttackMargin = 1.25f;

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null || context.VisibleEnemies.Count == 0) return null;
		float threshold = context.NormalAttackValue > 0f ? context.NormalAttackValue * NormalAttackMargin : 0f;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Damage))
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				// Never hit our own side.
				if (option.Affected.Any(c => c != null && c != context.Ally && c.Team == context.Ally.Team)) continue;
				float score = option.Affected.Where(c => c != null && c.Team != context.Ally.Team && c.Vitals.HP > 0)
					.Sum(c => SkillEstimates.EstimateDamage(skill, context.Ally, c));
				if (score <= threshold) continue;
				if (best == null || score > best.Score)
					best = new AllySkillChoice(Name, option, score, $"{skill.SkillName} ~{score:0} dmg");
			}
		return best;
	}
}
