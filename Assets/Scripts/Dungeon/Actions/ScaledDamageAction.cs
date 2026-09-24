using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum DamageScaling { Strength, Magic, ShieldStrength }
public enum EquipmentRequirement { None, Shield, Bow }

[Serializable]
public class ScaledDamageAction : GameAction, ISkillCastCondition
{
	public DamageScaling Scaling = DamageScaling.Strength;
	public DamageCategory Category = DamageCategory.Weapon;
	public float Percent = 1f;          // Strength modes: multiplier on the Torneko damage
	public int BaseDamage = 8;          // Magic mode
	public float PerLevel = 2f;         // Magic mode, per caster level
	[Min(1)] public int Hits = 1;
	public DamageElement Element = DamageElement.Physical;
	public bool RollToHit = true;
	public EquipmentRequirement Requires = EquipmentRequirement.None;
	public float PerAilmentBonus;       // Exploit: +x per ailment on the target
	public int HealCasterPerAilment;    // Siphon
	public float ExecuteBelowFraction;  // Assassinate: kill non-bosses at or below this HP fraction
	public bool CanEcho;                // Bolts: Spell Echo may repeat the hit

	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private SkillRankContext rank;

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		// Instead of MemberwiseClone, construct new instance copying every public field
		// to avoid sharing private collections (animationTargets, MetricsModifications)
		var copy = new ScaledDamageAction
		{
			Scaling = this.Scaling,
			Category = this.Category,
			Percent = this.Percent,
			BaseDamage = this.BaseDamage,
			PerLevel = this.PerLevel,
			Hits = this.Hits,
			Element = this.Element,
			RollToHit = this.RollToHit,
			Requires = this.Requires,
			PerAilmentBonus = this.PerAilmentBonus,
			HealCasterPerAilment = this.HealCasterPerAilment,
			ExecuteBelowFraction = this.ExecuteBelowFraction,
			CanEcho = this.CanEcho,
			caster = caster,
			target = target,
			rank = rank
		};
		return copy;
	}

	public bool CanCast(Character caster, out string reason)
	{
		if (Requires == EquipmentRequirement.Shield)
		{
			if (caster.Equipment?.EquippedShield?.EquipmentItemDefinition?.WeaponType != WeaponType.OffhandShield)
			{
				reason = "Needs a shield.";
				return false;
			}
		}

		if (Requires == EquipmentRequirement.Bow)
		{
			if (!ArrowSupply.HasBow(caster))
			{
				reason = "Needs a bow.";
				return false;
			}
		}

		reason = "";
		return true;
	}

	internal override bool IsValid(Character character) =>
		caster != null && target != null && target.Vitals.HP > 0;

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break; // the emitted TakeDamageActions animate
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character))
			return new List<GameAction>();

		var result = new List<GameAction>();

		int ailments = StatusCategories.CountAilments(target);
		var amplify = Element != DamageElement.Physical ?
			caster.StatusEffects.OfType<AmplifyStatusEffect>().FirstOrDefault(s => !s.IsExpired()) :
			null;

		// Repeat Hits times, plus one extra when Echo triggers
		int totalHits = Hits;
		if (CanEcho && UnityEngine.Random.value < ClassPassives.EchoChance(caster))
			totalHits += 1;

		for (int i = 0; i < totalHits; i++)
		{
			int damage = RollDamage(out bool critical);
			result.Add(new TakeDamageAction(caster, target, damage, true, false, Element)
			{
				RollToHit = RollToHit,
				Critical = critical
			});
		}

		// Remove amplify status effect after hits
		if (amplify != null)
			result.Add(new RemoveStatusEffectAction(caster, amplify));

		// Check elemental proc chance
		if (Element != DamageElement.Physical && UnityEngine.Random.value < ClassPassives.ElementalProcChance(caster))
		{
			var procName = Element == DamageElement.Fire ? "Burn" :
						   Element == DamageElement.Ice ? "Stuck" :
						   "Paralysis";
			var proc = StatusEffectRegistry.GetByName(procName);
			if (proc != null)
				result.Add(new ApplyStatusEffectAction { StatusEffect = proc }.AsTargetedSkill(caster, target, rank));
		}

		// Check heal caster per ailment
		if (HealCasterPerAilment > 0 && ailments > 0)
			result.Add(new TakeHealAction(caster, caster, HealCasterPerAilment * ailments));

		return result;
	}

	private int RollDamage(out bool critical)
	{
		float defense = target.FinalStats.Defense;
		float n = UnityEngine.Random.Range(112, 143) / 128f;
		float raw;

		if (Scaling == DamageScaling.Strength)
		{
			raw = caster.FinalStats.Strength * MathF.Pow(15f / 16f, defense) * n * Percent;
		}
		else if (Scaling == DamageScaling.ShieldStrength)
		{
			float shieldDefense = caster.Equipment?.EquippedShield?.EquipmentItemDefinition?.StatModification?.Defense ?? 0;
			raw = (caster.FinalStats.Strength + 2 * shieldDefense) * MathF.Pow(15f / 16f, defense) * n * Percent;
		}
		else // Scaling == DamageScaling.Magic
		{
			raw = (BaseDamage + PerLevel * Math.Max(1, caster.Vitals.Level)) * MathF.Pow(15f / 16f, defense / 2f) * n * Percent;
		}

		// Apply rank scaling
		int damage = rank.Scaling != null ?
			rank.Scaling.ScalePower(Mathf.FloorToInt(raw), rank.Rank) :
			Mathf.FloorToInt(raw);

		// Apply class passive damage multiplier
		damage = Mathf.RoundToInt(damage * ClassPassives.DamageMultiplier(
			new OutgoingDamage(caster, target, Category, Element, true)));

		// Apply amplify status effect bonus
		var amplify = Element != DamageElement.Physical ?
			caster.StatusEffects.OfType<AmplifyStatusEffect>().FirstOrDefault(s => !s.IsExpired()) :
			null;
		if (amplify != null)
			damage = Mathf.RoundToInt(damage * (1f + amplify.Bonus));

		// Apply per-ailment bonus
		if (PerAilmentBonus > 0)
		{
			int ailments = StatusCategories.CountAilments(target);
			damage = Mathf.RoundToInt(damage * (1f + PerAilmentBonus * ailments));
		}

		// Apply crit chance and damage
		critical = Scaling != DamageScaling.Magic && CombatMath.RollCrit(caster);
		if (critical)
			damage = CombatMath.ApplyCrit(damage);

		// Assassinate: instant kill non-bosses at or below HP fraction
		if (ExecuteBelowFraction > 0 && !EnemyRank.IsBoss(target) &&
			target.Vitals.HP <= ExecuteBelowFraction * target.FinalStats.HPMax)
		{
			damage = Math.Max(damage, target.Vitals.HP);
		}

		return Math.Max(1, damage);
	}
}
