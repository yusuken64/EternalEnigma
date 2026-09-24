using System.Collections.Generic;

public class CurseStatusEffect : StatusEffect
{
	[UnityEngine.Range(0f, 1f)] public float ReflectFraction = 0.5f;

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
		return "Curse";
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override IEnumerable<GameAction> GetResponseTo(Character owner, GameAction action)
	{
		if (action is TakeDamageAction hit && hit.Attacker == owner && hit.Target != owner && !hit.Missed &&
			hit.Damage > 0 && DamageResponses.CanRespond(hit))
		{
			int reflected = (int)System.Math.Floor(hit.Damage * ReflectFraction);
			if (reflected > 0)
				return new List<GameAction> { new TakeDamageAction(owner, owner, reflected, true, false) { ResponseDepth = hit.ResponseDepth + 1 } };
		}
		return new List<GameAction>();
	}
}
