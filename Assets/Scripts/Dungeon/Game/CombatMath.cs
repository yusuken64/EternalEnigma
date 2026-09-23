using UnityEngine;

public static class CombatMath
{
	public const float BaseHitChance = 0.8f;
	public const float MinHitChance = 0.05f;
	public const float MaxHitChance = 1f;
	public const float CritMultiplier = 1.5f;

	public static float HitChance(Stats attacker, Stats target)
	{
		float bonus = attacker != null ? attacker.HitBonus : 0f;
		float evasion = target != null ? target.Evasion : 0f;
		return Mathf.Clamp(BaseHitChance + bonus - evasion, MinHitChance, MaxHitChance);
	}

	public static float HitChance(Character attacker, Character target) =>
		HitChance(attacker != null ? attacker.FinalStats : null, target != null ? target.FinalStats : null);

	public static bool RollHit(Character attacker, Character target) => Random.value < HitChance(attacker, target);

	public static bool RollCrit(Character attacker) =>
		attacker != null && Random.value < Mathf.Clamp01(attacker.FinalStats.CritChance);

	public static int ApplyCrit(int damage) => (int)Mathf.Floor(damage * CritMultiplier);
}
