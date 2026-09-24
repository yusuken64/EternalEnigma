using DG.Tweening;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public static class SkillMovement
{
	/// <summary>
	/// Returns the normalized step direction from 'from' toward 'toward'.
	/// Each component is -1, 0, or 1 based on Math.Sign.
	/// </summary>
	public static Vector3Int Step(Vector3Int from, Vector3Int toward)
	{
		return new Vector3Int(Math.Sign(toward.x - from.x), Math.Sign(toward.y - from.y), 0);
	}

	/// <summary>
	/// Returns 1 if character has 3x3 footprint, 0 otherwise.
	/// Used to calculate reach for multi-tile characters.
	/// </summary>
	public static int HalfSize(Character c)
	{
		return c != null && c.FootPrint == FootPrint.Size3x3 ? 1 : 0;
	}

	/// <summary>
	/// Checks if actor can occupy a destination tile.
	/// Validates that all footprint cells are walkable and no other character overlaps.
	/// </summary>
	public static bool CanOccupy(Character actor, Vector3Int destination)
	{
		var dungeon = Game.Instance?.CurrentDungeon;
		if (dungeon == null || actor == null)
			return false;

		var bounds = Character.ToBounds(actor.FootPrint, destination);

		// Check all cells in bounds are walkable
		foreach (var cell in bounds.allPositionsWithin)
		{
			if (!dungeon.IsWalkable(new Vector3Int(cell.x, cell.y, 0)))
				return false;
		}

		// Check no other character overlaps
		return dungeon.OverlapsAnyOtherCharacter(actor, bounds) == null;
	}

	/// <summary>
	/// Finds a tile behind the target character where the actor can stand.
	/// First tries the preferred tile (directly behind at reach distance),
	/// then falls back to a deterministic ring search.
	/// Returns null if no valid tile found.
	/// </summary>
	public static Vector3Int? FindTileBehind(Character actor, Character target)
	{
		if (actor == null || target == null)
			return null;

		var dir = Step(actor.TilemapPosition, target.TilemapPosition);
		if (dir == Vector3Int.zero)
			dir = Vector3Int.right;

		int reach = HalfSize(target) + HalfSize(actor) + 1;
		var preferred = target.TilemapPosition + dir * reach;

		if (CanOccupy(actor, preferred))
			return preferred;

		// Fallback: search all cells at exactly 'reach' distance (ring)
		// Cells on the ring satisfy: Max(Abs(x), Abs(y)) == reach
		var candidates = new System.Collections.Generic.List<Vector3Int>();

		for (int x = -reach; x <= reach; x++)
		{
			for (int y = -reach; y <= reach; y++)
			{
				if (Math.Max(Math.Abs(x), Math.Abs(y)) == reach)
				{
					var cell = target.TilemapPosition + new Vector3Int(x, y, 0);
					if (CanOccupy(actor, cell))
					{
						candidates.Add(cell);
					}
				}
			}
		}

		if (candidates.Count == 0)
			return null;

		// Sort by: ChevDistance to preferred, then x, then y
		var sorted = candidates
			.OrderBy(c => TileWorldDungeon.ChevDistance(c, preferred))
			.ThenBy(c => c.x)
			.ThenBy(c => c.y)
			.ToList();

		return sorted[0];
	}

	/// <summary>
	/// Finds the last free tile actor can occupy along a direction within maxSteps.
	/// Returns the farthest reachable position, or actor's current position if blocked immediately.
	/// </summary>
	public static Vector3Int LastFreeAlong(Character actor, Vector3Int direction, int maxSteps)
	{
		if (direction == Vector3Int.zero || maxSteps <= 0)
			return actor.TilemapPosition;

		var current = actor.TilemapPosition;

		for (int step = 1; step <= maxSteps; step++)
		{
			var next = actor.TilemapPosition + direction * step;
			if (!CanOccupy(actor, next))
				break;
			current = next;
		}

		return current;
	}

	/// <summary>
	/// Sets the actor's logical tile position without any animation.
	/// The transform position is updated by AnimateMove.
	/// </summary>
	public static void Place(Character actor, Vector3Int destination)
	{
		actor.TilemapPosition = destination;
	}

	/// <summary>
	/// Coroutine that animates the actor's movement from current position to destination.
	/// Plays walk animation during movement, then idle animation after.
	/// </summary>
	public static IEnumerator AnimateMove(Character actor, Vector3Int destination, float seconds = 0.1f)
	{
		if (actor == null)
			yield break;

		var world = Game.Instance.CurrentDungeon.CellToWorld(destination);
		actor.PlayWalkAnimation();
		yield return actor.transform.DOMove(world, seconds).WaitForCompletion();
		actor.PlayIdleAnimation();
	}

	/// <summary>
	/// Instantly snaps the actor to a destination without animation.
	/// Used when animations are skipped (e.g., turn-based movement during AI turns).
	/// </summary>
	public static void SnapTo(Character actor, Vector3Int destination)
	{
		actor.transform.position = Game.Instance.CurrentDungeon.CellToWorld(destination);
	}
}
