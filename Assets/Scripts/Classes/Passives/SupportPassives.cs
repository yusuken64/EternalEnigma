using System;
using System.Linq;
using UnityEngine;

[Serializable]
public class HealingBonus : ClassPassive {
	public float Percent = 10f;
	public float PercentPerRank = 0f;

	internal override float HealingMultiplier(Character owner, Skill skill, Character target) {
		if (owner == null) return 1f;
		int r = RankOf(skill);
		return 1f + (Percent + PercentPerRank * (r - 1)) / 100f;
	}
}

[Serializable]
public class TriagePassive : ClassPassive {
	public float Threshold = 0.3f;
	public float Multiplier = 1.5f;
	public float MultiplierPerRank = 0.1f;

	internal override float HealingMultiplier(Character owner, Skill skill, Character target) {
		if (owner == null) return 1f;
		int r = RankOf(skill);
		if (target != null && target.Vitals != null && target.Vitals.HP <= Threshold * target.FinalStats.HPMax) {
			return Multiplier + MultiplierPerRank * (r - 1);
		}
		return 1f;
	}
}

[Serializable]
public class AilmentChancePassive : ClassPassive {
	public float Bonus = 0.1f;
	public float BonusPerRank = 0f;

	internal override float AilmentChanceBonus(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Bonus + BonusPerRank * (r - 1);
	}
}

[Serializable]
public class StatusDurationPassive : ClassPassive {
	public BuffFamily Family = BuffFamily.None;
	public bool AilmentsOnly;
	public int Turns = 1;

	internal override int StatusDurationBonus(Character owner, Skill skill, StatusEffect status) {
		if (owner == null) return 0;
		if (status == null) return 0;

		if (AilmentsOnly) {
			return StatusCategories.IsAilment(status) ? Turns : 0;
		}

		return Family != BuffFamily.None && status.Family == Family ? Turns : 0;
	}
}

[Serializable]
public class StatusImmunityPassive : ClassPassive {
	public string EffectName = "Stuck";

	internal override bool BlocksStatus(Character owner, Skill skill, StatusEffect status) {
		if (owner == null) return false;
		return status != null && status.GetEffectName() == EffectName;
	}
}

[Serializable]
public class ConditionalStatPassive : ClassPassive {
	public StatCondition Condition;
	public float HpFraction = 0.25f;
	public StatModification Bonus = new();

	internal override StatModification ConditionalStats(Character owner, Skill skill) {
		if (owner == null) return null;

		bool conditionMet = false;

		switch (Condition) {
			case StatCondition.ShieldEquipped:
				conditionMet = owner.Equipment?.EquippedShield?.EquipmentItemDefinition != null &&
					owner.Equipment.EquippedShield.EquipmentItemDefinition.WeaponType == WeaponType.OffhandShield;
				break;
			case StatCondition.BowEquipped:
				conditionMet = ArrowSupply.HasBow(owner);
				break;
			case StatCondition.HpBelowFraction:
				conditionMet = owner.Vitals != null && owner.BaseStats != null &&
					owner.Vitals.HP <= HpFraction * owner.BaseStats.HPMax;
				break;
		}

		if (conditionMet) {
			int r = RankOf(skill);
			return StatScaling.Scale(Bonus, skill?.RankScaling, r);
		}

		return null;
	}
}

[Serializable]
public class ResourcefulPassive : ClassPassive {
	public float Chance = 0.25f;
	public float ChancePerRank = 0.05f;

	internal override float ConsumableSaveChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Chance + ChancePerRank * (r - 1);
	}
}

[Serializable]
public class StealthOnKillPassive : ClassPassive {
	internal override bool KeepsStealthOnKill(Character owner, Skill skill) {
		if (owner == null) return false;
		return true;
	}
}
