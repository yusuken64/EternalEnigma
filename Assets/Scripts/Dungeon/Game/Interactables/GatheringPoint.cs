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
		var visual = GameObject.CreatePrimitive(kind == EternalEnigma.Core.World.GatheringKind.Ore ? PrimitiveType.Cube : PrimitiveType.Sphere);
		visual.name = "Visual";
		visual.transform.SetParent(root.transform, false);
		visual.transform.localPosition = new Vector3(size * 0.5f, size * 0.5f, -0.25f * size);
		visual.transform.localScale = Vector3.one * 0.45f * size;
		Object.Destroy(visual.GetComponent<Collider>());
		var color = kind == EternalEnigma.Core.World.GatheringKind.Ore ? new Color(0.55f, 0.55f, 0.6f)
			: kind == EternalEnigma.Core.World.GatheringKind.Plant ? new Color(0.3f, 0.75f, 0.3f) : new Color(0.6f, 0.4f, 0.2f);
		var block = new MaterialPropertyBlock();
		block.SetColor("_Color", color); block.SetColor("_BaseColor", color);
		visual.GetComponent<Renderer>().SetPropertyBlock(block);

		dungeon.Interactables.Add(point);
		return point;
	}
}
