using System;

public enum DamageElement
{
	Physical,
	Fire,
	Ice,
	Lightning
}

public static class ElementMath
{
	// Resistance steps: <= -1 Weak (x1.5), 0 Normal (x1), 1 Resist (x0.5), >= 2 Immune (x0).
	public static float Multiplier(int resistanceSteps)
	{
		if (resistanceSteps <= -1)
			return 1.5f;
		else if (resistanceSteps == 0)
			return 1f;
		else if (resistanceSteps == 1)
			return 0.5f;
		else // resistanceSteps >= 2
			return 0f;
	}

	public static int Resistance(Stats stats, DamageElement element)
	{
		if (stats == null || element == DamageElement.Physical)
			return 0;

		return element switch
		{
			DamageElement.Fire => stats.FireResistance,
			DamageElement.Ice => stats.IceResistance,
			DamageElement.Lightning => stats.LightningResistance,
			_ => 0
		};
	}

	public static int Apply(int damage, Stats targetStats, DamageElement element)
	{
		if (damage <= 0)
			return damage;

		return (int)MathF.Floor(damage * Multiplier(Resistance(targetStats, element)));
	}
}
