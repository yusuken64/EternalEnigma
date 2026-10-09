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

	public static float NormalAttackExpected(Character attacker, Character target)
	{
		if (attacker == null || target == null) return 0f;
		bool ranged = attacker is Ally ally && ally.IsRangedAttack(out _);
		bool bow = ArrowSupply.HasBow(attacker);
		float power = ranged ? attacker.FinalStats.Strength : BaseAttackDamage(attacker, target);
		return CombatMath.HitChance(attacker, target) * power * (bow ? ArrowSupply.DamageMultiplier(attacker) : 1) *
			(!ranged || bow ? ClassPassives.DamageMultiplier(new OutgoingDamage(attacker, target,
				bow ? DamageCategory.Bow : DamageCategory.Weapon, DamageElement.Physical, false)) : 1);
	}

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
			// ScaledDamageAction case
			else if (effect is ScaledDamageAction scaled)
			{
				float perHit = 0f;
				if (scaled.Scaling == DamageScaling.Strength || scaled.Scaling == DamageScaling.ShieldStrength)
				{
					perHit = BaseAttackDamage(caster, target) * scaled.Percent;
				}
				else if (scaled.Scaling == DamageScaling.Magic)
				{
					perHit = (scaled.BaseDamage + scaled.PerLevel * Math.Max(1, caster.Vitals.Level)) *
						MathF.Pow(15f / 16f, target.FinalStats.Defense / 2f) * scaled.Percent * HeroAttributes.MagicMultiplier(caster.FinalStats.MagicPower);
				}
				if (skill.RankScaling != null)
					perHit = skill.RankScaling.ScalePower((int)perHit, rank);
				perHit = ElementMath.Apply((int)perHit, target.FinalStats, scaled.Element);
				perHit *= ClassPassives.DamageMultiplier(new OutgoingDamage(caster,target,scaled.Category,scaled.Element,true));
				if (scaled.RollToHit)
					perHit *= CombatMath.HitChance(caster, target);
				sum += perHit * scaled.Hits;
			}
			// PierceLineAction case
			else if (effect is PierceLineAction pierce)
			{
				if (pierce.Damage != null)
				{
					// Estimate as if it were a ScaledDamageAction
					var scaledDmg = pierce.Damage;
					float perHit = 0f;
					if (scaledDmg.Scaling == DamageScaling.Strength || scaledDmg.Scaling == DamageScaling.ShieldStrength)
					{
						perHit = BaseAttackDamage(caster, target) * scaledDmg.Percent;
					}
					else if (scaledDmg.Scaling == DamageScaling.Magic)
					{
						perHit = (scaledDmg.BaseDamage + scaledDmg.PerLevel * Math.Max(1, caster.Vitals.Level)) *
							MathF.Pow(15f / 16f, target.FinalStats.Defense / 2f) * scaledDmg.Percent * HeroAttributes.MagicMultiplier(caster.FinalStats.MagicPower);
					}
					if (skill.RankScaling != null)
						perHit = skill.RankScaling.ScalePower((int)perHit, rank);
					perHit = ElementMath.Apply((int)perHit, target.FinalStats, scaledDmg.Element);
					perHit *= ClassPassives.DamageMultiplier(new OutgoingDamage(caster,target,scaledDmg.Category,scaledDmg.Element,true));
					if (scaledDmg.RollToHit)
						perHit *= CombatMath.HitChance(caster, target);
					sum += perHit * scaledDmg.Hits;
				}
			}
			// RandomHitsAction case
			else if (effect is RandomHitsAction random)
			{
				if (random.Damage != null)
				{
					// Estimate as if it were a ScaledDamageAction
					var scaledDmg = random.Damage;
					float perHit = 0f;
					if (scaledDmg.Scaling == DamageScaling.Strength || scaledDmg.Scaling == DamageScaling.ShieldStrength)
					{
						perHit = BaseAttackDamage(caster, target) * scaledDmg.Percent;
					}
					else if (scaledDmg.Scaling == DamageScaling.Magic)
					{
						perHit = (scaledDmg.BaseDamage + scaledDmg.PerLevel * Math.Max(1, caster.Vitals.Level)) *
							MathF.Pow(15f / 16f, target.FinalStats.Defense / 2f) * scaledDmg.Percent * HeroAttributes.MagicMultiplier(caster.FinalStats.MagicPower);
					}
					if (skill.RankScaling != null)
						perHit = skill.RankScaling.ScalePower((int)perHit, rank);
					perHit = ElementMath.Apply((int)perHit, target.FinalStats, scaledDmg.Element);
					perHit *= ClassPassives.DamageMultiplier(new OutgoingDamage(caster,target,scaledDmg.Category,scaledDmg.Element,true));
					if (scaledDmg.RollToHit)
						perHit *= CombatMath.HitChance(caster, target);
					sum += perHit * (random.MinHits + random.MaxHits) / 2f;
				}
			}
		}

		return Math.Max(0f, sum * (skill.UsesArrows ? ArrowSupply.DamageMultiplier(caster) : 1f));
	}

	public static float EstimateHealing(Skill skill, Character caster, Character target)
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
			// Scaled healing uses the same Magic Power multiplier as its action.
			else if (effect is ScaledHealAction scaledHeal)
			{
				int healed = RecoveryMath.Heal(scaledHeal.BaseHeal,scaledHeal.PerLevel,caster?.Vitals?.Level ?? 1,
					skill.RankContext,HeroAttributes.MagicMultiplier(caster?.FinalStats.MagicPower ?? 0) *
					(caster!=null?ClassPassives.HealingMultiplier(caster,target):1f));
				sum += healed;
			}
		}

		return Mathf.Min(sum, Mathf.Max(0, target.FinalStats.HPMax - target.Vitals.HP));
	}
}
