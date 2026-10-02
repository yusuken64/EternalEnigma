using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class ThrowItemAction : GameAction
{
	private const float ProjectileSpeed = 20f; // world units per second, same as RangedAttackAction
    private readonly Inventory inventory;
    private Character thrower;
	private InventoryItem item;
	private GameObject projectilePrefab;
	private Vector3Int rangedAttackTargetPosition;

	public ThrowItemAction()
	{

	}
	public ThrowItemAction(Inventory inventory, Character thrower, InventoryItem item, GameObject projectilePrefab)
	{
        this.inventory = inventory;
        this.thrower = thrower;
		this.item = item;
		this.projectilePrefab = projectilePrefab;
	}

	public bool LookAt { get; set; }

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		GameMessages.ForCharacter(thrower, $"{GameMessages.Name(thrower)} threw {item.ItemName}.");

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

		// Allies are passed over by the projectile, and a wall right in front leaves the landing cell on the thrower.
		Character rangedAttackTarget = game.AllCharacters.FirstOrDefault(x =>
			x != thrower && x.Team != thrower.Team && x.TilemapPosition == rangedAttackTargetPosition);

		var weapon = item is EquipableInventoryItem equipable &&
			(equipable.EquipmentSlot == EquipmentSlot.MainHand || equipable.EquipmentSlot == EquipmentSlot.TwoHand)
			? equipable : null;

		if (rangedAttackTarget != null)
		{
			if (item.ItemDefinition.ApplyToThrownTarget)
			{
				ret.AddRange(item.GetGameActions(thrower, rangedAttackTarget, inventory, item));
			}
			else if (weapon != null)
			{
				// A thrown weapon lands like a swing of that weapon, whether or not it was the one equipped.
				AttackAction.GetAttackDamage(thrower, rangedAttackTarget, WeaponStrength(weapon), out bool hit, out int damage, out bool critical);
				if (hit && damage > 0 && !AutoplayRunner.GodmodeFor(thrower))
					damage = Mathf.Max(1, Mathf.RoundToInt(damage * ClassPassives.DamageMultiplier(
						new OutgoingDamage(thrower, rangedAttackTarget, DamageCategory.Weapon, DamageElement.Physical, false))));
				ret.Add(new TakeDamageAction(thrower, rangedAttackTarget, damage, true, !hit) { Critical = critical });
			}
			else
			{
				//TODO get from item
				int itemThrowDamage = Mathf.RoundToInt(5 * ClassPassives.ThrowDamageMultiplier(thrower));
				ret.Add(new TakeDamageAction(thrower, rangedAttackTarget, itemThrowDamage, true, false));
			}
		}
		else
		{
			if (game.CurrentDungeon.PropAt(rangedAttackTargetPosition) is DungeonProp prop && prop.Alive)
			{
				int propDamage = weapon != null
					? Mathf.Max(1, Mathf.FloorToInt(WeaponStrength(weapon) * UnityEngine.Random.Range(112, 143) / 128f))
					: Mathf.RoundToInt(5 * ClassPassives.ThrowDamageMultiplier(thrower));
				ret.Add(prop.Damage(thrower, propDamage));
			}
			ret.Add(new FallToGroundAction(rangedAttackTargetPosition, item));
		}

		inventory.InventoryItems.Remove(item);
		return ret;
	}

	// Thrower's strength with the thrown weapon's bonus standing in for whatever weapon is currently equipped.
	private int WeaponStrength(EquipableInventoryItem weapon)
	{
		int strength = thrower.FinalStats.Strength;
		var equipped = thrower.Equipment.EquippedWeapon;
		if (equipped != null && equipped != weapon) strength -= equipped.GetEquipmentStatModification().Strength;
		if (equipped != weapon) strength += weapon.GetEquipmentStatModification().Strength;
		return strength;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		yield return character.VisualParent.transform.DOPunchScale(Vector3.one * 2, 0.2f)
			.WaitForCompletion();
		if (projectilePrefab == null) yield break;

		var game = Game.Instance;
		var projectile = UnityEngine.Object.Instantiate(projectilePrefab, null);
		Vector3 startPosition = character.VisualParent.transform.position;
		projectile.transform.position = startPosition;
		var targetWorldPosition = game.CurrentDungeon.CellToWorld(rangedAttackTargetPosition);

		Vector3 offset = Vector3.zero;
		if (LookAt)
		{
			offset = new Vector3(1.25f, 1.25f, 0);
			projectile.transform.LookAt(targetWorldPosition + offset);
		}
		float duration = Vector3.Distance(startPosition, targetWorldPosition + offset) / ProjectileSpeed;
		yield return projectile.transform.DOMove(targetWorldPosition + offset, duration)
			.SetEase(Ease.Linear)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);

		yield return null;
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        foreach (var cell in AnimationPath(actor, rangedAttackTargetPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return inventory.InventoryItems.Contains(item);
	}
}