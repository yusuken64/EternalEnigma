using System.Linq;

public sealed class UtilityEvaluator : IAllySkillEvaluator
{
	public string Name => "Utility";

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null || context.VisibleEnemies.Count > 0) return null;
		foreach (var skill in context.WithIntent(SkillIntent.Utility))
		{
			var kitchen = skill.ActionEffects.OfType<FieldKitchenAction>().FirstOrDefault();
			if (kitchen != null)
			{
				int amount = skill.RankContext.Scaling.ScalePower(kitchen.HungerRestore, skill.RankContext.Rank);
				if (amount <= 0 || !context.Party.Any(a => a.FinalStats.HungerMax - a.Vitals.Hunger >= amount)) continue;
			}
			var option = SkillCastOptions.Enumerate(context.Ally, skill).FirstOrDefault();
			if (option != null) return new AllySkillChoice(Name, option, 1f, skill.SkillName);
		}
		return null;
	}
}
