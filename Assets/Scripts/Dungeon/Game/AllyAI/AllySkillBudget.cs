using System;
using System.Collections.Generic;
using System.Linq;

public static class AllySkillBudget
{
	public const int ArrowReserve = 3;
	public const float EmergencyHpFraction = 0.3f;

	public static bool IsReserveSkill(Skill skill)
	{
		var intent = SkillIntents.Classify(skill);
		return intent.Has(SkillIntent.Revive) || intent.Has(SkillIntent.Heal);
	}

	// Highest SP cost among heal/revive skills; halved (integer division) for Aggresive.
	public static int SpReserve(IEnumerable<Skill> skills, AllyStrategy strategy)
	{
		if (skills == null) return 0;
		int reserve = skills.Where(s => s != null && IsReserveSkill(s)).Select(s => Math.Max(0, s.SPCost)).DefaultIfEmpty(0).Max();
		return strategy == AllyStrategy.Aggresive ? reserve / 2 : reserve;
	}

	// Heal/revive skills may spend the reserve; everything else must leave it untouched.
	public static bool CanAfford(int currentSp, Skill skill, int reserve)
	{
		if (skill == null || currentSp < skill.SPCost) return false;
		return IsReserveSkill(skill) || currentSp - skill.SPCost >= Math.Max(0, reserve);
	}

	public static bool ArrowsAllow(int available, int required) => required <= 0 || available - required >= ArrowReserve;

	public static bool IsEmergency(int hp, int hpMax) => hpMax > 0 && hp > 0 && hp < hpMax * EmergencyHpFraction;

	public static float DangerScore(int strength, int hp, int hpMax, bool isBoss)
	{
		float health = hpMax > 0 ? Math.Max(0, hp) / (float)hpMax : 0f;
		return Math.Max(0, strength) * (0.5f + health) * (isBoss ? 2f : 1f);
	}
}
