using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class ThrowItemAction : GameAction
{
	private Character thrower;
	private InventoryItem item;
	private GameObject projectilePrefab;
	private Vector3Int rangedAttackTargetPosition;

	public ThrowItemAction(Character thrower, InventoryItem item, GameObject projectilePrefab)
	{
		this.thrower = thrower;
		this.item = item;
		this.projectilePrefab = projectilePrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		thrower.Inventory.InventoryItems.Remove(item);

		var ret = new List<GameAction>();
		rangedAttackTargetPosition =
			game.CurrentDungeon.GetRangedAttackPosition(
				thrower,
				thrower.TilemapPosition,
				thrower.CurrentFacing,
				40,
				Dungeon.StopArrow);

		Character rangedAttackTarget = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == rangedAttackTargetPosition);

		if (rangedAttackTarget != null)
		{
			//TODO get from item
			int itemThrowDamage = 5;
			ret.Add(new TakeDamageAction(thrower, rangedAttackTarget, itemThrowDamage, true, false));
		}
		else
		{
			ret.Add(new FallToGroundAction(rangedAttackTargetPosition, item));
		}

		return ret;
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();

		var game = Game.Instance;
		var projectile = UnityEngine.Object.Instantiate(projectilePrefab, null);
		projectile.transform.position = character.VisualParent.transform.position;
		var attackerWorldPosition = game.CurrentDungeon.TileMap_Floor.CellToWorld(character.TilemapPosition);
		var offset = character.VisualParent.transform.position - attackerWorldPosition;
		var targetWorldPosition = game.CurrentDungeon.TileMap_Floor.CellToWorld(rangedAttackTargetPosition);
		projectile.transform.LookAt(targetWorldPosition + offset);
		yield return projectile.transform.DOMove(targetWorldPosition + offset, 0.5f)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);

		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		Game game = Game.Instance;

		if (!thrower.Inventory.InventoryItems.Contains(item))
		{
			return false;
		}

		return true;
	}
}