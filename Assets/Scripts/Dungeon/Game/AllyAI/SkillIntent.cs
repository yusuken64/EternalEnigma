using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum SkillIntent
{
	None = 0, Revive = 1, Heal = 2, Cure = 4, Buff = 8, CrowdControl = 16,
	Debuff = 32, Damage = 64, Movement = 128, Utility = 256, Never = 512
}

public static class SkillIntents
{
	public static readonly HashSet<string> DisablingStatuses = new(StringComparer.Ordinal)
		{ "Sleep", "Stun", "Paralysis", "Fear", "Stuck", "Arm Bind", "Silence", "Blind", "Taunt" };
	public static readonly HashSet<string> DebuffStatuses = new(StringComparer.Ordinal)
		{ "Weaken", "Frail", "Exposed", "Curse", "Dot", "Burn" };
	public static readonly HashSet<string> AilmentStatuses = new(StringComparer.Ordinal)
		{ "Dot", "Sleep", "Frail", "Paralysis", "Curse", "Fear", "Blind", "Burn", "Stun", "Weaken", "Exposed" };
	public static readonly HashSet<string> BindStatuses = new(StringComparer.Ordinal)
		{ "Arm Bind", "Stuck", "Silence" };

	public static bool Has(this SkillIntent value, SkillIntent flag) => flag != SkillIntent.None && (value & flag) == flag;

	public static bool TargetsEnemies(Skill skill) => skill?.TargetSelector != null &&
		(skill.TargetSelector.Team == TargetTeam.Enemies || skill.TargetSelector.Team == TargetTeam.All);

	public static SkillIntent Classify(Skill skill)
	{
		if (skill == null || skill.ActivationType != ActivationType.Active || skill.Targeting == SkillTargeting.InventoryItem || skill.ActionEffects == null)
		{
			return SkillIntent.None;
		}

		bool enemies = TargetsEnemies(skill);
		var result = SkillIntent.None;

		foreach (var effect in skill.ActionEffects)
		{
			if (effect == null) continue;

			if (effect is RetreatAction)
			{
				result |= SkillIntent.Never;
			}
			else if (effect is ReviveAction)
			{
				result |= SkillIntent.Revive;
			}
			else if (effect is TakeHealAction)
			{
				result |= SkillIntent.Heal;
			}
			else if (effect is ICureEffect)
			{
				result |= SkillIntent.Cure;
			}
			else if (effect is TakeDamageAction && enemies)
			{
				result |= SkillIntent.Damage;
			}
			else if (effect is DashStrikeAction || effect is TeleportBehindAction || effect is ShadowDanceAction)
			{
				result |= SkillIntent.Damage | SkillIntent.Movement;
			}
			else if (effect is StepBackAction || effect is GrappleLineAction)
			{
				result |= SkillIntent.Movement;
			}
			else if (effect is SongCountStrikeAction)
			{
				result |= SkillIntent.Damage;
			}
			else if (effect is StartSongAction || effect is ApplyCommandAction || effect is SummonCloneAction)
			{
				result |= SkillIntent.Buff;
			}
			else if (effect is DominateAction)
			{
				result |= SkillIntent.CrowdControl;
			}
			else if (effect is DisarmTrapAction)
			{
				result |= SkillIntent.Utility;
			}
			else if (effect is ApplyStatusEffectAction applyStatus)
			{
				if (applyStatus.StatusEffect != null)
				{
					var name = applyStatus.StatusEffect.GetEffectName();
					if (enemies)
					{
						if (DisablingStatuses.Contains(name))
						{
							result |= SkillIntent.CrowdControl;
						}
						else if (DebuffStatuses.Contains(name))
						{
							result |= SkillIntent.Debuff;
						}
					}
					else
					{
						if (applyStatus.StatusEffect is TimedBuffStatusEffect || name == "Strength" || name == "Hot")
						{
							result |= SkillIntent.Buff;
						}
					}
				}
			}
		}

		return result;
	}
}
