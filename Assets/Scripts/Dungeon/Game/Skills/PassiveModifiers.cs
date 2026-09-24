using System;
using System.Collections.Generic;
using System.Linq;

// Data-only passive effects: they never respond to actions; systems read them through PassiveModifiers.
[Serializable]
public abstract class PassiveModifier : PassiveResponse
{
	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action) => Enumerable.Empty<GameAction>();
}

[Serializable] public class SummonLimitBonus : PassiveModifier { public int ExtraClones = 1; }
[Serializable] public class SongSlotBonus : PassiveModifier { public int ExtraSlots = 1; }
[Serializable] public class SongDurationBonus : PassiveModifier { public int Turns = 1; }
[Serializable] public class SongPowerBonus : PassiveModifier { public float Percent = 50f; }
[Serializable] public class PerformingBonus : PassiveModifier { public StatModification Bonus = new(); }
[Serializable] public class CommandDurationBonus : PassiveModifier { public int Turns = 1; }
[Serializable] public class CommandExpiryHeal : PassiveModifier { public float HealPercent = 0.1f; }
[Serializable] public class RevealTreasurePassive : PassiveModifier { }

public static class PassiveModifiers
{
	// Every modifier of type T on the character's learned passive skills.
	public static IEnumerable<T> Of<T>(Character c) where T : PassiveResponse
	{
		if (c == null || c.Skills == null) yield break;
		foreach (var skill in c.Skills)
		{
			if (skill == null || skill.ActivationType != ActivationType.Passive || skill.PassiveResponses == null) continue;
			foreach (var response in skill.PassiveResponses)
				if (response is T typed) yield return typed;
		}
	}

	public static int Sum<T>(Character c, Func<T, int> value) where T : PassiveResponse => Of<T>(c).Sum(value);
	public static float SumFloat<T>(Character c, Func<T, float> value) where T : PassiveResponse => Of<T>(c).Sum(value);

	public static bool PartyHas<T>(Game game) where T : PassiveResponse =>
		game != null && PartyRules.PartyMembers(game).Any(member => Of<T>(member).Any());
}
