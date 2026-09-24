using UnityEngine;

public class DamageReductionStatusEffect : StatusEffect
{
	[Range(0f, 1f)] public float Reduction = 0.5f;

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	internal override string GetEffectName()
	{
		return "Bulwark";
	}

	internal override bool PreventsMenu()
	{
		return false;
	}

	public override void Tick()
	{
		base.Tick();
	}

	internal override void ReApply<T>(T newStatus)
	{
		TurnsLeft = System.Math.Max(TurnsLeft, newStatus.TurnsLeft);
	}

	internal override void ModifyIncomingDamage(Character owner, DamageContext context)
	{
		if (context.Target == owner && !context.Missed)
		{
			context.Damage = Mathf.FloorToInt(context.Damage * (1f - Reduction));
		}
	}
}
