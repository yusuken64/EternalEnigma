using System;

[Serializable]
public class StatModification : Stats
{
	public StatModification()
	{
	}

	public StatModification(Stats other) : base(other)
	{
	}

	public static StatModification operator +(StatModification stats, StatModification modification)
	{
		if (stats == null) { stats = new(); }
		if (modification == null) { modification = new(); }

		StatModification retStats = new(stats);
		retStats.HPMax += modification.HPMax;
		retStats.Strength += modification.Strength;
		retStats.Defense += modification.Defense;
		retStats.HungerAccumulateThreshold += modification.HungerAccumulateThreshold;
		retStats.HPRegenAcccumlateThreshold += modification.HPRegenAcccumlateThreshold;

		return retStats;
	}
}