using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Skill effect for Dominate. Author on a Missile skill targeting Enemies.
// Only a Feared, non-boss enemy that is not already a summon is dominated; otherwise it resists.
[Serializable]
public class DominateAction : GameAction
{
	public int Turns = 3;

	private Character caster;
	private Character target;
	private int turns;
	private bool dominated;

	public DominateAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new DominateAction { Turns = Turns, caster = caster, target = target,
			turns = rank.Scaling.ScaleDuration(Turns, rank.Rank) };

	internal static bool CanDominate(Character target) =>
		target is Enemy && target.Vitals != null && target.Vitals.HP > 0 && !EnemyRank.IsBoss(target) &&
		target.GetComponent<SummonedUnit>() == null &&
		target.StatusEffects.Any(x => x is FearStatusEffect && !x.IsExpired());

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		dominated = false;
		if (caster == null || !CanDominate(target)) return new();
		TrackAnimationTarget(target);
		foreach (var fear in target.StatusEffects.Where(x => x is FearStatusEffect).ToList())
		{
			target.RemoveStatusEffect(fear);
			UnityEngine.Object.Destroy(fear.gameObject);
		}
		var unit = target.gameObject.AddComponent<SummonedUnit>();
		unit.Kind = SummonKind.Dominated;
		unit.Summoner = caster;
		unit.TurnsLeft = Math.Max(1, turns);
		unit.OriginalTeam = target.Team;
		unit.Order = SummonedUnit.NextOrder();
		target.Team = caster.Team;
		target.PursuitTarget = null;
		target.PursuitPosition = null;
		dominated = true;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || target == null) yield break;
		Game.Instance.DoFloatingText(dominated ? "Dominated!" : "Resisted", dominated ? Color.magenta : Color.white,
			target.VisualParent.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
