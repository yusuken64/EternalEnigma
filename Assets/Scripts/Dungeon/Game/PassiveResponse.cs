using System;
using System.Collections.Generic;

// Serialized in Skill.PassiveResponses via [SerializeReference]; subclasses implement passive class behaviors.
[Serializable]
public abstract class PassiveResponse
{
	// Called once per executed action while the owner is alive. Return follow-up actions (may be empty, never null).
	internal abstract IEnumerable<GameAction> Respond(Character owner, Skill skill, GameAction action);

	// Called before any TakeDamageAction applies damage, for every living character in the dungeon.
	internal virtual void ModifyIncomingDamage(Character owner, Skill skill, DamageContext context) { }
}
