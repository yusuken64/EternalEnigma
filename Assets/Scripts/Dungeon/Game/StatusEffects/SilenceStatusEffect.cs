//prevents casting
public class SilenceStatusEffect : StatusEffect
{
	public override void Tick()
	{
		base.Tick();
	}

	internal override StatModification GetStatModification()
	{
		return null;
	}

	public override GameAction GetActionOverride(Character character)
	{
		return null;
	}

	internal override bool PreventsMenu()
	{
		return false;
	}

	internal override string GetEffectName()
	{
		return "Silence";
	}

	internal override bool Interupts(GameAction action)
	{
		return action is CastSpellAction || action is SkillAction;
	}
}
