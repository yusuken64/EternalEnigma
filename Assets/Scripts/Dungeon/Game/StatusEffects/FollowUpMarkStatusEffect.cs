using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FollowUpMarkStatusEffect : StatusEffect
{
	public DamageElement Element = DamageElement.Fire;
	[Range(0f, 2f)] public float DamagePercent = 0.5f;
	[NonSerialized] public Character Source;

	public override GameAction GetActionOverride(Character character)
	{
		return null;
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
		return Element + " Mark";
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override string StackKey => "FollowUpMark:" + Element;

	internal override void ReApply<T>(T newStatus)
	{
		TurnsLeft = Math.Max(TurnsLeft, newStatus.TurnsLeft);
	}

	internal override void OnApplied(Character owner, Character source)
	{
		if (source != null) Source = source;
		if (Source != null) TurnsLeft += ClassPassives.FollowUpTurnBonus(Source);
	}

	internal override IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action)
	{
		if (!(action is TakeDamageAction hit &&
		      hit.Target == owner &&
		      !hit.Missed &&
		      hit.Attacker != null &&
		      Source != null &&
		      Source.Vitals.HP > 0 &&
		      owner.Vitals.HP > 0 &&
		      hit.Attacker.Team == Source.Team &&
		      hit.Element == DamageElement.Physical &&
		      DamageResponses.CanRespond(hit) &&
		      !IsExpired()))
		{
			return Enumerable.Empty<GameAction>();
		}

		int damage = Math.Max(1, Mathf.FloorToInt(Source.FinalStats.Strength * DamagePercent * (1f + ClassPassives.FollowUpDamageBonus(Source)) * MathF.Pow(15f / 16f, owner.FinalStats.Defense)));

		var result = new List<GameAction>
		{
			new TakeDamageAction(Source, owner, damage, true, false, Element) { ResponseDepth = hit.ResponseDepth + 1 }
		};

		if (UnityEngine.Random.value < ClassPassives.FollowUpChainChance(Source))
		{
			result.Add(new TakeDamageAction(Source, owner, damage, true, false, Element) { ResponseDepth = hit.ResponseDepth + 1 });
		}

		return result;
	}
}
