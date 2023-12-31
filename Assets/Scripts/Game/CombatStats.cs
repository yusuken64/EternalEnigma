using System;
using UnityEngine;

[Serializable]
public class CombatStats
{
	[SerializeField]
	private int hP;
	public int HP 
	{
		get => hP;
		set
		{
			hP = value;
			hP = Mathf.Clamp(hP, 0, HPMax);
		}
	}
	public int HPMax;
	public int Strength;
	public int Defense;

	public int EXP;
	public int Level;
	public int Floor; //Does this belong here?
	[SerializeField]
	private int hunger;
	public int Hunger
	{
		get => hunger;
		set
		{
			hunger = value;
			hunger = Mathf.Clamp(hunger, 0, 100);
		}
	}
	public int Gold;

	public int HungerAccumulate;
	public int HPRegenAcccumlate;
	public int HungerAccumulateThreshold = 15;
	public int HPRegenAcccumlateThreshold = 3;
	public float DropRate;

	public CombatStats() { }
	public CombatStats(CombatStats other)
	{
		// Copy values from the provided CombatStats object
		HPMax = other.HPMax;
		HP = other.HP;
		Strength = other.Strength;
		Defense = other.Defense;
		EXP = other.EXP;
		Level = other.Level;
		Floor = other.Floor;
		Hunger = other.Hunger;
		Gold = other.Gold;
		HungerAccumulate = other.HungerAccumulate;
		HPRegenAcccumlate = other.HPRegenAcccumlate;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
	}

	public static CombatStats operator +(CombatStats stats, StatModification modification)
	{
		if (stats == null) { stats = new(); }
		if (modification == null) { modification = new(); }

		CombatStats retStats = new(stats);
		retStats.HPMax += modification.HPMax;
		retStats.HP += modification.HP;
		retStats.Strength += modification.Strength;
		retStats.Defense += modification.Defense;
		retStats.EXP += modification.EXP;
		retStats.Level += modification.Level;
		retStats.Floor += modification.Floor;
		retStats.Hunger += modification.Hunger;
		retStats.Gold += modification.Gold;
		retStats.HungerAccumulate += modification.HungerAccumulate;
		retStats.HPRegenAcccumlate += modification.HPRegenAcccumlate;
		retStats.HungerAccumulateThreshold += modification.HungerAccumulateThreshold;
		retStats.HPRegenAcccumlateThreshold += modification.HPRegenAcccumlateThreshold;

		return retStats;
	}
}

[Serializable]
public class ReadOnlyCombatStats
{
	public readonly int HP;
	public readonly int HPMax;
	public readonly int Strength;
	public readonly int Defense;
	public readonly int EXP;
	public readonly int Level;
	public readonly int Floor;
	public readonly int Hunger;
	public readonly int Gold;
	public readonly int HungerAccumulate;
	public readonly int HPRegenAcccumlate;
	public readonly int HungerAccumulateThreshold;
	public readonly int HPRegenAcccumlateThreshold;
	public readonly float DropRate;

	public ReadOnlyCombatStats(CombatStats originalStats)
	{
		HP = originalStats.HP;
		HPMax = originalStats.HPMax;
		Strength = originalStats.Strength;
		Defense = originalStats.Defense;
		EXP = originalStats.EXP;
		Level = originalStats.Level;
		Floor = originalStats.Floor;
		Hunger = originalStats.Hunger;
		Gold = originalStats.Gold;
		HungerAccumulate = originalStats.HungerAccumulate;
		HPRegenAcccumlate = originalStats.HPRegenAcccumlate;
		HungerAccumulateThreshold = originalStats.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = originalStats.HPRegenAcccumlateThreshold;
		DropRate = originalStats.DropRate;
	}
}