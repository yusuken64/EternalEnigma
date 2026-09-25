using UnityEngine;

public class AmplifyStatusEffect : StatusEffect
{
	[Range(0f, 2f)] public float Bonus = 0.5f;

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
		return "Amplify";
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
}
