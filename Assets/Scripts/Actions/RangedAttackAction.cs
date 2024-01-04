using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class RangedAttackAction : GameAction
{
	private Character attacker;
	private Character target;
	private int damage;
	private GameObject projectilePrefab;
	private Vector3Int rangedAttackTargetPosition;

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
		rangedAttackTargetPosition = 
			game.CurrentDungeon.GetRangedAttackPosition(
				attacker,
				attacker.TilemapPosition,
				attacker.CurrentFacing,
				40,
				Dungeon.StopArrow);
		
		Character rangedAttackTarget = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == rangedAttackTargetPosition);

		if (rangedAttackTarget != null)
		{
			ret.Add(new TakeDamageAction(attacker, rangedAttackTarget, damage, true, false));
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
		var offset = new Vector3(1.25f, 1.25f, 0);
		var targetWorldPosition = game.CurrentDungeon.TileMap_Floor.CellToWorld(rangedAttackTargetPosition) + offset;
		projectile.transform.LookAt(targetWorldPosition);
		yield return projectile.transform.DOMove(targetWorldPosition, 0.5f)
			.WaitForCompletion();

		UnityEngine.Object.Destroy(projectile.gameObject);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}