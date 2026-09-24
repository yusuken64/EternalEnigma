using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SkillCastOption
{
	public SkillCastOption(Skill skill, Character target, Vector3Int? direction, IReadOnlyList<Character> affected)
	{
		Skill = skill ?? throw new ArgumentNullException(nameof(skill));
		Target = target;
		Direction = direction;
		Affected = affected ?? Array.Empty<Character>();
	}

	public Skill Skill { get; }
	public Character Target { get; }
	public Vector3Int? Direction { get; }
	public IReadOnlyList<Character> Affected { get; }

	public GameAction ToAction(Character caster) => Direction.HasValue
		? SkillAction.ForMissile(caster, Skill, Direction.Value)
		: new SkillAction(caster, Skill, Target);
}

public static class SkillCastOptions
{
	public static readonly Vector3Int[] Directions =
	{
		new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, -1, 0),
		new Vector3Int(0, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, 1, 0)
	};

	public static List<SkillCastOption> Enumerate(Character caster, Skill skill)
	{
		if (caster == null || skill == null || skill.Targeting == SkillTargeting.InventoryItem)
			return new List<SkillCastOption>();

		try
		{
			var result = new List<SkillCastOption>();

			if (skill.Targeting == SkillTargeting.Missile)
			{
				foreach (var dir in Directions)
				{
					var hit = MissileTargeting.Trace(caster, dir, skill.MissileRange);
					var affected = skill.TargetingRules.GetMissileAffected(caster, hit);
					if (affected.Count > 0)
						result.Add(new SkillCastOption(skill, hit.Character, dir, affected));
				}
			}
			else if (skill.RequiresTargetSelection)
			{
				foreach (var candidate in skill.GetTargetCharacters(caster))
				{
					if (candidate == null) continue;
					var affected = skill.GetAffectedCharacters(caster, candidate);
					if (affected.Count > 0)
						result.Add(new SkillCastOption(skill, candidate, null, affected));
				}
			}
			else
			{
				var affected = skill.GetAffectedCharacters(caster, caster);
				if (affected.Count > 0)
					result.Add(new SkillCastOption(skill, caster, null, affected));
			}

			return result;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			return new List<SkillCastOption>();
		}
	}
}
