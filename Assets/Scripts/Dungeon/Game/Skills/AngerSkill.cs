using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngerSkill : Skill
{
	public override int SPCost => 1;
	public override string SkillName => "Anger";
	public StatusEffect StrengthStatusEffect;

	internal override List<GameAction> GetEffects(Player player)
	{
		return new()
		{
			new ApplyStatusEffectAction(player, StrengthStatusEffect, player)
		};
	}

	internal override IEnumerator ExecuteRoutine(Player player)
	{
		player.VisualParent.transform.DOPunchScale(Vector3.one * 3, 1f, 50);
		yield return new WaitForSecondsRealtime(1f);
		player.PlayIdleAnimation();
	}
}
