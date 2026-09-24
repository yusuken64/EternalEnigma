using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TimedBuffStatusEffect : StatusEffect
{
	public string BuffName = "Buff";
	public StatModification Modification = new();

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	public override bool PreventsMenu()
	{
		return false;
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return Modification;
	}

	internal override string GetEffectName()
	{
		return BuffName;
	}

	public int Magnitude()
	{
		if (Modification == null)
			return 0;

		int result = 0;

		// Sum of absolute values of int fields
		result += Math.Abs(Modification.HPMax);
		result += Math.Abs(Modification.SPMax);
		result += Math.Abs(Modification.HungerMax);
		result += Math.Abs(Modification.Strength);
		result += Math.Abs(Modification.Defense);
		result += Math.Abs(Modification.EXPOnKill);
		result += Math.Abs(Modification.HungerAccumulateThreshold);
		result += Math.Abs(Modification.HPRegenAcccumlateThreshold);
		result += Math.Abs(Modification.SPRegenAcccumlateThreshold);
		result += Math.Abs(Modification.ActionsPerTurnMax);
		result += Math.Abs(Modification.AttacksPerTurnMax);
		result += Math.Abs(Modification.FireResistance);
		result += Math.Abs(Modification.IceResistance);
		result += Math.Abs(Modification.LightningResistance);

		// 100 * sum of absolute values of float fields
		float floatSum = Math.Abs(Modification.DropRate) +
						Math.Abs(Modification.CritChance) +
						Math.Abs(Modification.Evasion) +
						Math.Abs(Modification.HitBonus);
		result += (int)Math.Round(100 * floatSum);

		return result;
	}

	internal override string StackKey
	{
		get
		{
			if (Family == BuffFamily.None)
			{
				return "TimedBuff:" + BuffName;
			}
			else
			{
				return Family + ":" + string.Join(",", ModifiedStatNames());
			}
		}
	}

	private List<string> ModifiedStatNames()
	{
		var names = new List<string>();

		if (Modification == null)
			return names;

		// Check each field and add its name if non-zero
		if (Modification.ActionsPerTurnMax != 0) names.Add("ActionsPerTurnMax");
		if (Modification.AttacksPerTurnMax != 0) names.Add("AttacksPerTurnMax");
		if (Modification.CritChance != 0) names.Add("CritChance");
		if (Modification.Defense != 0) names.Add("Defense");
		if (Modification.DropRate != 0) names.Add("DropRate");
		if (Modification.EXPOnKill != 0) names.Add("EXPOnKill");
		if (Modification.Evasion != 0) names.Add("Evasion");
		if (Modification.FireResistance != 0) names.Add("FireResistance");
		if (Modification.HitBonus != 0) names.Add("HitBonus");
		if (Modification.HPMax != 0) names.Add("HPMax");
		if (Modification.HPRegenAcccumlateThreshold != 0) names.Add("HPRegenAcccumlateThreshold");
		if (Modification.HungerAccumulateThreshold != 0) names.Add("HungerAccumulateThreshold");
		if (Modification.HungerMax != 0) names.Add("HungerMax");
		if (Modification.IceResistance != 0) names.Add("IceResistance");
		if (Modification.LightningResistance != 0) names.Add("LightningResistance");
		if (Modification.SPMax != 0) names.Add("SPMax");
		if (Modification.SPRegenAcccumlateThreshold != 0) names.Add("SPRegenAcccumlateThreshold");
		if (Modification.Strength != 0) names.Add("Strength");

		// Already alphabetically sorted by the order we added them
		return names;
	}

	internal override void ReApply<T>(T newStatus)
	{
		if (newStatus is TimedBuffStatusEffect incoming && incoming.Magnitude() >= Magnitude())
		{
			Modification = new StatModification(incoming.Modification);
			BuffName = incoming.BuffName;
			TurnsLeft = incoming.TurnsLeft;
		}
		// Otherwise do nothing (the stronger current buff wins)
	}
}
