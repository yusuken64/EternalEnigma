using System.Collections.Generic;
using System.Linq;

public static class EnemyTargeting
{
	public static bool CanBeTargeted(Character target) =>
		target != null && target.Vitals != null && target.Vitals.HP > 0 &&
		!target.StatusEffects.Any(s => s is StealthStatusEffect && !s.IsExpired());

	// Candidates are already ordered by preference (nearest first) and filtered to what the enemy can see.
	// Returns the enemy's taunter if it is among the targetable candidates, else the first targetable candidate.
	public static Character SelectTarget(Character enemy, IEnumerable<Character> candidates)
	{
		if (candidates == null) return null;
		var targetable = candidates.Where(c => c != null && c != enemy && CanBeTargeted(c)).ToList();
		var taunt = enemy == null ? null : enemy.StatusEffects.OfType<TauntStatusEffect>()
			.FirstOrDefault(t => !t.IsExpired() && t.Taunter != null);
		if (taunt != null && targetable.Contains(taunt.Taunter)) return taunt.Taunter;
		return targetable.FirstOrDefault();
	}
}
