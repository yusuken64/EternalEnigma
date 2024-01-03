using System;
using UnityEngine;

[Serializable]
public class Stats
{
	private int hPMax;
	private int hungerMax;
	private int strength;
	private int defense;
	private int eXPOnKill;

	private int hungerAccumulateThreshold;
	private int hPRegenAcccumlateThreshold;
	private float dropRate;

	public int HPMax
	{
		get => hPMax;
		set
		{
			hPMax = value;
			OnStatChanged?.Invoke();
		}
	}
	public int HungerMax
	{
		get => hungerMax;
		set
		{
			hungerMax = value;
			OnStatChanged?.Invoke();
		}
	}
	public int Strength
	{
		get => strength;
		set
		{
			strength = value;
			OnStatChanged?.Invoke();
		}
	}
	public int Defense
	{
		get => defense;
		set
		{
			defense = value;
			OnStatChanged?.Invoke();
		}
	}
	public int EXPOnKill
	{
		get => eXPOnKill;
		set
		{
			eXPOnKill = value;
			OnStatChanged?.Invoke();
		}
	}

	public int HungerAccumulateThreshold
	{
		get => hungerAccumulateThreshold;
		set
		{
			hungerAccumulateThreshold = value;
			OnStatChanged?.Invoke();
		}
	}
	public int HPRegenAcccumlateThreshold
	{
		get => hPRegenAcccumlateThreshold;
		set
		{
			hPRegenAcccumlateThreshold = value;
			OnStatChanged?.Invoke();
		}
	}

	internal void FromStartingStats(StartingStats startingStats)
	{
		HPMax = startingStats.HPMax;
		HungerMax = startingStats.HungerMax;
		Strength = startingStats.Strength;
		Defense = startingStats.Defense;
		EXPOnKill = startingStats.EXPOnKill;
		HungerAccumulateThreshold = startingStats.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = startingStats.HPRegenAcccumlateThreshold;
		DropRate = startingStats.DropRate;
	}

	public float DropRate
	{
		get => dropRate;
		set
		{
			dropRate = value;
			OnStatChanged?.Invoke();
		}
	}

	public delegate void StatChangedDelegate();
	public event StatChangedDelegate OnStatChanged;

	public Stats()
	{
	}

	public Stats(Stats other)
	{
		HPMax = other.HPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		EXPOnKill = other.EXPOnKill;
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
		retStats.EXPOnKill += modification.EXPOnKill;
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
		EXPOnKill = other.EXPOnKill;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
	}

	public override int GetHashCode()
	{
		unchecked // Overflow is fine, just wrap
		{
			int hash = 17;
			hash = hash * 23 + HPMax.GetHashCode();
			hash = hash * 23 + HungerMax.GetHashCode();
			hash = hash * 23 + Strength.GetHashCode();
			hash = hash * 23 + Defense.GetHashCode();
			hash = hash * 23 + EXPOnKill.GetHashCode();
			hash = hash * 23 + HungerAccumulateThreshold.GetHashCode();
			hash = hash * 23 + HPRegenAcccumlateThreshold.GetHashCode();
			hash = hash * 23 + DropRate.GetHashCode();
			return hash;
		}
	}

	internal string ToDebugString()
	{
		return $"HPMax: {HPMax} " +
			$"HungerMax: {HungerMax} " +
			$"Strength: {Strength} " +
			$"Defense: {Defense} " +
			$"EXPOnKill: {EXPOnKill} " +
			$"HungerAccumulateThreshold: {HungerAccumulateThreshold} " +
			$"HPRegenAcccumlateThreshold: {HPRegenAcccumlateThreshold} " +
			$"DropRate: {DropRate}";
	}
}