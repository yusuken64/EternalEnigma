using System;
using UnityEngine;

[Serializable]
public class Stats
{
	private int hPMax;
	private int sPMax;
	private int hungerMax;
	private int strength;
	private int defense;
	private int eXPOnKill;

	private int hungerAccumulateThreshold;
	private int hPRegenAcccumlateThreshold;
	private int sPRegenAcccumlateThreshold;
	private float dropRate;
	private int actionsPerTurnMax;
	private int attacksPerTurnMax;

	private int fireResistance;
	private int iceResistance;
	private int lightningResistance;
	private float critChance;
	private float evasion;
	private float hitBonus;

	public int HPMax
	{
		get => hPMax;
		set
		{
			hPMax = value;
			OnStatChanged?.Invoke();
		}
	}

	public int SPMax
	{
		get => sPMax;
		set
		{
			sPMax = value;
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
	
	public int SPRegenAcccumlateThreshold
	{
		get => sPRegenAcccumlateThreshold;
		set
		{
			sPRegenAcccumlateThreshold = value;
			OnStatChanged?.Invoke();
		}
	}

	public int ActionsPerTurnMax
	{
		get => actionsPerTurnMax;
		set
		{
			actionsPerTurnMax = value;
			OnStatChanged?.Invoke();
		}
	}

	public int AttacksPerTurnMax
	{
		get => attacksPerTurnMax;
		set
		{
			attacksPerTurnMax = value;
			OnStatChanged?.Invoke();
		}
	}

	public int FireResistance
	{
		get => fireResistance;
		set
		{
			fireResistance = value;
			OnStatChanged?.Invoke();
		}
	}

	public int IceResistance
	{
		get => iceResistance;
		set
		{
			iceResistance = value;
			OnStatChanged?.Invoke();
		}
	}

	public int LightningResistance
	{
		get => lightningResistance;
		set
		{
			lightningResistance = value;
			OnStatChanged?.Invoke();
		}
	}

	public float CritChance
	{
		get => critChance;
		set
		{
			critChance = value;
			OnStatChanged?.Invoke();
		}
	}

	public float Evasion
	{
		get => evasion;
		set
		{
			evasion = value;
			OnStatChanged?.Invoke();
		}
	}

	public float HitBonus
	{
		get => hitBonus;
		set
		{
			hitBonus = value;
			OnStatChanged?.Invoke();
		}
	}

	internal void FromStartingStats(StartingStats startingStats)
	{
		HPMax = startingStats.HPMax;
		SPMax = startingStats.SPMax;
		HungerMax = startingStats.HungerMax;
		Strength = startingStats.Strength;
		Defense = startingStats.Defense;
		EXPOnKill = startingStats.EXPOnKill;
		HungerAccumulateThreshold = startingStats.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = startingStats.HPRegenAcccumlateThreshold;
		SPRegenAcccumlateThreshold = startingStats.SPRegenAcccumlateThreshold;
		DropRate = startingStats.DropRate;
		ActionsPerTurnMax = startingStats.ActionsPerTurnMax;
		AttacksPerTurnMax = startingStats.AttacksPerTurnMax;
		FireResistance = startingStats.FireResistance;
		IceResistance = startingStats.IceResistance;
		LightningResistance = startingStats.LightningResistance;
		CritChance = startingStats.CritChance;
		Evasion = startingStats.Evasion;
		HitBonus = startingStats.HitBonus;
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
		SPMax = other.SPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		EXPOnKill = other.EXPOnKill;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		SPRegenAcccumlateThreshold = other.SPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
		ActionsPerTurnMax = other.ActionsPerTurnMax;
		AttacksPerTurnMax = other.AttacksPerTurnMax;
		FireResistance = other.FireResistance;
		IceResistance = other.IceResistance;
		LightningResistance = other.LightningResistance;
		CritChance = other.CritChance;
		Evasion = other.Evasion;
		HitBonus = other.HitBonus;
	}

	public static Stats operator +(Stats stats, StatModification modification)
	{
		if (stats == null) { stats = new(); }
		if (modification == null) { modification = new(); }

		Stats retStats = new(stats);

		retStats.HPMax += modification.HPMax;
		retStats.SPMax += modification.SPMax;
		retStats.HungerMax += modification.HungerMax;
		retStats.Strength += modification.Strength;
		retStats.Defense += modification.Defense;
		retStats.EXPOnKill += modification.EXPOnKill;
		retStats.HungerAccumulateThreshold += modification.HungerAccumulateThreshold;
		retStats.HPRegenAcccumlateThreshold += modification.HPRegenAcccumlateThreshold;
		retStats.SPRegenAcccumlateThreshold += modification.SPRegenAcccumlateThreshold;
		retStats.DropRate += modification.DropRate;
		retStats.ActionsPerTurnMax += modification.ActionsPerTurnMax;
		retStats.AttacksPerTurnMax += modification.AttacksPerTurnMax;
		retStats.FireResistance += modification.FireResistance;
		retStats.IceResistance += modification.IceResistance;
		retStats.LightningResistance += modification.LightningResistance;
		retStats.CritChance += modification.CritChance;
		retStats.Evasion += modification.Evasion;
		retStats.HitBonus += modification.HitBonus;

		return retStats;
	}

	internal void Sync(Stats other)
	{
		HPMax = other.HPMax;
		SPMax = other.SPMax;
		HungerMax = other.HungerMax;
		Strength = other.Strength;
		Defense = other.Defense;
		EXPOnKill = other.EXPOnKill;
		HungerAccumulateThreshold = other.HungerAccumulateThreshold;
		HPRegenAcccumlateThreshold = other.HPRegenAcccumlateThreshold;
		SPRegenAcccumlateThreshold = other.SPRegenAcccumlateThreshold;
		DropRate = other.DropRate;
		ActionsPerTurnMax = other.ActionsPerTurnMax;
		AttacksPerTurnMax = other.AttacksPerTurnMax;
		FireResistance = other.FireResistance;
		IceResistance = other.IceResistance;
		LightningResistance = other.LightningResistance;
		CritChance = other.CritChance;
		Evasion = other.Evasion;
		HitBonus = other.HitBonus;
	}

	public override int GetHashCode()
	{
		unchecked // Overflow is fine, just wrap
		{
			int hash = 17;
			hash = hash * 23 + HPMax.GetHashCode();
			hash = hash * 23 + SPMax.GetHashCode();
			hash = hash * 23 + HungerMax.GetHashCode();
			hash = hash * 23 + Strength.GetHashCode();
			hash = hash * 23 + Defense.GetHashCode();
			hash = hash * 23 + EXPOnKill.GetHashCode();
			hash = hash * 23 + HungerAccumulateThreshold.GetHashCode();
			hash = hash * 23 + HPRegenAcccumlateThreshold.GetHashCode();
			hash = hash * 23 + SPRegenAcccumlateThreshold.GetHashCode();
			hash = hash * 23 + DropRate.GetHashCode();
			hash = hash * 23 + ActionsPerTurnMax.GetHashCode();
			hash = hash * 23 + AttacksPerTurnMax.GetHashCode();
			hash = hash * 23 + FireResistance.GetHashCode();
			hash = hash * 23 + IceResistance.GetHashCode();
			hash = hash * 23 + LightningResistance.GetHashCode();
			hash = hash * 23 + CritChance.GetHashCode();
			hash = hash * 23 + Evasion.GetHashCode();
			hash = hash * 23 + HitBonus.GetHashCode();
			return hash;
		}
	}

	internal string ToDebugString()
	{
		return $"HPMax: {HPMax} " +
			$"SPMax: {SPMax} " +
			$"HungerMax: {HungerMax} " +
			$"Strength: {Strength} " +
			$"Defense: {Defense} " +
			$"EXPOnKill: {EXPOnKill} " +
			$"HungerAccumulateThreshold: {HungerAccumulateThreshold} " +
			$"HPRegenAcccumlateThreshold: {HPRegenAcccumlateThreshold} " +
			$"SPRegenAcccumlateThreshold: {SPRegenAcccumlateThreshold} " +
			$"DropRate: {DropRate} " +
			$"ActionsPerTurnMax: {ActionsPerTurnMax} " +
			$"AttacksPerTurnMax: {AttacksPerTurnMax} " +
			$"FireResistance: {FireResistance} " +
			$"IceResistance: {IceResistance} " +
			$"LightningResistance: {LightningResistance} " +
			$"CritChance: {CritChance} " +
			$"Evasion: {Evasion} " +
			$"HitBonus: {HitBonus}";
	}
}