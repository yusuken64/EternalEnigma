using System.Collections.Generic;
using System.Linq;

public sealed class EmergencyHealEvaluator : IAllySkillEvaluator
{
	public string Name => "EmergencyHeal";

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null) return null;
		var needy = new HashSet<Character>(context.Party.Where(a => a != null &&
			AllySkillBudget.IsEmergency(a.Vitals.HP, a.FinalStats.HPMax)));
		if (needy.Count == 0) return null;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Heal))
		{
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				int covered = option.Affected.Count(needy.Contains);
				if (covered == 0) continue;
				float healing = option.Affected.Where(c => c != null && c.Team == context.Ally.Team)
					.Sum(c => SkillEstimates.EstimateHealing(skill, c));
				float score = covered * 1000f + healing;
				if (best == null || score > best.Score)
					best = new AllySkillChoice(Name, option, score, $"Heal {covered} below 30%");
			}
		}
		return best;
	}
}
