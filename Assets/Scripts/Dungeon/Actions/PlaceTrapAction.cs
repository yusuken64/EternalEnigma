using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rogue "Caltrops": place a party trap on the faced tile (or the first free neighbour).
[Serializable]
internal class PlaceTrapAction : GameAction, ISkillEffectPrecondition
{
	private Character caster;
	private bool placed;

	public PlaceTrapAction() { }
	private PlaceTrapAction(Character caster) { this.caster = caster; }

	internal override GameAction AsTargetedSkill(Character caster, Character target) => new PlaceTrapAction(caster);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => new PlaceTrapAction(caster);

	public bool CanUse(Character caster, out string reason)
	{
		var dungeon = Game.Instance?.CurrentDungeon;
		reason = "No free tile to place caltrops.";
		return dungeon != null && dungeon.FindFreeAdjacentTile(caster.TilemapPosition, caster.CurrentFacing) != null;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var owner = caster ?? character;
		var dungeon = Game.Instance.CurrentDungeon;
		var cell = dungeon?.FindFreeAdjacentTile(owner.TilemapPosition, owner.CurrentFacing);
		if (cell == null) return new();
		CaltropTrap.Spawn(dungeon, cell.Value, owner);
		placed = true;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		var owner = caster ?? character;
		Game.Instance.DoFloatingText(placed ? "Caltrops!" : "No room", Color.yellow, owner.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
