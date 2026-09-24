using System.Collections.Generic;
using System.Linq;

public static class StatusCategories
{
	public static readonly HashSet<string> BindNames = new() { "Stuck", "Arm Bind", "Silence" };
	public static readonly HashSet<string> AilmentNames = new() { "Dot", "Sleep", "Frail", "Paralysis", "Curse", "Fear", "Blind", "Burn", "Stun", "Weaken", "Exposed", "Defense Down" };

	public static bool IsBind(StatusEffect status) => status != null && BindNames.Contains(status.GetEffectName());
	public static bool IsAilment(StatusEffect status) => status != null && AilmentNames.Contains(status.GetEffectName());
	// Positive effects Discord may strip from enemies.
	public static bool IsBuff(StatusEffect status) => status != null && !IsBind(status) && !IsAilment(status) &&
		(status is StrengthStatusEffect || status is HotStatusEffect || status is TimedBuffStatusEffect || status is CommandStatusEffect);
	public static int CountAilments(Character character) => character == null || character.StatusEffects == null ? 0 :
		character.StatusEffects.Count(s => s != null && !s.IsExpired() && IsAilment(s));
}
