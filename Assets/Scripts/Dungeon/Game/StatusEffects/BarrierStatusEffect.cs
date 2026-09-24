using UnityEngine;

public class BarrierStatusEffect : TimedBuffStatusEffect
{
	public DamageElement Element = DamageElement.Fire;

	[UnityEngine.Range(0f, 1f)]
	public float Reduction = 0.75f;

	private void Reset()
	{
		Family = BuffFamily.Barrier;
		BuffName = "Barrier";
	}

	internal override string StackKey => "Barrier:" + Element;

	internal override void ModifyIncomingDamage(Character owner, DamageContext context)
	{
		if (context.Target == owner && !context.Missed && context.Element == Element)
		{
			context.Damage = (int)System.Math.Floor(context.Damage * (1f - Reduction));
		}
	}
}
