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

	public GameAction ToAction(Character caster) => Skill.Targeting == SkillTargeting.Tile && Direction.HasValue ? SkillAction.ForTile(caster, Skill, Direction.Value) : Direction.HasValue
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

			if (skill.Targeting == SkillTargeting.Tile)
            {
                foreach (var cell in skill.GetTargetTiles(caster))
                {
                    var affected = skill.GetTargetCharacters(caster).Where(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, cell) <= skill.AreaRadius).ToList();
                    if (affected.Count > 0) result.Add(new SkillCastOption(skill, null, cell, affected));
                }
            }
            else if (skill.Targeting == SkillTargeting.Missile)
			{
				foreach (var dir in Directions)
				{
					var line = MissileTargeting.TraceLine(caster, dir, skill.MissileRange + (skill.UsesArrows ? ClassPassives.MissileRangeBonus(caster) : 0), skill.AreaRadius > 0 ? 1 : ArrowSupply.Penetration(caster, skill), skill.AreaRadius > 0);
                    var hit = new MissileTargeting.Hit(line.Endpoint, line.Encounters.LastOrDefault().Character);
                    var affected = skill.AreaRadius > 0 ? skill.TargetingRules.GetMissileAffected(caster, hit) : line.Encounters.Select(h => h.Character).Where(c => skill.TargetSelector.Eligible(caster, c)).ToList();
					if (affected.Any(c => !EnemyBehavior.IsDisguised(c)))
						result.Add(new SkillCastOption(skill, hit.Character, dir, affected));
				}
			}
			else if (skill.RequiresTargetSelection)
			{
				foreach (var candidate in skill.GetTargetCharacters(caster))
				{
					if (candidate == null || EnemyBehavior.IsDisguised(candidate)) continue;
					var affected = skill.GetAffectedCharacters(caster, candidate);
					if (affected.Any(c => !EnemyBehavior.IsDisguised(c)))
						result.Add(new SkillCastOption(skill, candidate, null, affected));
				}
			}
			else
			{
				var affected = skill.GetAffectedCharacters(caster, caster);
				if (affected.Any(c => !EnemyBehavior.IsDisguised(c)))
					result.Add(new SkillCastOption(skill, caster, null, affected));
			}

			// AI decisions use personal, logical sight, including missile and area recipients.
			bool Visible(Character c) => c != null && (c == caster || Game.Instance.CurrentDungeon.CanSee(caster, c));
			return result.Where(o => (o.Target == null || Visible(o.Target)) && o.Affected.Any(Visible)).ToList();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			return new List<SkillCastOption>();
		}
	}
}
