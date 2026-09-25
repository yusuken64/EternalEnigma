using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class CoverPassive : ClassPassive
{
	public float Chance = 0.3f;
	public float ChancePerRank = 0.05f;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return;

		int r = RankOf(skill);

		if (context.Target != null && context.Target != owner && context.Target.Team == owner.Team && !context.Missed && !context.Redirected && TileWorldDungeon.ChevDistance(owner.TilemapPosition, context.Target.TilemapPosition) <= 1 && UnityEngine.Random.value < Chance + ChancePerRank * (r - 1))
		{
			context.Target = owner;
		}
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		return Enumerable.Empty<GameAction>();
	}
}

[Serializable]
public class SecondWindPassive : ClassPassive
{
	public float Threshold = 0.1f;
	public float HealFraction = 0.3f;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return Enumerable.Empty<GameAction>();

		if (action is TakeDamageAction t && t.Target != null && t.Target.Team == owner.Team && !t.Missed && t.Target.Vitals.HP > 0 && t.Target.Vitals.HP <= Threshold * t.Target.FinalStats.HPMax && ClassPassives.TryUseOncePerFloor(owner, "SecondWind"))
		{
			return new List<GameAction> { new TakeHealAction(owner, t.Target, Math.Max(1, Mathf.RoundToInt(HealFraction * t.Target.FinalStats.HPMax))) };
		}

		return Enumerable.Empty<GameAction>();
	}
}

[Serializable]
public class GrimHarvestPassive : ClassPassive
{
	public int SP = 1;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return Enumerable.Empty<GameAction>();

		int r = RankOf(skill);

		if (action is TakeDamageAction t && t.Target != null && t.Target.Team != owner.Team && t.Target.Team != Team.Neutral && !t.Missed && t.Target.Vitals.HP <= 0 && StatusCategories.CountAilments(t.Target) > 0)
		{
			return new List<GameAction> { RestoreSPAction.Bound(owner, owner, SP + (r >= 3 ? 1 : 0) + (r >= 5 ? 1 : 0)) };
		}

		return Enumerable.Empty<GameAction>();
	}
}

[Serializable]
public class SwiftnessPassive : ClassPassive
{
	public float Chance = 0.15f;
	public float ChancePerRank = 0.03f;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		return Enumerable.Empty<GameAction>();
	}

    internal override void OnTurnStart(Character owner, Skill skill)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return;

		int r = RankOf(skill);

		if (UnityEngine.Random.value < Chance + ChancePerRank * (r - 1))
		{
			owner.Vitals.ActionsPerTurnLeft += 1;
		}
	}
}

[Serializable]
public class OpeningActionPassive : ClassPassive
{
	public bool WholeParty;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		return Enumerable.Empty<GameAction>();
	}

    internal override void OnFloorStart(Character owner, Skill skill)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return;

		IEnumerable<Character> targets = WholeParty && Game.Instance != null ? PartyRules.StandingMembers(Game.Instance).Cast<Character>() : new[] { owner };

		foreach (var c in targets)
		{
			if (c != null && c.Vitals != null)
			{
				c.Vitals.ActionsPerTurnLeft = c.FinalStats.ActionsPerTurnMax + 1;
				c.SyncDisplayedStats();
			}
		}
	}
}

[Serializable]
public class FieldMedicinePassive : ClassPassive
{
	public float HealFraction = 0.1f;
	public float HealFractionPerRank = 0.02f;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		return Enumerable.Empty<GameAction>();
	}

	internal override void OnFloorStart(Character owner, Skill skill)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return;

		if (Game.Instance == null)
			return;

		int r = RankOf(skill);
		var standings = PartyRules.StandingMembers(Game.Instance);

		foreach (var a in standings)
		{
			if (a != null && a.FinalStats != null)
			{
				int max = a.FinalStats.HPMax;
				a.Vitals.HP = Math.Min(max, a.Vitals.HP + Math.Max(1, Mathf.RoundToInt((HealFraction + HealFractionPerRank * (r - 1)) * max)));
				a.SyncDisplayedStats();
			}
		}
	}
}

[Serializable]
public class AuraOfCommandPassive : ClassPassive
{
	public int Radius = 2;
	public int Interval = 3;
	[NonSerialized]
	private int turns;

	internal override void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context)
	{
	}

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action)
	{
		return Enumerable.Empty<GameAction>();
	}

    internal override void OnTurnStart(Character owner, Skill skill)
	{
		if (owner == null || owner.Vitals == null || owner.Vitals.HP <= 0)
			return;

		int r = RankOf(skill);
		turns++;

		int interval = Math.Max(1, Interval - (r - 1) / 2);

		if (turns % interval != 0 || Game.Instance == null)
			return;

		foreach (var a in Game.Instance.Allies)
		{
			if (a != null && a.Vitals != null && a.Vitals.HP > 0 && TileWorldDungeon.ChevDistance(a.TilemapPosition, owner.TilemapPosition) <= Radius)
			{
				a.Vitals.SP = Math.Min(a.FinalStats.SPMax, a.Vitals.SP + 1);
				a.SyncDisplayedStats();
			}
		}
	}
}
