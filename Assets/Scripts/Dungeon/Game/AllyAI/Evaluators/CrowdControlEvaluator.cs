using System.Linq;

public sealed class CrowdControlEvaluator : IAllySkillEvaluator
{
	public string Name => "CrowdControl";

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null || context.VisibleEnemies.Count == 0) return null;
		AllySkillChoice best = null;
		foreach (var skill in context.Castable.Where(s => context.CanAfford(s)))
		{
			var intent = SkillIntents.Classify(skill);
			if (!intent.Has(SkillIntent.CrowdControl) && !intent.Has(SkillIntent.Debuff)) continue;
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				float score = option.Affected
					.Where(e => e != null && e.Team != context.Ally.Team && e.Vitals.HP > 0)
					.Sum(e => Value(skill, e));
				if (score <= 0f) continue;
				if (best == null || score > best.Score)
					best = new AllySkillChoice(Name, option, score, $"{skill.SkillName} on {option.Affected.Count} enemy");
			}
		}
		return best;
	}

	internal static float Danger(Character e) =>
		AllySkillBudget.DangerScore(e.FinalStats.Strength, e.Vitals.HP, e.FinalStats.HPMax, EnemyRank.IsBoss(e));

	private static float Value(Skill skill, Character enemy)
	{
		if (skill?.ActionEffects == null) return 0f;

		float maxValue = 0f;
		foreach (var effect in skill.ActionEffects)
		{
			if (effect == null) continue;

			if (effect is DominateAction)
			{
				float dominateValue = !EnemyRank.IsBoss(enemy) && !PartyRules.IsSummon(enemy) &&
					enemy.StatusEffects.Any(s => s is FearStatusEffect && !s.IsExpired())
					? Danger(enemy) * 1.5f
					: 0f;
				maxValue = System.Math.Max(maxValue, dominateValue);
			}
			else if (effect is ApplyStatusEffectAction a && a.StatusEffect != null)
			{
				// Check if enemy already has a non-expired status with the same StackKey
				if (enemy.StatusEffects.Any(s => s != null && !s.IsExpired() && s.StackKey == a.StatusEffect.StackKey))
				{
					maxValue = System.Math.Max(maxValue, 0f);
				}
				else
				{
					string name = a.StatusEffect.GetEffectName();
					float statusValue = 0f;

					if (SkillIntents.DisablingStatuses.Contains(name))
					{
						statusValue = Danger(enemy);
					}
					else if (SkillIntents.DebuffStatuses.Contains(name))
					{
						statusValue = enemy.Vitals.HP >= enemy.FinalStats.HPMax * 0.5f
							? Danger(enemy) * 0.5f
							: 0f;
					}

					maxValue = System.Math.Max(maxValue, statusValue);
				}
			}
		}

		return maxValue;
	}
}
