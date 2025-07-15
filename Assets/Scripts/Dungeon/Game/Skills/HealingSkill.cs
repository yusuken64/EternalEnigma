using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HealingSkill : Skill
{
	public override int SPCost => 1;
	public override string SkillName => "Healing";
	public GameObject ParticlePrefab;
	private Vector3Int healTile;

	internal override IEnumerator ExecuteRoutine(Character caster)
	{
		var original = caster.VisualParent.transform.rotation;
		caster.VisualParent.transform.DORotate(new Vector3(0, 0, 360), 0.4f, RotateMode.FastBeyond360)
			.SetRelative();
		caster.PlayAttackAnimation();
		yield return new WaitForSecondsRealtime(0.5f);

		var particle = Instantiate(ParticlePrefab, this.transform);
		particle.transform.position = Game.Instance.CurrentDungeon.CellToWorld(healTile);
		Destroy(particle.gameObject, 1f);

		yield return new WaitForSecondsRealtime(0.3f);
		caster.PlayIdleAnimation();
		caster.VisualParent.transform.rotation = original;
	}

	internal override List<GameAction> GetEffects(Character caster, Vector3Int targetPosition)
	{
		var target = Game.Instance.CurrentDungeon.GetCharacterAtPosition(targetPosition);
		if (target == null) { return new(); }

		healTile = targetPosition;
		var healAction = new TakeHealAction(caster, target, 10, true, false);

		return new()
		{
			healAction
		};
	}

	internal override List<Vector3Int> GetTargets(Character caster)
	{
		BoundsInt visionBounds = Game.Instance.CurrentDungeon.GetVisionBounds(caster, caster.TilemapPosition);
		return Game.Instance.AllCharacters
			.Where(x => x.Team == caster.Team)
			.Where(x => visionBounds.Contains(x.TilemapPosition))
			.Select(x => x.TilemapPosition).ToList();
	}
}
