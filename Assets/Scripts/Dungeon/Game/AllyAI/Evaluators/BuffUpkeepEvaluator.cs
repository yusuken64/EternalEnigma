using System.Linq;

public sealed class BuffUpkeepEvaluator : IAllySkillEvaluator
{
	public string Name => "BuffUpkeep";
	private const int RefreshAtTurns = 1; // recast when 1 turn or less remains

	public AllySkillChoice Evaluate(AllySkillContext context)
	{
		if (context == null || context.VisibleEnemies.Count == 0) return null;
		AllySkillChoice best = null;
		foreach (var skill in context.WithIntent(SkillIntent.Buff))
			foreach (var option in SkillCastOptions.Enumerate(context.Ally, skill))
			{
				int needing = CountNeeding(context, skill, option);
				if (needing <= 0) continue;
				if (best == null || needing > best.Score)
					best = new AllySkillChoice(Name, option, needing, $"Upkeep {skill.SkillName}");
			}
		return best;
	}

	private static int CountNeeding(AllySkillContext context, Skill skill, SkillCastOption option)
	{
		if (skill == null || skill.ActionEffects == null || skill.ActionEffects.Count == 0)
			return 0;

		int maxNeeding = 0;
		foreach (var effect in skill.ActionEffects)
		{
			if (effect == null) continue;

			int needing = 0;
			if (effect is StartSongAction songEffect && songEffect.SongId != null)
			{
				// Song: recast if not active or about to expire
				if (SongRules.ActiveSongs(context.Ally).Any(s => s.SongId == songEffect.SongId && s.TurnsLeft > RefreshAtTurns))
					needing = 0;
				else
					needing = context.Party.Count;
			}
			else if (effect is ApplyCommandAction cmdEffect && cmdEffect.CommandId != null)
			{
				// Command: count party members without the command
				needing = context.Party.Count(p => p != null && !HasCommand(p, cmdEffect.CommandId));
			}
			else if (effect is SummonCloneAction)
			{
				// Clone: recast if under limit
				needing = SummonRules.ClonesOf(context.Game, context.Ally).Count < SummonRules.CloneLimit(context.Ally) ? 1 : 0;
			}
			else if (effect is ApplyStatusEffectAction statusEffect && statusEffect.StatusEffect != null)
			{
				// Status effect: count allies needing it
				needing = 0;
				foreach (var target in option.Affected)
				{
					if (target == null || target.Team != context.Ally.Team) continue;
					if (HasStatus(target, statusEffect.StatusEffect)) continue;

					// Check effect-specific conditions
					if (statusEffect.StatusEffect.GetEffectName() == "Hot")
					{
						// Hot: only apply if HP is below 70%
						if (target.Vitals.HP >= target.FinalStats.HPMax * 0.7f) continue;
					}
					else if (statusEffect.StatusEffect is BarrierStatusEffect)
					{
						// Barrier: only apply if enemies are within 2 tiles of party
						if (!context.VisibleEnemies.Any(e =>
							context.Party.Any(p => TileWorldDungeon.ChevDistance(e.TilemapPosition, p.TilemapPosition) <= 2)))
							continue;
					}

					needing++;
				}
			}

			if (needing > maxNeeding)
				maxNeeding = needing;
		}

		return maxNeeding;
	}

	private static bool HasCommand(Character c, string commandId) =>
		c.StatusEffects.OfType<CommandStatusEffect>().Any(s => s.CommandId == commandId && s.TurnsLeft > RefreshAtTurns);

	private static bool HasStatus(Character c, StatusEffect prefab) =>
		c.StatusEffects.Any(s => s != null && !s.IsExpired() && s.TurnsLeft > RefreshAtTurns && s.StackKey == prefab.StackKey);
}
