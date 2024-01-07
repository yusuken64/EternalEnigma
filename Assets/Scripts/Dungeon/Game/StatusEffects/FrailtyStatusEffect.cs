public class FrailtyStatusEffect : StatusEffect
{
	public int Stacks;

	public override void Apply()
	{
		Stacks = 1;
	}

	internal override void ReApply<T>(T newStatus)
	{
		Stacks++;
	}

	//Presists until cleansed by item
	public override void Tick() { }

	internal override StatModification GetStatModification()
	{
		return new StatModification()
		{
			Strength = -Stacks
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
		return "Frail";
	}
}
