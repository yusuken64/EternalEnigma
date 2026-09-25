using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Private static helper for scaling buffs with rank context
internal static class SongActionsHelper
{
	internal static StatModification ScaleBuffs(StatModification mod, SkillRankContext rank)
	{
		var result = new StatModification();
		if (mod == null) return result;

		var fields = typeof(StartingStats).GetFields(BindingFlags.Public | BindingFlags.Instance);
		foreach (var field in fields)
		{
			if (field.FieldType == typeof(int))
			{
				int value = (int)field.GetValue(mod);
				int scaled = rank.Scaling.ScaleBuff(value, rank.Rank);
				field.SetValue(result, scaled);
			}
			else if (field.FieldType == typeof(float))
			{
				float value = (float)field.GetValue(mod);
				int scaled = rank.Scaling.ScaleBuff(Mathf.RoundToInt(value * 100), rank.Rank);
				float scaledFloat = scaled / 100f;
				field.SetValue(result, scaledFloat);
			}
		}

		return result;
	}
}

[Serializable]
public class StartSongAction : GameAction
{
	public string SongId;
	public string SongName;
	public StatModification Modification = new();
	public int HealPerTurn;
	public int Radius = 3;
	public int Turns = 5;

	private Character caster;
	private Character target;
	private SkillRankContext rank;

	public StartSongAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var result = new StartSongAction
		{
			SongId = SongId,
			SongName = SongName,
			Modification = Modification,
			HealPerTurn = HealPerTurn,
			Radius = Radius,
			Turns = Turns,
			caster = caster,
			target = target,
			rank = rank
		};
		return result;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null) return new();

		float power = 1f + PassiveModifiers.SumFloat<SongPowerBonus>(caster, b => b.Percent) / 100f;
		var mod = BuffStacking.Scale(SongActionsHelper.ScaleBuffs(Modification, rank), power);
		int heal = Mathf.RoundToInt(rank.Scaling.ScalePower(HealPerTurn, rank.Rank) * power);
		int turns = rank.Scaling.ScaleDuration(Turns, rank.Rank) + PassiveModifiers.Sum<SongDurationBonus>(caster, b => b.Turns);

		SongRules.AddOrRefresh(caster, string.IsNullOrEmpty(SongId) ? SongName : SongId, SongName, mod, heal, Radius, turns);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation)
		{
			Game game = Game.Instance;
			if (game != null)
				game.DoFloatingText($"{SongName}!", Color.yellow, caster.transform.position);
		}
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}

[Serializable]
public class ExtendSongsAction : GameAction
{
	public int Turns = 3;

	private Character caster;
	private Character target;
	private SkillRankContext rank;

	public ExtendSongsAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var result = new ExtendSongsAction
		{
			Turns = Turns,
			caster = caster,
			target = target,
			rank = rank
		};
		return result;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		foreach (var song in SongRules.ActiveSongs(caster))
		{
			song.TurnsLeft += rank.Scaling.ScaleDuration(Turns, rank.Rank);
		}
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}

[Serializable]
public class GrandFinaleAction : GameAction
{
	public int HealPerSong = 10;
	public int DamagePerSong = 8;

	private Character caster;
	private Character target;
	private SkillRankContext rank;

	public GrandFinaleAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var result = new GrandFinaleAction
		{
			HealPerSong = HealPerSong,
			DamagePerSong = DamagePerSong,
			caster = caster,
			target = target,
			rank = rank
		};
		return result;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var songs = SongRules.ActiveSongs(caster);
		int n = songs.Count;
		if (n == 0) return new();

		foreach (var song in songs)
		{
			SongRules.RemoveSong(caster, song);
		}

		var game = Game.Instance;
		var result = new List<GameAction>();

		var allies = game.Allies.Where(a => a != null && a.Vitals.HP > 0 && (a == caster || game.CurrentDungeon.CanSee(caster, a)));
		foreach (var ally in allies)
		{
			result.Add(new TakeHealAction(caster, ally, rank.Scaling.ScalePower(HealPerSong * n, rank.Rank)));
		}

		var enemies = game.Enemies.Where(e => e != null && e.Team != caster.Team && e.Vitals.HP > 0 && game.CurrentDungeon.CanSee(caster, e));
		foreach (var enemy in enemies)
		{
			result.Add(new TakeDamageAction(caster, enemy, rank.Scaling.ScalePower(DamagePerSong * n, rank.Rank)));
		}

		return result;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}

[Serializable]
public class SongCountStrikeAction : GameAction
{
	public float PercentPerHit = 0.7f;

	private Character caster;
	private Character target;
	private SkillRankContext rank;

	public SongCountStrikeAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var result = new SongCountStrikeAction
		{
			PercentPerHit = PercentPerHit,
			caster = caster,
			target = target,
			rank = rank
		};
		return result;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster == null || target == null || target.Vitals.HP <= 0) return new();

		int hits = Math.Max(1, SongRules.ActiveSongs(caster).Count);
		var result = new List<GameAction>();

		for (int i = 0; i < hits; i++)
		{
			AttackAction.GetAttackDamage(caster, target, out bool hit, out int damage);
			result.Add(new TakeDamageAction(caster, target, Math.Max(0, rank.Scaling.ScalePower(Mathf.RoundToInt(damage * PercentPerHit), rank.Rank)), true, !hit));
		}

		return result;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}
