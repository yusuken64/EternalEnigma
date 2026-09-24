using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CommandStatusEffect : StatusEffect
{
	public string CommandId;
	public string CommandName;
	public StatModification Modification = new();
	public int HealPerTurn;
	public DamageElement BonusElement;
	public int BonusElementPercent;
	public Character Issuer;
	public float Multiplier = 1f;

	private void Awake() => Family = BuffFamily.Command;

	public override GameAction GetActionOverride(Character character) => null;
	internal override StatModification GetStatModification() => null; // applied through ModificationFor
	internal override string GetEffectName() => $"Command: {CommandName}";
	internal override bool PreventsMenu() => false;

	// Per-stat strongest across this character's active commands (each scaled by its Multiplier).
	public static StatModification ModificationFor(Character c)
	{
		if (c == null || c.StatusEffects == null) return new StatModification();
		return BuffStacking.Strongest(c.StatusEffects.OfType<CommandStatusEffect>()
			.Where(x => x != null && !x.IsExpired())
			.Select(x => BuffStacking.Scale(x.Modification, x.Multiplier)));
	}

	internal override List<GameAction> GetTickEffects(Character owner)
	{
		if (HealPerTurn <= 0 || owner == null || owner.Vitals.HP <= 0) return null;
		return new List<GameAction> { new TakeHealAction(Issuer != null ? Issuer : owner, owner,
			Mathf.RoundToInt(HealPerTurn * Multiplier), false) };
	}

	// Arms commands: bonus elemental hit after each of the owner's primary hits on an opposing character.
	internal override IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action)
	{
		if (BonusElementPercent <= 0 || owner == null || action is not TakeDamageAction hit) yield break;
		if (hit.Attacker != owner || hit.ResponseDepth != 0 || hit.Missed || hit.Target == null ||
			hit.Target.Team == owner.Team || hit.Target.Vitals.HP <= 0) yield break;
		int bonus = Math.Max(1, Mathf.RoundToInt(hit.Damage * BonusElementPercent / 100f * Multiplier));
		yield return new TakeDamageAction(owner, hit.Target, bonus, false, false, BonusElement) { ResponseDepth = 1 };
	}

	// Reinforce: heal the recipient when the command ends, if the issuer has CommandExpiryHeal.
	internal override List<GameAction> GetExpiryEffects(Character owner)
	{
		if (Issuer == null || owner == null || owner.Vitals.HP <= 0) return null;
		float percent = PassiveModifiers.SumFloat<CommandExpiryHeal>(Issuer, b => b.HealPercent);
		if (percent <= 0) return null;
		return new List<GameAction> { new TakeHealAction(Issuer, owner, Math.Max(1, Mathf.RoundToInt(owner.FinalStats.HPMax * percent)), false) };
	}

	public static CommandStatusEffect AddOrRefresh(Character recipient, Character issuer, string commandId,
		string commandName, StatModification modification, int healPerTurn,
		DamageElement bonusElement, int bonusElementPercent, int turns)
	{
		var existing = recipient.StatusEffects.OfType<CommandStatusEffect>()
			.FirstOrDefault(x => x != null && !x.IsExpired() && x.CommandId == commandId);
		var effect = existing;
		if (effect == null)
		{
			var go = new GameObject($"Command {commandName}");
			go.transform.SetParent(recipient.VisualParent.transform, false);
			effect = go.AddComponent<CommandStatusEffect>();
			recipient.StatusEffects.Add(effect);
		}
		effect.CommandId = commandId;
		effect.CommandName = commandName;
		effect.Modification = modification ?? new StatModification();
		effect.HealPerTurn = healPerTurn;
		effect.BonusElement = bonusElement;
		effect.BonusElementPercent = bonusElementPercent;
		effect.Issuer = issuer;
		effect.Multiplier = 1f;
		effect.TurnsLeft = existing != null ? Math.Max(existing.TurnsLeft, Math.Max(1, turns)) : Math.Max(1, turns);
		recipient.InvalidateCachedStats();
		recipient.DisplayedStats.Sync(recipient.FinalStats);
		return effect;
	}
}
