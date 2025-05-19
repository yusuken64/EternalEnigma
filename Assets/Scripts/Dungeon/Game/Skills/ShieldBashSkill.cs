using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShieldBashSkill : Skill
{
	public override int SPCost => 2;
	public override string SkillName => "Shield Bash";
	public StatusEffect SilenceStatusEffect;

	internal override List<GameAction> GetEffects(Character caster, Vector3Int target)
    {
		var offset = Dungeon.GetFacingOffset(caster.CurrentFacing);
		var newMapPosition = caster.TilemapPosition + offset;
		var target2 = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == newMapPosition);

		return new()
		{
			new ApplyStatusEffectAction(target2, SilenceStatusEffect, caster)
		};
	}

	internal override IEnumerator ExecuteRoutine(Character caster)
	{
		caster.VisualParent.transform.DOPunchScale(Vector3.one * 3, 1f, 50);
		yield return new WaitForSecondsRealtime(1f);
		caster.PlayIdleAnimation();
	}
}
