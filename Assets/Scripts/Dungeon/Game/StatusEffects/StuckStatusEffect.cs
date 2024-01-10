public class StuckStatusEffect : StatusEffect
{
	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	public override GameAction GetActionOverride(Character character)
	{
		return new SleepTurnAction(character);
	}

	internal override bool PreventsMenu()
	{
		return true;
	}

	internal override string GetEffectName()
	{
		return "Stuck";
	}
}
