using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WideAttackSkill : Skill
{
	public override int SPCost => 1;
	public override string SkillName => "Wide Attack";
	public GameObject ParticlePrefab;
	private IEnumerable<Vector3Int> _attackTiles;

	internal override List<GameAction> GetEffects(Player player)
	{
		var offset = Dungeon.GetFacingOffset(player.CurrentFacing);
		Vector3Int tilemapPosition = player.TilemapPosition;
		var attackPosition = tilemapPosition + offset;
		var neighborHoodTiles = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(attackPosition);
		_attackTiles = neighborHoodTiles.Where(x =>
		{
			var chebyshevDistance = Mathf.Max(Mathf.Abs(x.x - tilemapPosition.x), Mathf.Abs(x.y - tilemapPosition.y));
			return chebyshevDistance <= 1 && chebyshevDistance > 0;
		});

		var what = _attackTiles.ToList();
		var enemies = _attackTiles.Select(tile => Game.Instance.CurrentDungeon.GetEnemyAtPosition(tile)).ToList();

		var takeDamageActions = _attackTiles
			.Select(tile => Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == tile))
			.Where(x => x != null)
			.Select(enemy =>
			{
				AttackAction.GetAttackDamage(player, enemy, out bool hit, out int damage);
				return new TakeDamageAction(player, enemy, damage, true, !hit);
			});

		return takeDamageActions
			.Cast<GameAction>()
			.ToList();
	}

	internal override IEnumerator ExecuteRoutine(Player player)
	{
		var original = player.VisualParent.transform.rotation;
		player.VisualParent.transform.DORotate(new Vector3(0, 0, 360), 0.4f, RotateMode.FastBeyond360)
			.SetRelative();
		player.PlayAttackAnimation();
		yield return new WaitForSecondsRealtime(0.5f);
		foreach (var attackTile in _attackTiles)
		{
			var particle = Instantiate(ParticlePrefab, this.transform);
			particle.transform.position = Game.Instance.CurrentDungeon.CellToWorld(attackTile);
			Destroy(particle.gameObject, 1f);
			yield return new WaitForSecondsRealtime(0.1f);
		}
		yield return new WaitForSecondsRealtime(0.3f);
		player.PlayIdleAnimation();
		player.VisualParent.transform.rotation = original;
	}
}
