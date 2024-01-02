using System;
using UnityEngine;

[Serializable]
public class Stats
{
	public int HPMax;
	public int HungerMax;
	public int Strength;
	public int Defense;
	public int EXPOnKill;

	public int HungerAccumulateThreshold;
	public int HPRegenAcccumlateThreshold;
	public float DropRate;


	public Stats()
	{
	}

	public Stats(Stats other)
	{
		HPMax = other.HPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
	}

	public static Stats operator +(Stats stats, StatModification modification)
	{
		if (stats == null) { stats = new(); }
		if (modification == null) { modification = new(); }

		Stats retStats = new(stats);

		retStats.HPMax += modification.HPMax;
		retStats.HungerMax += modification.HungerMax;
		retStats.Strength += modification.Strength;
		retStats.Defense += modification.Defense;
		retStats.HungerAccumulateThreshold += modification.HungerAccumulateThreshold;
		retStats.HPRegenAcccumlateThreshold += modification.HPRegenAcccumlateThreshold;

		return retStats;
	}

	internal void Sync(Stats other)
	{
		HPMax = other.HPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
	}
}