using System;

public class BlindStatusEffect : StatusEffect
{
	public float Amount = 0.3f;

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
			HitBonus = -Amount
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
		return "Blind";
	}
}
