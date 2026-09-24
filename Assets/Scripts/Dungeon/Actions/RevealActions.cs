using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Floor Sense. Author on a Self-targeted skill.
[Serializable]
public class RevealFloorLayoutAction : GameAction
{
	public RevealFloorLayoutAction() { }
	internal override GameAction AsTargetedSkill(Character caster, Character target) => new RevealFloorLayoutAction();
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => new RevealFloorLayoutAction();

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		game.FloorReveal.LayoutRevealed = true;
		UnityEngine.Object.FindFirstObjectByType<Minimap>()?.RevealLayout();
		game.UpdateMiniMap();
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation) Game.Instance.DoFloatingText("Floor revealed", Color.cyan, character.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}

// Farsight. Author on a Self-targeted skill.
[Serializable]
public class RevealEnemiesAction : GameAction
{
	public int Turns = 10;
	private int turns;

	public RevealEnemiesAction() { }
	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new RevealEnemiesAction { Turns = Turns, turns = rank.Scaling.ScaleDuration(Turns, rank.Rank) };

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		game.FloorReveal.EnemiesRevealedTurns = Math.Max(game.FloorReveal.EnemiesRevealedTurns, turns);
		game.UpdateMiniMap();
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (!skipAnimation) Game.Instance.DoFloatingText("Enemies revealed", Color.cyan, character.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
