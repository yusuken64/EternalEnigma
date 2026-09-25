using UnityEngine;

public class EndureStatusEffect : StatusEffect
{
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
		return "Endure";
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
			if (context.Damage >= owner.Vitals.HP && owner.Vitals.HP > 1)
			{
				context.Damage = owner.Vitals.HP - 1;
				TurnsLeft = 0;
			}
		}
	}
}
