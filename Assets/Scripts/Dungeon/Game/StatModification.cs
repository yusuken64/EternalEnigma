using System;

[Serializable]
public class StatModification : StartingStats
{

	public StatModification()
	{
	}

	public StatModification(Stats other)
	{
		HPMax = other.HPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		EXPOnKill = other.EXPOnKill;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		SPRegenAcccumlateThreshold = other.SPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
	}

	public StatModification(StatModification stats)
	{
		HPMax = stats.HPMax;
		HungerMax = stats.HungerMax;
		Strength = stats.Strength;
		Defense = stats.Defense;
		EXPOnKill = stats.EXPOnKill;
		HungerAccumulateThreshold = stats.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = stats.HPRegenAcccumlateThreshold;
		DropRate = stats.DropRate;
	}

	public static StatModification operator +(StatModification stats, StatModification modification)
	{
		if (stats == null) { stats = new(); }
		if (modification == null) { modification = new(); }

		StatModification retStats = new(stats);
		retStats.HPMax += modification.HPMax;
		retStats.Strength += modification.Strength;
		retStats.Defense += modification.Defense;
		retStats.EXPOnKill += modification.EXPOnKill;
		retStats.HungerAccumulateThreshold += modification.HungerAccumulateThreshold;
		retStats.HPRegenAcccumlateThreshold += modification.HPRegenAcccumlateThreshold;

		return retStats;
	}
}