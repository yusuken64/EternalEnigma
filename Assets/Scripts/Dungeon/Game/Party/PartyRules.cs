using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PartyRules
{
	public static bool IsSummon(Character c) => c != null && c.GetComponent<SummonedUnit>() != null;

	public static bool IsStanding(Game game, Ally ally) => ally != null && game != null && game.Allies.Contains(ally) && ally.Vitals != null && ally.Vitals.HP > 0 && !IsSummon(ally);

	public static IReadOnlyList<Ally> StandingMembers(Game game)
	{
		if (game == null || game.Allies == null)
			return new List<Ally>();
		return game.Allies.Where(a => IsStanding(game, a)).ToList();
	}

	public static bool IsPartyDefeated(Game game) => StandingMembers(game).Count == 0;

	public static IReadOnlyList<Ally> PartyMembers(Game game)
	{
		var members = new List<Ally>();

		// Add non-null, non-summon members from game.Allies
		if (game != null && game.Allies != null)
		{
			members.AddRange(game.Allies.Where(a => a != null && !IsSummon(a)));
		}

		// Add non-null members from game.DownedAllies
		if (game != null && game.DownedAllies != null)
		{
			members.AddRange(game.DownedAllies.Where(a => a != null));
		}

		return members.Distinct().ToList();
	}

	public static void MarkDowned(Game game, Ally ally)
	{
		game.Allies.Remove(ally);
		if (!game.DownedAllies.Contains(ally))
			game.DownedAllies.Add(ally);

		ally.IsDowned = true;

		foreach (var effect in ally.StatusEffects.ToList())
			if (effect != null)
				UnityEngine.Object.Destroy(effect.gameObject);
		ally.StatusEffects.Clear();
		ally.InvalidateCachedStats();
	}

	public static void MarkStanding(Game game, Ally ally)
	{
		game.DownedAllies.Remove(ally);
		if (!game.Allies.Contains(ally))
			game.Allies.Add(ally);

		ally.IsDowned = false;

		if (ally.VisualParent != null)
			ally.VisualParent.SetActive(true);

		ally.InvalidateCachedStats();
	}

	public static void RestoreAllDowned(Game game, int hp)
	{
		foreach (var ally in game.DownedAllies.ToList())
		{
			if (ally != null)
			{
				MarkStanding(game, ally);
				ally.Vitals.HP = hp;
				ally.DisplayedVitals.HP = hp;
				ally.SyncDisplayedStats();
				ally.PlayIdleAnimation();
			}
		}
	}
}
