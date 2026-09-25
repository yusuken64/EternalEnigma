using UnityEngine;
using System;

public class DamageShieldStatusEffect : StatusEffect
{
	public int Absorb = 10;
	[NonSerialized] private int remaining = -1;

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	internal override string GetEffectName()
	{
		return "Sanctuary";
	}

	internal override bool PreventsMenu()
	{
		return false;
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override void ReApply<T>(T newStatus)
	{
		TurnsLeft = System.Math.Max(TurnsLeft, newStatus.TurnsLeft);
		remaining = Absorb;
	}

	internal override void ModifyIncomingDamage(Character owner, DamageContext context)
	{
		if (context.Target == owner && !context.Missed)
		{
			if (remaining < 0) remaining = Absorb;
			int absorbed = System.Math.Min(remaining, context.Damage);
			context.Damage -= absorbed;
			remaining -= absorbed;
			if (remaining <= 0) TurnsLeft = 0;
		}
	}
}
