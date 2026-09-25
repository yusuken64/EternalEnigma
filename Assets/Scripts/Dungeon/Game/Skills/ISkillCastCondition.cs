// Implemented by skill effects (GameActions in Skill.ActionEffects) that can veto a cast.
public interface ISkillCastCondition
{
	bool CanCast(Character caster, out string reason);
}
