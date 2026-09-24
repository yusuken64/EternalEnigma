using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Scout "Disarm": remove an adjacent revealed trap; 50% (+5 points per extra rank) to salvage Trap Parts.
[Serializable]
internal class DisarmTrapAction : GameAction, ISkillEffectPrecondition
{
	private Character caster;
	private int rank = 1;
	private string message;

	public DisarmTrapAction() { }
	private DisarmTrapAction(Character caster, int rank) { this.caster = caster; this.rank = Mathf.Max(1, rank); }

	internal override GameAction AsTargetedSkill(Character caster, Character target) => new DisarmTrapAction(caster, 1);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rankContext) =>
		new DisarmTrapAction(caster, rankContext.Rank);

	internal static float RecoverChance(int rank) => 0.5f + 0.05f * (Mathf.Max(1, rank) - 1);

	public bool CanUse(Character caster, out string reason)
	{
		var dungeon = Game.Instance?.CurrentDungeon;
		reason = "No revealed trap nearby.";
		return dungeon != null && dungeon.FindAdjacentRevealedTrap(caster.TilemapPosition, caster.CurrentFacing) != null;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var who = caster ?? character;
		var dungeon = Game.Instance.CurrentDungeon;
		var trap = dungeon?.FindAdjacentRevealedTrap(who.TilemapPosition, who.CurrentFacing);
		if (trap == null) { message = "Nothing to disarm"; return new(); }
		dungeon.RemoveInteractable(trap);
		message = "Disarmed!";
		if (UnityEngine.Random.value < RecoverChance(rank) &&
			MaterialCatalog.AddToInventory(Game.Instance.PlayerController.Inventory, MaterialCatalog.Find(MaterialCatalog.TrapParts), 1) > 0)
			message = "Disarmed! +1 " + MaterialCatalog.TrapParts;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || string.IsNullOrEmpty(message)) yield break;
		var who = caster ?? character;
		Game.Instance.DoFloatingText(message, Color.yellow, who.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
