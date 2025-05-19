using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngerSkill : Skill
{
	public override int SPCost => 1;
	public override string SkillName => "Anger";
	public StatusEffect StrengthStatusEffect;

	internal override List<GameAction> GetEffects(Character caster, Vector3Int target)
    {
		return new()
		{
			new ApplyStatusEffectAction(caster, StrengthStatusEffect, caster)
		};
	}

	internal override IEnumerator ExecuteRoutine(Character caster)
	{
		caster.VisualParent.transform.DOPunchScale(Vector3.one * 3, 1f, 50);
		yield return new WaitForSecondsRealtime(1f);
		caster.PlayIdleAnimation();
	}
}
