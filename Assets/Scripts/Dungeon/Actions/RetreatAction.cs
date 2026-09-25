using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scout "Retreat": return to town keeping gold and items; the dungeon is not completed.
[Serializable]
internal class RetreatAction : GameAction, ISkillEffectPrecondition
{
	private Character caster;
	public RetreatAction() { }
	private RetreatAction(Character caster) { this.caster = caster; }

	internal override GameAction AsTargetedSkill(Character caster, Character target) => new RetreatAction(caster);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) => new RetreatAction(caster);

	public bool CanUse(Character caster, out string reason)
	{
		var game = Game.Instance;
		var common = Common.Instance;
		reason = "Cannot retreat here.";
		if (game == null || game.CurrentDungeon == null || common == null || common.GameSaveData == null) return false;
		if (common.GameSaveData.DungeonSaveData.ReturnCommitted) return false;
		if (common.CampaignContext != null && common.CampaignContext.IsSandbox) return false;
		reason = "Cannot retreat from a boss floor.";
		// Phase 4 (already merged): EnemyRank.IsBoss(Character) in Assets/Scripts/Dungeon/Game/EnemyRank.cs.
		return !game.CurrentDungeon.IsBossFloor && !game.Enemies.Any(EnemyRank.IsBoss);
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		Game.Instance.PlayerController.PendingRetreat = true;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		var who = caster ?? character;
		Game.Instance.DoFloatingText("Retreat!", Color.cyan, who.transform.position);
		yield return new WaitForSecondsRealtime(0.5f);
	}

	internal override bool IsValid(Character character) => true;
}
