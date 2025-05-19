using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DamageSkill : Skill
{
    public override int SPCost => 1;
    public override string SkillName => "Damage";
	public GameObject ParticlePrefab;
	private List<Vector3Int> _attackTiles;

	internal override IEnumerator ExecuteRoutine(Character caster)
	{
		var original = caster.VisualParent.transform.rotation;
		caster.VisualParent.transform.DORotate(new Vector3(0, 0, 360), 0.4f, RotateMode.FastBeyond360)
			.SetRelative();
		caster.PlayAttackAnimation();
		yield return new WaitForSecondsRealtime(0.5f);
		foreach (var attackTile in _attackTiles)
		{
			var particle = Instantiate(ParticlePrefab, this.transform);
			particle.transform.position = Game.Instance.CurrentDungeon.CellToWorld(attackTile);
			Destroy(particle.gameObject, 1f);
			yield return new WaitForSecondsRealtime(0.1f);
		}
		yield return new WaitForSecondsRealtime(0.3f);
		caster.PlayIdleAnimation();
		caster.VisualParent.transform.rotation = original;
	}

    internal override List<GameAction> GetEffects(Character caster, Vector3Int target)
    {
		var offset = Dungeon.GetFacingOffset(caster.CurrentFacing);
		Vector3Int tilemapPosition = caster.TilemapPosition;
		var attackPosition = tilemapPosition + offset;
		var neighborHoodTiles = Game.Instance.CurrentDungeon.GetWalkableNeighborhoodTiles(attackPosition);
		_attackTiles = new()
		{
			target
		};

		var enemies = _attackTiles.Select(tile => Game.Instance.CurrentDungeon.GetCharacterAtPosition(tile)).ToList();

		var takeDamageActions = _attackTiles
			.Select(tile => Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == tile))
			.Where(x => x != null)
			.Select(enemy =>
			{
				AttackAction.GetAttackDamage(caster, enemy, out bool hit, out int damage);
				return new TakeDamageAction(caster, enemy, damage, true, !hit);
			});

		return takeDamageActions
			.Cast<GameAction>()
			.ToList();
	}
}
