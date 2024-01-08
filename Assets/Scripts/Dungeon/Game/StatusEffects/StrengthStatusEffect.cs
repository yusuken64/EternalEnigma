public class StrengthStatusEffect : StatusEffect
{
	public int Stacks;
	public int StrengthPerStack = 5;

	public override void Apply()
	{
		Stacks = 1;
	}

	internal override void ReApply<T>(T newStatus)
	{
		Stacks++;
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return new StatModification()
		{
			Strength = Stacks * StrengthPerStack
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
		return "Strength";
	}
}
