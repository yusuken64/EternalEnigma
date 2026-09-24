using System;
using System.Linq;
using UnityEngine;

[Serializable]
public class DamageBonus : ClassPassive {
	public DamageCategory Category = DamageCategory.Weapon;
	public float Percent = 5f;
	public float PercentPerRank = 0f;

	internal override float DamageMultiplier(Character owner, Skill skill, OutgoingDamage hit) {
		if (owner == null) return 1f;
		int r = RankOf(skill);
		return hit.Category == Category ? 1f + (Percent + PercentPerRank * (r - 1)) / 100f : 1f;
	}
}

[Serializable]
public class AmbushBonus : ClassPassive {
	public float Multiplier = 2f;
	public float MultiplierPerRank = 0.1f;

	internal override float DamageMultiplier(Character owner, Skill skill, OutgoingDamage hit) {
		if (owner == null) return 1f;
		int r = RankOf(skill);
		return owner.StatusEffects.Any(s => s is StealthStatusEffect && !s.IsExpired()) ? Multiplier + MultiplierPerRank * (r - 1) : 1f;
	}
}

[Serializable]
public class ExtraShotPassive : ClassPassive {
	public float Chance = 0.25f;
	public float ChancePerRank = 0.05f;

	internal override float ExtraShotChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Chance + ChancePerRank * (r - 1);
	}
}

[Serializable]
public class ArrowRecoveryPassive : ClassPassive {
	public float Chance = 0.3f;
	public float ChancePerRank = 0.05f;

	internal override float ArrowRecoveryChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Chance + ChancePerRank * (r - 1);
	}
}

[Serializable]
public class MissileRangePassive : ClassPassive {
	public int Tiles = 3;

	internal override int MissileRangeBonus(Character owner, Skill skill) {
		if (owner == null) return 0;
		int r = RankOf(skill);
		return Tiles + (r - 1);
	}
}

[Serializable]
public class SpellEchoPassive : ClassPassive {
	public float Chance = 0.2f;
	public float ChancePerRank = 0.05f;

	internal override float EchoChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Chance + ChancePerRank * (r - 1);
	}
}

[Serializable]
public class ElementalProcPassive : ClassPassive {
	public float Chance = 0.15f;
	public float ChancePerRank = 0.05f;

	internal override float ElementalProcChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		int r = RankOf(skill);
		return Chance + ChancePerRank * (r - 1);
	}
}

[Serializable]
public class FollowUpBonusPassive : ClassPassive {
	public int Turns = 2;
	public float DamageBonus = 0.25f;

	internal override int FollowUpTurnBonus(Character owner, Skill skill) {
		if (owner == null) return 0;
		return Turns;
	}

	internal override float FollowUpDamageBonus(Character owner, Skill skill) {
		if (owner == null) return 0f;
		return DamageBonus;
	}
}

[Serializable]
public class FollowUpChainPassive : ClassPassive {
	public float Chance = 0.3f;

	internal override float FollowUpChainChance(Character owner, Skill skill) {
		if (owner == null) return 0f;
		return Chance;
	}
}

[Serializable]
public class ThrowingArmPassive : ClassPassive {
	public float Multiplier = 1.5f;
	public float MultiplierPerRank = 0.1f;

	internal override float ThrowDamageMultiplier(Character owner, Skill skill) {
		if (owner == null) return 1f;
		int r = RankOf(skill);
		return Multiplier + MultiplierPerRank * (r - 1);
	}
}
