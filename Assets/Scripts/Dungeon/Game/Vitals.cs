using System;
using UnityEngine;

[Serializable]
public class Vitals
{
	[SerializeField] private int hp;
	[SerializeField] private int sp;
	[SerializeField] private int level;
	[SerializeField] private int exp;
	[SerializeField] private int hunger;
	[SerializeField] private int hungerAccumulate;
	[SerializeField] private int hpRegenAcccumlate;
	[SerializeField] private int spRegenAcccumlate;
	[SerializeField] private int actionsPerTurnLeft;
	[SerializeField] private int attacksPerTurnLeft;

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

	public int ActionsPerTurnLeft
	{
		get { return actionsPerTurnLeft; }
		set
		{
			if (actionsPerTurnLeft != value)
			{
				actionsPerTurnLeft = value;
				ClampVitals();
			}
		}
	}

	public int AttacksPerTurnLeft
	{
		get { return attacksPerTurnLeft; }
		set
		{
			if (attacksPerTurnLeft != value)
			{
				attacksPerTurnLeft = value;
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
		Hunger = vitals.Hunger;
		HungerAccumulate = vitals.HungerAccumulate;
		HPRegenAcccumlate = vitals.HPRegenAcccumlate;
		SPRegenAcccumlate = vitals.SPRegenAcccumlate;
		ActionsPerTurnLeft = vitals.ActionsPerTurnLeft;
		AttacksPerTurnLeft = vitals.AttacksPerTurnLeft;
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
			hash = hash * 23 + hunger.GetHashCode();
			hash = hash * 23 + hungerAccumulate.GetHashCode();
			hash = hash * 23 + hpRegenAcccumlate.GetHashCode();
			hash = hash * 23 + spRegenAcccumlate.GetHashCode();
			hash = hash * 23 + actionsPerTurnLeft.GetHashCode();
			hash = hash * 23 + attacksPerTurnLeft.GetHashCode();
			return hash;
		}
	}

	internal string ToDebugString()
	{
		return $"hp:{hp} " +
			$"sp:{sp} " +
			$"level:{level} " +
			$"exp:{exp} " +
			$"hunger:{hunger} " +
			$"hungerAccumulate:{hungerAccumulate} " +
			$"hpRegenAcccumlate:{hpRegenAcccumlate}" +
			$"spRegenAcccumlate:{spRegenAcccumlate}" +
			$"actionsPerTurnLeft:{actionsPerTurnLeft}" +
			$"attacksPerTurnLeft:{attacksPerTurnLeft}";
	}

	public static Vitals operator +(Vitals vitals, VitalModification modification)
	{
		if (modification == null) { modification = new(); }

		vitals.HP += modification.Hp;
		vitals.SP += modification.Sp;
		vitals.Level += modification.Level;
		vitals.Exp += modification.Exp;
		vitals.Hunger += modification.Hunger;
		vitals.HungerAccumulate += modification.HungerAccumulate;
		vitals.HPRegenAcccumlate += modification.HpRegenAcccumlate;
		vitals.SPRegenAcccumlate += modification.SpRegenAcccumlate;
		vitals.ActionsPerTurnLeft += modification.ActionsPerTurnLeft;
		vitals.AttacksPerTurnLeft += modification.AttacksPerTurnLeft;

		return vitals;
	}
}