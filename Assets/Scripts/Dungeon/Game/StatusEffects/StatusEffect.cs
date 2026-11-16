using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class StatusEffect : MonoBehaviour
{
	public int TurnsLeft;
	public StatusVisualProfile VisualProfile;

    // Runtime credit only; prefabs and victim-owned tick actions must never become the caster.
    internal Character DamageSource { get; set; }

	// Prevents walking and displacement without preventing attacks or item use.
	internal virtual bool BlocksMovement => false;

	// Notifies active statuses when a character dies or is downed.
	internal virtual void OnCharacterDied(Character owner, Character deceased) { }

	//return true if it forces character to take a certain action
	//i.e. sleep
	public abstract GameAction GetActionOverride(Character character);

	// How this status ends. Turns: counts down and is removed when it runs out.
	// None: never expires on its own; ignores TurnsLeft and stays until removed by another effect (e.g. a cleanse).
	internal virtual StatusExpiration Expiration => StatusExpiration.Turns;

	public virtual void Tick()
	{
		if (Expiration == StatusExpiration.Turns) TurnsLeft--;
	}

	internal bool IsExpired()
	{
		return Expiration == StatusExpiration.Turns && TurnsLeft <= 0;
	}

	// True on the last turn of a turn-limited status, when it is queued for removal.
	internal bool IsOnFinalTurn()
	{
		return Expiration == StatusExpiration.Turns && TurnsLeft == 1;
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

	internal virtual List<GameAction> GetTickEffects(Character character) { return null; }

	public BuffFamily Family;

	// Two statuses with the same key on one character merge (ReApply) instead of coexisting.
	internal virtual string StackKey => GetType().FullName;

	// Called once per executed action (from TurnManager) while the owner is alive. Never return null.
	internal virtual IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action) => Enumerable.Empty<GameAction>();

	// Called when this status is removed from its owner (expiry or cleanse). May return null.
	internal virtual List<GameAction> GetExpiryEffects(Character owner) => null;

	// Called before damage is applied, for every living character's statuses.
	internal virtual void ModifyIncomingDamage(Character owner, DamageContext context) { }

	// Called after this status is applied or re-applied to owner; source is the caster (may be null).
	internal virtual void OnApplied(Character owner, Character source) { }
}

public enum BuffFamily { None, Song, Command, Barrier }

public enum StatusExpiration
{
	Turns,
	None
}
