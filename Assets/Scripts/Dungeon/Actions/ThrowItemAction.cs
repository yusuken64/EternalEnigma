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
	private Vector3Int sourcePosition;
	internal Vector3 ProjectileStart => CellCenter(Game.Instance.CurrentDungeon, sourcePosition);
	internal Vector3 ProjectileEnd => CellCenter(Game.Instance.CurrentDungeon, rangedAttackTargetPosition);
	internal static Vector3 CellCenter(TileWorldDungeon dungeon, Vector3Int cell) =>
		dungeon.CellToWorld(cell) + (dungeon.CellToWorld(new Vector3Int(1, 1, 0)) - dungeon.CellToWorld(Vector3Int.zero)) * .5f;

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
		sourcePosition = thrower.TilemapPosition;
        QueueSound(SoundCue.Throw);
		GameMessages.ForCharacter(thrower, $"{GameMessages.Name(thrower)} threw {item.DisplayName}.");

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
				if (hit && damage > 0 && !AutoplayRunner.InfiniteStrengthFor(thrower))
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
		float gesture = thrower.PlayItemGesture(true);
		yield return new WaitForSecondsRealtime(gesture * .45f);
		GameObject projectile = null;
		try
		{
			if (projectilePrefab != null)
			{
				projectile = UnityEngine.Object.Instantiate(projectilePrefab, ProjectileStart, Quaternion.identity);
				if (LookAt && (ProjectileEnd - ProjectileStart).sqrMagnitude > .001f) projectile.transform.LookAt(ProjectileEnd, Vector3.back);
				float duration = Vector3.Distance(ProjectileStart, ProjectileEnd) / ProjectileSpeed;
				yield return projectile.transform.DOMove(ProjectileEnd, duration).SetEase(Ease.Linear).SetUpdate(true).WaitForCompletion();
			}
			yield return new WaitForSecondsRealtime(gesture * .55f);
		}
		finally
		{
			if (projectile != null) { projectile.transform.DOKill(); UnityEngine.Object.Destroy(projectile); }
			if (thrower != null) thrower.PlayIdleAnimation();
		}
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
