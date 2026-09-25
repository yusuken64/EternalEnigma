using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class RandomHitsAction : GameAction, ISkillCastCondition
{
	public int Radius = 2;        // used when Visible == false
	public bool Visible;          // any enemy the caster can see
	[Min(1)] public int MinHits = 4;
	[Min(1)] public int MaxHits = 6;
	public bool ArrowPerHit;
	[SerializeReference] public ScaledDamageAction Damage = new();
	[NonSerialized] private Character caster;
	[NonSerialized] private SkillRankContext rank;

	public RandomHitsAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		return new RandomHitsAction
		{
			Radius = Radius,
			Visible = Visible,
			MinHits = MinHits,
			MaxHits = MaxHits,
			ArrowPerHit = ArrowPerHit,
			Damage = Damage,
			caster = caster,
			rank = rank
		};
	}

	public bool CanCast(Character caster, out string reason)
	{
		if (Damage != null && !Damage.CanCast(caster, out reason))
		{
			return false;
		}

		if (ArrowPerHit && ArrowSupply.Count(caster) < 1)
		{
			reason = "No arrows.";
			return false;
		}

		reason = "";
		return true;
	}

	internal override bool IsValid(Character character)
	{
		return caster != null && caster.Vitals.HP > 0 && Damage != null;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character))
		{
			return new();
		}

		// Find candidates - enemies in range that are not neutral
		var candidates = Game.Instance.AllCharacters.Where(c =>
			c != null &&
			c.Vitals.HP > 0 &&
			c.Team != caster.Team &&
			c.Team != Team.Neutral &&
			(Visible ?
				Game.Instance.CurrentDungeon.CanSee(caster, c) :
				TileWorldDungeon.ChevDistance(c.TilemapPosition, caster.TilemapPosition) <= Radius)
		).ToList();

		if (candidates.Count == 0)
		{
			return new();
		}

		var result = new List<GameAction>();
		int hits = UnityEngine.Random.Range(MinHits, Mathf.Max(MinHits, MaxHits) + 1);

		for (int i = 0; i < hits; i++)
		{
			if (ArrowPerHit)
			{
				int arrowsFired = ArrowSupply.Consume(caster, 1, ClassPassives.ArrowRecoveryChance(caster));
				if (arrowsFired < 1)
				{
					break;
				}
			}

			int targetIndex = UnityEngine.Random.Range(0, candidates.Count);
			Character target = candidates[targetIndex];
			var damageAction = Damage.AsTargetedSkill(caster, target, rank);
			result.Add(damageAction);
		}

		return result;
	}
}
