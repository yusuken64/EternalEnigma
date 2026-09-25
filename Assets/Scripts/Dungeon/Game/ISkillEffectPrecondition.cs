// Implemented by skill GameActions whose use depends on the situation (e.g. an adjacent trap).
// Character.CanCast refuses the skill, with the returned reason, when any effect says no.
internal interface ISkillEffectPrecondition
{
	bool CanUse(Character caster, out string reason);
}
