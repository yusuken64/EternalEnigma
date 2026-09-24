using System.Collections.Generic;
using UnityEngine;

public class TauntStatusEffect : StatusEffect
{
	public Character Taunter;

	internal override string GetEffectName() => "Taunt";

	internal override bool PreventsMenu() => false;

	internal override StatModification GetStatModification() => null;

	public override void Tick()
	{
		base.Tick();
	}

	internal override void OnApplied(Character owner, Character source)
	{
		if (source != null)
		{
			Taunter = source;
		}
	}

	internal override void ReApply<T>(T newStatus)
	{
		TurnsLeft = System.Math.Max(TurnsLeft, newStatus.TurnsLeft);
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}
}
