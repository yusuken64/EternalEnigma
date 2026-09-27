using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Player-placed (Rogue "Caltrops"). Always visible; ignores the party; Sticks one enemy, then breaks.
public class CaltropTrap : Trap
{
	public Character Owner;

	internal override string GetInteractionText() => "Caltrops";

	internal override List<GameAction> GetTrapSideEffects(Character character)
	{
		if (!CanTrigger(character) || character.Team == Team.Player) return new();
		var dungeon = Game.Instance.CurrentDungeon;
		var stuck = Game.Instance.StatusEffectPrefabs.FirstOrDefault(x => x.GetEffectName() == "Stuck");
		dungeon?.RemoveInteractable(this);
		if (stuck == null) return new();
		return new List<GameAction> { new ApplyStatusEffectAction(character, stuck, Owner != null ? Owner : character) };
	}

	public static CaltropTrap Spawn(TileWorldDungeon dungeon, Vector3Int cell, Character owner)
	{
		var root = new GameObject("Caltrops");
		root.transform.SetParent(dungeon.transform, false);
		root.transform.position = dungeon.CellToWorld(cell);
		var trap = root.AddComponent<CaltropTrap>();
		trap.Position = cell;
		trap.Owner = owner;

		float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
        var visual = DungeonPropModels.Create("Caltrops", root.transform, size);
		trap.VisualObject = visual;   // active => "revealed", so pathfinding treats it as a known trap

		dungeon.Interactables.Add(trap);
		return trap;
	}
}
