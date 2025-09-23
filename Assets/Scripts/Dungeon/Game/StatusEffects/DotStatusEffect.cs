using System.Collections.Generic;

public class DotStatusEffect : StatusEffect
{
	public int TickDamage;

	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	internal override bool PreventsMenu()
	{
		return false;
	}

	internal override string GetEffectName()
	{
		return "Dot";
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override List<GameAction> GetTickEffects(Character character)
	{
		return new() { new TakeDamageAction(character, character, TickDamage, false, false) };
	}
}
