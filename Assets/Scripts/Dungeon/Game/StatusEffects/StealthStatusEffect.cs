using System.Collections.Generic;
using UnityEngine;

public class StealthStatusEffect : StatusEffect
{
	internal override string GetEffectName() => "Stealth";

	internal override bool PreventsMenu() => false;

	internal override StatModification GetStatModification() => null;

	public override void Tick()
	{
		base.Tick();
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action)
	{
		if (action is TakeDamageAction hit && hit.Attacker == owner && hit.Target != owner && !(hit.Target != null && hit.Target.Vitals.HP <= 0 && ClassPassives.KeepsStealthOnKill(owner)))
		{
			return new List<GameAction> { new RemoveStatusEffectAction(owner, this) };
		}
		return new List<GameAction>();
	}
}
