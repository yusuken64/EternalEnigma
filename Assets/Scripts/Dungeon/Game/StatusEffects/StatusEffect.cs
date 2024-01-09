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

	virtual public void Apply() { }

	//status is alreay applied to the target
	//extend the turns left
	//override can add stack behavior i.e. frailty stacks can increase the debuff strength
	virtual internal void ReApply<T>(T newStatus) where T : StatusEffect
	{
		TurnsLeft += newStatus.TurnsLeft;
	}

	//return null if status effect does not modify stats
	abstract internal StatModification GetStatModification();

	abstract internal string GetEffectName();

	abstract internal bool PreventsMenu();

	//return true if this status effect prevents this action
	internal virtual bool Interupts(GameAction action) { return false; }
}