using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum DamageCategory { Weapon, Bow, Magic, Throw }
public enum StatCondition { ShieldEquipped, BowEquipped, HpBelowFraction }

public readonly struct OutgoingDamage
{
	public OutgoingDamage(Character attacker, Character target, DamageCategory category, DamageElement element, bool isSkill)
	{ Attacker = attacker; Target = target; Category = category; Element = element; IsSkill = isSkill; }
	public Character Attacker { get; }
	public Character Target { get; }
	public DamageCategory Category { get; }
	public DamageElement Element { get; }
	public bool IsSkill { get; }
}

// Base for Phase 7 class passives. Put instances in Skill.PassiveResponses of a Passive skill.
[Serializable]
public abstract class ClassPassive : PassiveResponse
{
	protected static int RankOf(Skill skill) => skill == null || skill.Rank < 1 ? 1 : skill.Rank;

	internal override IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action) => Enumerable.Empty<GameAction>();
	internal virtual void OnFloorStart(Character owner, Skill skill) { }
	internal virtual void OnTurnStart(Character owner, Skill skill) { }
	internal virtual float DamageMultiplier(Character owner, Skill skill, OutgoingDamage hit) => 1f;
	internal virtual float HealingMultiplier(Character owner, Skill skill, Character target) => 1f;
	internal virtual float AilmentChanceBonus(Character owner, Skill skill) => 0f;
	internal virtual int StatusDurationBonus(Character owner, Skill skill, StatusEffect status) => 0;
	internal virtual bool BlocksStatus(Character owner, Skill skill, StatusEffect status) => false;
	// Must NOT read owner.FinalStats (called from inside Character.UpdateCachedStats). Use BaseStats/Vitals/Equipment.
	internal virtual StatModification ConditionalStats(Character owner, Skill skill) => null;
	internal virtual int MissileRangeBonus(Character owner, Skill skill) => 0;
	internal virtual float ArrowRecoveryChance(Character owner, Skill skill) => 0f;
	internal virtual float ExtraShotChance(Character owner, Skill skill) => 0f;
	internal virtual float EchoChance(Character owner, Skill skill) => 0f;
	internal virtual float ElementalProcChance(Character owner, Skill skill) => 0f;
	internal virtual float ThrowDamageMultiplier(Character owner, Skill skill) => 1f;
	internal virtual float ConsumableSaveChance(Character owner, Skill skill) => 0f;
	internal virtual int FollowUpTurnBonus(Character owner, Skill skill) => 0;
	internal virtual float FollowUpDamageBonus(Character owner, Skill skill) => 0f;
	internal virtual float FollowUpChainChance(Character owner, Skill skill) => 0f;
	internal virtual bool KeepsStealthOnKill(Character owner, Skill skill) => false;
}

public static class ClassPassives
{
	public static IEnumerable<(ClassPassive Passive, Skill Skill)> Of(Character c)
	{
		if (c == null || c.Skills == null)
			yield break;

		foreach (var skill in c.Skills)
		{
			if (skill != null && skill.ActivationType == ActivationType.Passive && skill.PassiveResponses != null)
			{
				foreach (var p in skill.PassiveResponses.OfType<ClassPassive>())
				{
					yield return (p, skill);
				}
			}
		}
	}

	public static float DamageMultiplier(OutgoingDamage hit)
	{
		float product = 1f;
		foreach (var (passive, skill) in Of(hit.Attacker))
		{
			product *= passive.DamageMultiplier(hit.Attacker, skill, hit);
		}
		return product;
	}

	public static float HealingMultiplier(Character healer, Character target)
	{
		float product = 1f;
		foreach (var (passive, skill) in Of(healer))
		{
			product *= passive.HealingMultiplier(healer, skill, target);
		}
		return product;
	}

	public static float ThrowDamageMultiplier(Character c)
	{
		float product = 1f;
		foreach (var (passive, skill) in Of(c))
		{
			product *= passive.ThrowDamageMultiplier(c, skill);
		}
		return product;
	}

	public static float AilmentChanceBonus(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.AilmentChanceBonus(c, skill);
		}
		return sum;
	}

	public static int StatusDurationBonus(Character c, StatusEffect status)
	{
		int sum = 0;
		if (status == null)
			return sum;

		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.StatusDurationBonus(c, skill, status);
		}
		return sum;
	}

	public static int MissileRangeBonus(Character c)
	{
		int sum = 0;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.MissileRangeBonus(c, skill);
		}
		return sum;
	}

	public static float ArrowRecoveryChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.ArrowRecoveryChance(c, skill);
		}
		return Mathf.Clamp(sum, 0f, 0.9f);
	}

	public static float ExtraShotChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.ExtraShotChance(c, skill);
		}
		return Mathf.Clamp01(sum);
	}

	public static float EchoChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.EchoChance(c, skill);
		}
		return Mathf.Clamp01(sum);
	}

	public static float ElementalProcChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.ElementalProcChance(c, skill);
		}
		return Mathf.Clamp01(sum);
	}

	public static float ConsumableSaveChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.ConsumableSaveChance(c, skill);
		}
		return Mathf.Clamp(sum, 0f, 0.9f);
	}

	public static int FollowUpTurnBonus(Character c)
	{
		int sum = 0;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.FollowUpTurnBonus(c, skill);
		}
		return sum;
	}

	public static float FollowUpDamageBonus(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.FollowUpDamageBonus(c, skill);
		}
		return sum;
	}

	public static float FollowUpChainChance(Character c)
	{
		float sum = 0f;
		foreach (var (passive, skill) in Of(c))
		{
			sum += passive.FollowUpChainChance(c, skill);
		}
		return Mathf.Clamp01(sum);
	}

	public static bool IsImmune(Character c, StatusEffect status)
	{
		if (status == null)
			return false;

		return Of(c).Any(x => x.Passive.BlocksStatus(c, x.Skill, status));
	}

	public static bool KeepsStealthOnKill(Character c)
	{
		return Of(c).Any(x => x.Passive.KeepsStealthOnKill(c, x.Skill));
	}

	public static StatModification ConditionalStats(Character c)
	{
		var result = new StatModification();

		foreach (var (passive, skill) in Of(c))
		{
			var conditionalStats = passive.ConditionalStats(c, skill);
			if (conditionalStats != null)
			{
				result = result + conditionalStats;
			}
		}

		return result;
	}

	public static void OnTurnStart(Character c)
	{
		foreach (var (passive, skill) in Of(c).ToList())
		{
			passive.OnTurnStart(c, skill);
		}
	}

	public static void OnFloorStart(Game game)
	{
		ResetFloorState();

		if (game == null)
			return;

		foreach (var ally in PartyRules.StandingMembers(game))
		{
			foreach (var (passive, skill) in Of(ally).ToList())
			{
				passive.OnFloorStart(ally, skill);
			}
		}
	}

	private static readonly HashSet<string> onceFlags = new();

	public static bool TryUseOncePerFloor(Character owner, string key)
	{
		if (owner == null)
			return false;

		return onceFlags.Add(owner.GetInstanceID() + ":" + key);
	}

	public static void ResetFloorState()
	{
		onceFlags.Clear();
	}
}

public static class StatScaling
{
	public static StatModification Scale(StatModification mod, SkillRankScaling scaling, int rank)
	{
		if (mod == null)
			mod = new StatModification();

		if (scaling == null)
			scaling = new SkillRankScaling();

		rank = Math.Max(1, rank);
		float f = 1f + 0.25f * (rank - 1);

		var result = new StatModification();

		result.HPMax = scaling.ScaleBuff(mod.HPMax, rank);
		result.SPMax = scaling.ScaleBuff(mod.SPMax, rank);
		result.HungerMax = scaling.ScaleBuff(mod.HungerMax, rank);
		result.Strength = scaling.ScaleBuff(mod.Strength, rank);
		result.Defense = scaling.ScaleBuff(mod.Defense, rank);
		result.EXPOnKill = scaling.ScaleBuff(mod.EXPOnKill, rank);
		result.HungerAccumulateThreshold = scaling.ScaleBuff(mod.HungerAccumulateThreshold, rank);
		result.HPRegenAcccumlateThreshold = scaling.ScaleBuff(mod.HPRegenAcccumlateThreshold, rank);
		result.SPRegenAcccumlateThreshold = scaling.ScaleBuff(mod.SPRegenAcccumlateThreshold, rank);
		result.ActionsPerTurnMax = scaling.ScaleBuff(mod.ActionsPerTurnMax, rank);
		result.AttacksPerTurnMax = scaling.ScaleBuff(mod.AttacksPerTurnMax, rank);
		result.FireResistance = scaling.ScaleBuff(mod.FireResistance, rank);
		result.IceResistance = scaling.ScaleBuff(mod.IceResistance, rank);
		result.LightningResistance = scaling.ScaleBuff(mod.LightningResistance, rank);
		result.CritChance = mod.CritChance * f;
		result.Evasion = mod.Evasion * f;
		result.HitBonus = mod.HitBonus * f;
		result.DropRate = mod.DropRate;

		return result;
	}
}
