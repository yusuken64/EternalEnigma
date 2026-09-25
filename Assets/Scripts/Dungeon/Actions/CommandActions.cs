using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable]
public class ApplyCommandAction : GameAction
{
	public string CommandId;
	public string CommandName;
	public StatModification Modification = new();
	public int HealPerTurn;
	public DamageElement BonusElement = DamageElement.Physical;
	public int BonusElementPercent;
	public int Turns = 5;

	private Character caster;
	private SkillRankContext rank;

	public ApplyCommandAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new ApplyCommandAction
		{
			CommandId = this.CommandId,
			CommandName = this.CommandName,
			Modification = this.Modification,
			HealPerTurn = this.HealPerTurn,
			BonusElement = this.BonusElement,
			BonusElementPercent = this.BonusElementPercent,
			Turns = this.Turns,
			caster = caster,
			rank = rank
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null)
			return new();

		var game = Game.Instance;
		if (game == null)
			return new();

		int turns = rank.Scaling.ScaleDuration(Turns, rank.Rank) + PassiveModifiers.Sum<CommandDurationBonus>(caster, b => b.Turns);
		var mod = ScaleBuffs(Modification, rank);
		int heal = rank.Scaling.ScalePower(HealPerTurn, rank.Rank);
		int bonus = rank.Scaling.ScalePower(BonusElementPercent, rank.Rank);

		var recipients = game.Allies.Where(a => a != null && a.Vitals.HP > 0 && (a == caster || game.CurrentDungeon.CanSee(caster, a)));
		foreach (var recipient in recipients)
		{
			CommandStatusEffect.AddOrRefresh(recipient, caster, string.IsNullOrEmpty(CommandId) ? CommandName : CommandId, CommandName, mod, heal, BonusElement, bonus, turns);
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation && caster != null)
		{
			var game = Game.Instance;
			if (game != null)
				game.DoFloatingText($"{CommandName}!", Color.white, caster.VisualParent.gameObject.transform.position);
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}

	private static StatModification ScaleBuffs(StatModification mod, SkillRankContext rank)
	{
		var result = new StatModification(mod);
		if (mod == null || rank.Scaling == null)
			return result;

		var fields = typeof(StartingStats).GetFields(BindingFlags.Public | BindingFlags.Instance)
			.Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float))
			.ToArray();

		foreach (var field in fields)
		{
			if (field.FieldType == typeof(int))
			{
				int current = (int)field.GetValue(result);
				int scaled = rank.Scaling.ScaleBuff(current, rank.Rank);
				field.SetValue(result, scaled);
			}
		}

		return result;
	}
}

[Serializable]
public class AmplifyCommandsAction : GameAction
{
	public float Multiplier = 2f;
	public int Turns = 2;

	private Character caster;
	private SkillRankContext rank;

	public AmplifyCommandsAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new AmplifyCommandsAction
		{
			Multiplier = this.Multiplier,
			Turns = this.Turns,
			caster = caster,
			rank = rank
		};
		return bound;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		if (game == null)
			return new();

		foreach (var ally in game.Allies)
		{
			if (ally == null) continue;
			var commands = ally.StatusEffects.OfType<CommandStatusEffect>().Where(c => c != null && !c.IsExpired()).ToList();
			foreach (var effect in commands)
			{
				effect.Multiplier = Multiplier;
				effect.TurnsLeft = Math.Max(1, Turns);
			}
			ally.InvalidateCachedStats();
			ally.DisplayedStats.Sync(ally.FinalStats);
		}

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation)
		{
			var game = Game.Instance;
			if (game != null)
				game.DoFloatingText("Decisive Order!", Color.white, caster.VisualParent.gameObject.transform.position);
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
