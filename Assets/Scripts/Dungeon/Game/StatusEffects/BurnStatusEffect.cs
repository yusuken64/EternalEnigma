using System.Collections.Generic;

public class BurnStatusEffect : StatusEffect
{
	public int TickDamage = 3;

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
		return "Burn";
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override List<GameAction> GetTickEffects(Character character)
	{
		return new() { new TakeDamageAction(character, character, TickDamage, false, false, DamageElement.Fire) };
	}
}
