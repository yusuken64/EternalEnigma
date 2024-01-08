using System;
using UnityEngine;

[Serializable]
public class Vitals
{
	[SerializeField] private int hp;
	[SerializeField] private int sp;
	[SerializeField] private int level;
	[SerializeField] private int exp;
	[SerializeField] private int floor;
	[SerializeField] private int hunger;
	[SerializeField] private int gold;
	[SerializeField] private int hungerAccumulate;
	[SerializeField] private int hpRegenAcccumlate;
	[SerializeField] private int spRegenAcccumlate;

	public int HP
	{
		get { return hp; }
		set
		{
			if (hp != value)
			{
				hp = value;
				ClampVitals();
			}
		}
	}


	public int SP
	{
		get { return sp; }
		set
		{
			if (sp != value)
			{
				sp = value;
				ClampVitals();
			}
		}
	}

	public int Level
	{
		get { return level; }
		set
		{
			if (level != value)
			{
				level = value;
				ClampVitals();
			}
		}
	}

	public int Exp
	{
		get { return exp; }
		set
		{
			if (exp != value)
			{
				exp = value;
				ClampVitals();
			}
		}
	}

	public int Floor
	{
		get { return floor; }
		set
		{
			if (floor != value)
			{
				floor = value;
				ClampVitals();
			}
		}
	}

	public int Hunger
	{
		get { return hunger; }
		set
		{
			if (hunger != value)
			{
				hunger = value;
				ClampVitals();
			}
		}
	}

	public int Gold
	{
		get { return gold; }
		set
		{
			if (gold != value)
			{
				gold = value;
				ClampVitals();
			}
		}
	}

	public int HungerAccumulate
	{
		get { return hungerAccumulate; }
		set
		{
			if (hungerAccumulate != value)
			{
				hungerAccumulate = value;
				ClampVitals();
			}
		}
	}

	public int HPRegenAcccumlate
	{
		get { return hpRegenAcccumlate; }
		set
		{
			if (hpRegenAcccumlate != value)
			{
				hpRegenAcccumlate = value;
				ClampVitals();
			}
		}
	}
	public int SPRegenAcccumlate
	{
		get { return spRegenAcccumlate; }
		set
		{
			if (spRegenAcccumlate != value)
			{
				spRegenAcccumlate = value;
				ClampVitals();
			}
		}
	}

	public Func<Stats> LinkedStats;


	public Vitals(Vitals vitals)
	{
		this.LinkedStats = vitals.LinkedStats;
		Sync(vitals);
	}

	public Vitals()
	{
	}

	internal void Sync(Vitals vitals)
	{
		HP = vitals.HP;
		SP = vitals.SP;
		Level = vitals.Level;
		Exp = vitals.Exp;
		Floor = vitals.Floor;
		Hunger = vitals.Hunger;
		Gold = vitals.Gold;
		HungerAccumulate = vitals.HungerAccumulate;
		HPRegenAcccumlate = vitals.HPRegenAcccumlate;
		SPRegenAcccumlate = vitals.SPRegenAcccumlate;
	}

	protected virtual void ClampVitals()
	{
		Stats stats = LinkedStats();
		HP = Math.Clamp(HP, 0, stats.HPMax);
		SP = Math.Clamp(SP, 0, stats.SPMax);
		Hunger = Math.Clamp(Hunger, 0, stats.HungerMax);
	}

	public override int GetHashCode()
	{
		unchecked // Overflow is fine, just wrap
		{
			int hash = 17;
			hash = hash * 23 + hp.GetHashCode();
			hash = hash * 23 + sp.GetHashCode();
			hash = hash * 23 + level.GetHashCode();
			hash = hash * 23 + exp.GetHashCode();
			hash = hash * 23 + floor.GetHashCode();
			hash = hash * 23 + hunger.GetHashCode();
			hash = hash * 23 + gold.GetHashCode();
			hash = hash * 23 + hungerAccumulate.GetHashCode();
			hash = hash * 23 + hpRegenAcccumlate.GetHashCode();
			hash = hash * 23 + spRegenAcccumlate.GetHashCode();
			return hash;
		}
	}

	internal string ToDebugString()
	{
		return $"hp:{hp} " +
			$"sp:{sp} " +
			$"level:{level} " +
			$"exp:{exp} " +
			$"floor:{floor} " +
			$"hunger:{hunger} " +
			$"gold:{gold} " +
			$"hungerAccumulate:{hungerAccumulate} " +
			$"hpRegenAcccumlate:{hpRegenAcccumlate}" +
			$"spRegenAcccumlate:{spRegenAcccumlate}";
	}

	public static Vitals operator +(Vitals vitals, VitalModification modification)
	{
		if (modification == null) { modification = new(); }

		vitals.HP += modification.Hp;
		vitals.SP += modification.Sp;
		vitals.Level += modification.Level;
		vitals.Exp += modification.Exp;
		vitals.Floor += modification.Floor;
		vitals.Hunger += modification.Hunger;
		vitals.Gold += modification.Gold;
		vitals.HungerAccumulate += modification.HungerAccumulate;
		vitals.HPRegenAcccumlate += modification.HpRegenAcccumlate;
		vitals.SPRegenAcccumlate += modification.SpRegenAcccumlate;

		return vitals;
	}
}