//prevents attacking with weapons or weapon skills
public class ArmBindStatusEffect : StatusEffect
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
		return "Arm Bind";
	}

	internal override bool Interupts(GameAction action)
	{
		return action is AttackAction || action is RangedAttackAction || (action is SkillAction s && s.Skill != null && (s.Skill.IsWeaponSkill || s.Skill.UsesArrows));
	}
}
