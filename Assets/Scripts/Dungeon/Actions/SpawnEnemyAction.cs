using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class SpawnEnemyAction : GameAction
{
	private Vector3 spawnSourceWorldLocation;
	private Enemy enemyPrefab;
	private Vector3Int spawnTilePosition;

	private Enemy _spawnedEnemy;

	//spawned enemy will lerp from spawnsourcecworldlocation to the spawnedtile
	public SpawnEnemyAction(Enemy enemyPrefab, Vector3Int spawnTilePosition, Vector3 spawnSourceWorldLocation)
	{
		this.enemyPrefab = enemyPrefab;
		this.spawnTilePosition = spawnTilePosition;
		this.spawnSourceWorldLocation = spawnSourceWorldLocation;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		_spawnedEnemy = UnityEngine.Object.Instantiate(enemyPrefab, Game.Instance.transform);
		_spawnedEnemy.UpdateCachedStats();
		_spawnedEnemy.InitialzeVitalsFromStats();
		_spawnedEnemy.TilemapPosition = spawnTilePosition;
		_spawnedEnemy.VisualParent.gameObject.SetActive(false);
		_spawnedEnemy.VisualParent.transform.localScale = Vector3.zero;
		_spawnedEnemy.VisualParent.transform.position = spawnSourceWorldLocation;
		Game.Instance.Enemies.Add(_spawnedEnemy);

		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		_spawnedEnemy.VisualParent.gameObject.SetActive(true);
		_spawnedEnemy.VisualParent.transform.DOScale(Vector3.one * 2, 0.1f);
		var endValue = Game.Instance.CurrentDungeon.CellToWorld(spawnTilePosition) + new Vector3(1.25f, 1.25f, 0);
		var moveTween = _spawnedEnemy.VisualParent.transform.DOMove(endValue, 0.5f);
		yield return moveTween.WaitForCompletion();
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}