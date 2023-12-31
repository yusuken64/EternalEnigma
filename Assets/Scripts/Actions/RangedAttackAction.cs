using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class RangedAttackAction : GameAction
{
	private Character attacker;
	private Character target;
	private int damage;
	private GameObject projectilePrefab;

	private Vector3Int targetPosition;

	public RangedAttackAction(Character attacker, Character target, int damage, GameObject projectilePrefab)
	{
		this.attacker = attacker;
		this.target = target;
		this.damage = damage;
		this.projectilePrefab = projectilePrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		var ret = new List<GameAction>();
		var rangedAttackTarget = game.CurrentDungeon.GetRangedAttackTarget(attacker.TilemapPosition, attacker.CurrentFacing, 40, out targetPosition);
		if (rangedAttackTarget != null)
		{
			ret.Add(new TakeDamageAction(attacker, rangedAttackTarget, damage, true, false));
		}

		return ret;
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		var game = Game.Instance;
		var projectile = UnityEngine.Object.Instantiate(projectilePrefab, null);
		projectile.transform.position = character.VisualParent.transform.position;
		var attackerWorldPosition = game.CurrentDungeon.TileMap_Floor.CellToWorld(character.TilemapPosition);
		var offset = character.VisualParent.transform.position - attackerWorldPosition;
		var targetWorldPosition = game.CurrentDungeon.TileMap_Floor.CellToWorld(targetPosition);
		projectile.transform.LookAt(targetWorldPosition + offset);
		yield return projectile.transform.DOMove(targetWorldPosition + offset, 0.5f)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}