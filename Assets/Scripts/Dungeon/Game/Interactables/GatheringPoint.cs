using System.Collections.Generic;
using UnityEngine;

// Optional loot site placed by Core GatheringPlacement. Harvested by walking onto it.
public class GatheringPoint : Interactable
{
	public EternalEnigma.Core.World.GatheringKind Kind;
	public int Roll;

	internal override List<GameAction> GetInteractionSideEffects(Character character) =>
		new List<GameAction> { new GatherAction(this, character) };

	internal override string GetInteractionText() => ExplorationSkillNames.ForKind(Kind) + " point";

	public static GatheringPoint Spawn(TileWorldDungeon dungeon, Vector3Int cell, EternalEnigma.Core.World.GatheringKind kind, int roll)
	{
		var root = new GameObject("Gathering " + kind);
		root.transform.SetParent(dungeon.transform, false);
		root.transform.position = dungeon.CellToWorld(cell);
		var point = root.AddComponent<GatheringPoint>();
		point.Position = cell;
		point.Kind = kind;
		point.Roll = roll;

		float size = dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x;
        DungeonPropModels.Create(kind == EternalEnigma.Core.World.GatheringKind.Ore ? "CrystalOre" : kind == EternalEnigma.Core.World.GatheringKind.Plant ? "Herbs" : "Mushrooms", root.transform, size);
		dungeon.Interactables.Add(point);
		return point;
	}
}
