using System;

[Serializable]
public class StatModification
{
	public int HPMax;
	public int HP;
	public int Strength;
	public int Defense;
	public int EXP;
	public int Level;
	public int Floor;
	public int Hunger;
	public int Gold;
	public int HungerAccumulate;
	public int HPRegenAcccumlate;
	public int HungerAccumulateThreshold;
	public int HPRegenAcccumlateThreshold;

	public StatModification(StatModification other)
	{
		// Copy values from the provided StatModification object
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
	}

	// Default constructor
	public StatModification()
	{
	}

	public static StatModification operator +(StatModification stats, StatModification modification)
	{
		if (stats == null)
		{
			stats = new StatModification();
		}

		if (modification == null)
		{
			modification = new StatModification();
		}

		StatModification retStats = new(stats);
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