using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum SkillIntent
{
	None = 0, Revive = 1, Heal = 2, Cure = 4, Buff = 8, CrowdControl = 16,
	Debuff = 32, Damage = 64, Movement = 128, Utility = 256, Never = 512, CastingSupport = 1024
}

public static class SkillIntents
{
	public static readonly HashSet<string> DisablingStatuses = new(StringComparer.Ordinal)
		{ "Sleep", "Stun", "Paralysis", "Fear", "Stuck", "Arm Bind", "Silence", "Blind", "Taunt", "Confusion" };
	public static readonly HashSet<string> DebuffStatuses = new(StringComparer.Ordinal)
		{ "Weaken", "Frail", "Exposed", "Curse", "Dot", "Burn", "Defense Down" };
	public static readonly HashSet<string> AilmentStatuses = new(StringComparer.Ordinal)
		{ "Dot", "Sleep", "Frail", "Paralysis", "Curse", "Fear", "Blind", "Burn", "Stun", "Weaken", "Exposed" };
	public static readonly HashSet<string> BindStatuses = new(StringComparer.Ordinal)
		{ "Arm Bind", "Stuck", "Silence" };

	public static bool Has(this SkillIntent value, SkillIntent flag) => flag != SkillIntent.None && (value & flag) == flag;

	public static bool TargetsEnemies(Skill skill) => skill?.TargetSelector != null &&
		(skill.TargetSelector.Team == TargetTeam.Enemies || skill.TargetSelector.Team == TargetTeam.All);

	private static SkillIntent ClassifyStatus(StatusEffect status, bool enemies)
	{
		if (status == null)
			return SkillIntent.None;

		var name = status.GetEffectName();
		if (enemies)
		{
			if (DisablingStatuses.Contains(name))
				return SkillIntent.CrowdControl;
			else if (DebuffStatuses.Contains(name) || status is FollowUpMarkStatusEffect)
				return SkillIntent.Debuff;
		}
		else
		{
			if (StatusCategories.IsBuff(status))
				return SkillIntent.Buff;
		}

		return SkillIntent.None;
	}

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

			if (effect is ReduceCastAction) { result |= SkillIntent.CastingSupport; }
            else if (effect is RetreatAction)
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
			else if (effect is CleanseAction cleanse)
			{
				if (cleanse.Buffs && enemies) result |= SkillIntent.Debuff;
				if ((cleanse.Ailments || cleanse.Binds) && !enemies) result |= SkillIntent.Cure;
			}
			else if (effect is ICureEffect && !enemies)
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
					result |= ClassifyStatus(applyStatus.StatusEffect, enemies);
				}
			}
			else if (effect is ScaledDamageAction || effect is PierceLineAction || effect is RandomHitsAction)
			{
				if (enemies)
					result |= SkillIntent.Damage;
			}
			else if (effect is ScaledHealAction)
			{
				result |= SkillIntent.Heal;
			}
			else if (effect is RestoreSPAction)
			{
				result |= SkillIntent.Buff;
			}
			else if (effect is ApplyStatusChanceAction c)
			{
				if (c.StatusEffect != null)
				{
					if (c.OnCaster)
					{
						result |= SkillIntent.Buff;
					}
					else
					{
						result |= ClassifyStatus(c.StatusEffect, enemies);
					}
				}
			}
			else if (effect is SpreadAilmentsAction)
			{
				result |= SkillIntent.Debuff;
			}
			else if (effect is FieldKitchenAction)
			{
				result |= SkillIntent.Utility;
			}
		}

		return result;
	}
}
