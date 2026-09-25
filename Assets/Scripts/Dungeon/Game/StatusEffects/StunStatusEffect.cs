//forces stunned behavior on target
//prevents action and menu interaction
public class StunStatusEffect : StatusEffect
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
		return "Stun";
	}
}
