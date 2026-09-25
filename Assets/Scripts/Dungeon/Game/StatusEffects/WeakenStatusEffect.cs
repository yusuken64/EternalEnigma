using System;

public class WeakenStatusEffect : StatusEffect
{
	public int Amount = 3;

	public override void Apply()
	{
	}

	internal override void ReApply<T>(T newStatus)
	{
		TurnsLeft = Math.Max(TurnsLeft, newStatus.TurnsLeft);
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return new StatModification()
		{
			Strength = -Amount
		};
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override bool PreventsMenu()
	{
		return false;
	}

	internal override string GetEffectName()
	{
		return "Weaken";
	}
}
