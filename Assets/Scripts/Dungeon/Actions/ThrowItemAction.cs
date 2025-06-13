using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class ThrowItemAction : GameAction
{
    private readonly Inventory inventory;
    private Character thrower;
	private InventoryItem item;
	private GameObject projectilePrefab;
	private Vector3Int rangedAttackTargetPosition;

	public ThrowItemAction(Inventory inventory, Character thrower, InventoryItem item, GameObject projectilePrefab)
	{
        this.inventory = inventory;
        this.thrower = thrower;
		this.item = item;
		this.projectilePrefab = projectilePrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		inventory.InventoryItems.Remove(item);

		var ret = new List<GameAction>();
		if (thrower.Equipment.IsEquipped(item))
		{
			var unEquipAction = new UnEquipAction(thrower, item as EquipableInventoryItem);
			ret.Add(unEquipAction);
		}

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
			if (item.ItemDefinition.ApplyToThrownTarget)
			{
				ret.AddRange(item.GetGameActions(thrower, rangedAttackTarget, inventory, item));
			}
			else
			{
				//TODO get from item
				int itemThrowDamage = 5;
				ret.Add(new TakeDamageAction(thrower, rangedAttackTarget, itemThrowDamage, true, false));
			}
		}
		else
		{
			ret.Add(new FallToGroundAction(rangedAttackTargetPosition, item));
		}

		return ret;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();

		var game = Game.Instance;
		var projectile = UnityEngine.Object.Instantiate(projectilePrefab, null);
		projectile.transform.position = character.VisualParent.transform.position;
		var attackerWorldPosition = game.CurrentDungeon.CellToWorld(character.TilemapPosition);
		var offset = new Vector3(1.25f, 1.25f, 0);
		var targetWorldPosition = game.CurrentDungeon.CellToWorld(rangedAttackTargetPosition);
		projectile.transform.LookAt(targetWorldPosition + offset);
		yield return projectile.transform.DOMove(targetWorldPosition + offset, 0.5f)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);

		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		Game game = Game.Instance;

		if (!inventory.InventoryItems.Contains(item))
		{
			return false;
		}

		return true;
	}
}