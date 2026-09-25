using UnityEngine;

//chance to skip action each turn
public class ParalysisStatusEffect : StatusEffect
{
	[Range(0f, 1f)] public float SkipChance = 0.5f;
	private bool skipping;

	public override void Apply()
	{
		Roll();
	}

	public override void Tick()
	{
		base.Tick();
		Roll();
	}

	private void Roll()
	{
		skipping = Random.value < SkipChance;
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	public override GameAction GetActionOverride(Character character)
	{
		return skipping ? new SleepTurnAction(character) : null;
	}

	internal override bool PreventsMenu()
	{
		return skipping;
	}

	internal override string GetEffectName()
	{
		return "Paralysis";
	}
}
