using System;
using UnityEngine;

[Serializable]
public class Vitals
{
	[SerializeField] private int hp;
	[SerializeField] private int level;
	[SerializeField] private int exp;
	[SerializeField] private int floor;
	[SerializeField] private int hunger;
	[SerializeField] private int gold;
	[SerializeField] private int hungerAccumulate;
	[SerializeField] private int hpRegenAcccumlate;
	private Stats linkedStats;

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

	public Stats LinkedStats 
	{
		get => linkedStats;
		set
		{
			linkedStats = value;
			ClampVitals();
		}
	}

	internal void Sync(Vitals vitals)
	{
		HP = vitals.HP;
		Level = vitals.Level;
		Exp = vitals.Exp;
		Floor = vitals.Floor;
		Hunger = vitals.Hunger;
		Gold = vitals.Gold;
		HungerAccumulate = vitals.HungerAccumulate;
		HPRegenAcccumlate = vitals.HPRegenAcccumlate;
	}

	protected virtual void ClampVitals()
	{
		HP = Math.Clamp(HP, 0, LinkedStats.HPMax);
		Hunger = Math.Clamp(Hunger, 0, LinkedStats.HungerMax);
	}
}