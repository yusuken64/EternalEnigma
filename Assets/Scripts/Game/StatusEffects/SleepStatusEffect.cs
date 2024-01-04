using System;
using UnityEngine;

//forces sleep behavior on target
//dissapates over n turns
public class SleepStatusEffect : StatusEffect
{
	public void Apply() { }

	public override void Tick()
	{
		Debug.Log("Sleeping !");
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
}
