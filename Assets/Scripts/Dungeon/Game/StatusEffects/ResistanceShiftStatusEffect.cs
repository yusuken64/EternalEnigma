using System;

public class ResistanceShiftStatusEffect : StatusEffect
{
	public int FireShift = -1;
	public int IceShift = -1;
	public int LightningShift = -1;

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
			FireResistance = FireShift,
			IceResistance = IceShift,
			LightningResistance = LightningShift
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
		return "Exposed";
	}
}
