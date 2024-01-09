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

	internal override List<GameAction> GetEffects(Player player)
	{
		var offset = Dungeon.GetFacingOffset(player.CurrentFacing);
		var newMapPosition = player.TilemapPosition + offset;
		var target = Game.Instance.AllCharacters.FirstOrDefault(x => x.TilemapPosition == newMapPosition);

		return new()
		{
			new ApplyStatusEffectAction(target, SilenceStatusEffect, player)
		};
	}

	internal override IEnumerator ExecuteRoutine(Player player)
	{
		player.VisualParent.transform.DOPunchScale(Vector3.one * 3, 1f, 50);
		yield return new WaitForSecondsRealtime(1f);
		player.PlayIdleAnimation();
	}
}
