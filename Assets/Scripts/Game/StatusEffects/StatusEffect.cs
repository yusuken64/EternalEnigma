using System;
using UnityEngine;

public abstract class StatusEffect : MonoBehaviour
{
	public int TurnsLeft;

	//return true if it forces character to take a certain action
	//i.e. sleep
	public abstract GameAction GetActionOverride(Character character);

	//Do all statuseffects expire with turns?
	public virtual void Tick()
	{
		TurnsLeft--;
	}

	internal bool IsExpired()
	{
		return TurnsLeft <= 0;
	}

	//status is alreay applied to the target
	//extend the turns left
	internal void ReApply<T>(T newStatus) where T : StatusEffect
	{
		TurnsLeft += newStatus.TurnsLeft;
	}

	//return null if status effect does not modify stats
	abstract internal StatModification GetStatModification();

	abstract internal bool PreventsMenu();
}