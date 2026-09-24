using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Skill effect for Clone. Author on a skill with Targeting = Self.
[Serializable]
public class SummonCloneAction : GameAction
{
	public float StatPercent = 0.5f;
	public int Turns = 10;

	private Character caster;
	private float percent;
	private int turns;
	private Ally spawned;

	public SummonCloneAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new SummonCloneAction
		{
			StatPercent = StatPercent, Turns = Turns, caster = caster,
			percent = Mathf.Clamp01(rank.Scaling.ScaleChance(StatPercent, rank.Rank)),
			turns = rank.Scaling.ScaleDuration(Turns, rank.Rank),
		};

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (caster is not Ally summoner) return new();
		var game = Game.Instance;
		var clones = SummonRules.ClonesOf(game, summoner);
		int excess = clones.Count - SummonRules.CloneLimit(summoner) + 1;
		for (int i = 0; i < excess && i < clones.Count; i++)
			SummonRules.Despawn(game, clones[i].GetComponent<SummonedUnit>());
		spawned = SummonRules.SpawnClone(game, summoner, percent, turns);
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || spawned == null) yield break;
		Game.Instance.DoFloatingText("Clone!", Color.cyan, spawned.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
