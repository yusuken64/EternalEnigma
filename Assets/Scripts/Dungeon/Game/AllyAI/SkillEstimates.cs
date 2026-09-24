using System;
using System.Linq;
using UnityEngine;

public static class SkillEstimates
{
	// Average of the Torneko formula used by AttackAction.GetAttackDamage (rand 112..142 / 128).
	public static float BaseAttackDamage(Character attacker, Character target)
	{
		if (attacker == null || target == null) return 0f;
		return attacker.FinalStats.Strength * Mathf.Pow(15f / 16f, target.FinalStats.Defense) * (127f / 128f);
	}

	public static float NormalAttackExpected(Character attacker, Character target) =>
		attacker == null || target == null ? 0f : CombatMath.HitChance(attacker, target) * BaseAttackDamage(attacker, target);

	public static float EstimateDamage(Skill skill, Character caster, Character target)
	{
		if (skill == null || caster == null || target == null || skill.ActionEffects == null)
			return 0f;

		int rank = Math.Max(1, skill.Rank);
		float sum = 0f;

		foreach (var effect in skill.ActionEffects)
		{
			if (effect == null)
				continue;

			// TakeDamageAction case
			if (effect is TakeDamageAction d)
			{
				int v = skill.RankScaling != null ? skill.RankScaling.ScalePower(d.damage, rank) : d.damage;
				float value = ElementMath.Apply(v, target.FinalStats, d.Element);
				if (d.RollToHit)
					value *= CombatMath.HitChance(caster, target);
				sum += value;
			}
			// DashStrikeAction case
			else if (effect is DashStrikeAction m1)
			{
				sum += m1.DamagePercent * BaseAttackDamage(caster, target);
			}
			// TeleportBehindAction case
			else if (effect is TeleportBehindAction m2)
			{
				sum += m2.DamagePercent * BaseAttackDamage(caster, target);
			}
			// ShadowDanceAction case
			else if (effect is ShadowDanceAction m3)
			{
				sum += m3.DamagePercent * BaseAttackDamage(caster, target);
			}
			// SongCountStrikeAction case
			else if (effect is SongCountStrikeAction s)
			{
				sum += s.PercentPerHit * Math.Max(1, SongRules.ActiveSongs(caster).Count) * BaseAttackDamage(caster, target);
			}
		}

		return Math.Max(0f, sum);
	}

	public static float EstimateHealing(Skill skill, Character target)
	{
		if (skill == null || target == null || skill.ActionEffects == null || skill.ActionEffects.Count == 0)
			return 0f;

		int rank = Math.Max(1, skill.Rank);
		int sum = 0;

		foreach (var effect in skill.ActionEffects)
		{
			if (effect == null)
				continue;

			if (effect is TakeHealAction h)
			{
				int healed = skill.RankScaling != null ? skill.RankScaling.ScalePower(h.healing, rank) : h.healing;
				sum += healed;
			}
		}

		return Mathf.Min(sum, Mathf.Max(0, target.FinalStats.HPMax - target.Vitals.HP));
	}
}
