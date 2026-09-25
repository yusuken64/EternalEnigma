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
		if (character == null || character.Team == Team.Player) return new();
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
		var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		visual.name = "Visual";
		visual.transform.SetParent(root.transform, false);
		visual.transform.localPosition = new Vector3(size * 0.5f, size * 0.5f, -0.05f * size);
		visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
		visual.transform.localScale = new Vector3(0.6f, 0.03f, 0.6f) * size;
		Object.Destroy(visual.GetComponent<Collider>());
		var block = new MaterialPropertyBlock();
		block.SetColor("_Color", Color.gray); block.SetColor("_BaseColor", Color.gray);
		visual.GetComponent<Renderer>().SetPropertyBlock(block);
		trap.VisualObject = visual;   // active => "revealed", so pathfinding treats it as a known trap

		dungeon.Interactables.Add(trap);
		return trap;
	}
}
