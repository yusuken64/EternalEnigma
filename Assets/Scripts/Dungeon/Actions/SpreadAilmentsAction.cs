using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class SpreadAilmentsAction : GameAction
{
	public int Radius = 2;
	[NonSerialized] private Character caster;
	[NonSerialized] private Character target;
	[NonSerialized] private SkillRankContext rank;

	public SpreadAilmentsAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new SpreadAilmentsAction
		{
			Radius = Radius,
			caster = caster,
			target = target,
			rank = rank,
		};

	internal override bool IsValid(Character character) =>
		target != null && target.Vitals.HP > 0;

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		yield break;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();

		var results = new List<GameAction>();

		var ailments = target.StatusEffects
			.Where(s => s != null && !s.IsExpired() && StatusCategories.IsAilment(s))
			.ToList();

		var neighbors = Game.Instance.AllCharacters
			.Where(c => c != null && c != target && c.Vitals.HP > 0 && c.Team == target.Team && TileWorldDungeon.ChevDistance(c.TilemapPosition, target.TilemapPosition) <= Radius)
			.ToList();

		foreach (var ailment in ailments)
		{
			var prefab = Game.Instance.StatusEffectPrefabs?.FirstOrDefault(p => p != null && p.StackKey == ailment.StackKey);
			if (prefab == null) continue;

			foreach (var neighbor in neighbors)
			{
				results.Add(new ApplyStatusEffectAction { StatusEffect = prefab }.AsTargetedSkill(caster, neighbor, rank));
			}
		}

		return results;
	}
}
