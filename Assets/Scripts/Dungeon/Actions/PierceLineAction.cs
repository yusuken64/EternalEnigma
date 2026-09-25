using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class PierceLineAction : GameAction, ISkillCastCondition
{
	[Min(1)] public int Range = 2;
	public int MaxTargets = 2;                 // 0 = unlimited
	[SerializeReference] public ScaledDamageAction Damage = new();
	[NonSerialized] private Character caster;
	[NonSerialized] private SkillRankContext rank;

	public PierceLineAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target)
	{
		return AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	}

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank)
	{
		var bound = new PierceLineAction
		{
			Range = this.Range,
			MaxTargets = this.MaxTargets,
			Damage = this.Damage,
			caster = caster,
			rank = rank
		};
		return bound;
	}

	public bool CanCast(Character caster, out string reason)
	{
		if (Damage != null)
			return Damage.CanCast(caster, out reason);
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
			return new();

		var result = new List<GameAction>();
		var dungeon = Game.Instance.CurrentDungeon;
		var step = GridMovement.GetFacingOffset(caster.CurrentFacing);
		int range = Range + (Damage.Category == DamageCategory.Bow ? ClassPassives.MissileRangeBonus(caster) : 0);

		var hitCharacters = new HashSet<Character>();

		for (int i = 1; i <= range; i++)
		{
			var cell = caster.TilemapPosition + step * i;

			// Stop at the first cell where it's not walkable
			if (!dungeon.IsWalkable(cell))
				break;

			// Find a character at this cell
			var target = Game.Instance.AllCharacters.FirstOrDefault(c =>
				c != null && c != caster && c.Vitals.HP > 0 && c.OverlapsWith(Character.ToBounds(cell)));

			if (target != null)
			{
				// Check if we should hit this character (not already hit and correct team)
				if (!hitCharacters.Contains(target) && target.Team != caster.Team && target.Team != Team.Neutral)
				{
					result.Add(Damage.AsTargetedSkill(caster, target, rank));
					hitCharacters.Add(target);

					// Stop if we've hit MaxTargets
					if (MaxTargets > 0 && hitCharacters.Count >= MaxTargets)
						break;
				}
				// If it's an ally or already hit, skip but don't stop
			}
		}

		return result;
	}
}
