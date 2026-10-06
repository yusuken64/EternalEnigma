using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ReviveScope { Adjacent, Visible }

// Skill effect. Author on a skill with Targeting = Self. Revives downed allies around the caster.
[Serializable]
public class ReviveAction : GameAction, ISkillCastCondition
{
	public ReviveScope Scope = ReviveScope.Adjacent;
	[Range(0f, 1f)] public float HpFraction = 0.25f;

	private Character caster;
    private Ally selected;
	private float fraction;
	private readonly List<Ally> revived = new();

	public ReviveAction() { }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		AsTargetedSkill(caster, target, SkillRankContext.Unranked);

	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new ReviveAction
		{
			Scope = Scope, HpFraction = HpFraction, caster = caster, selected = target is Ally ally && ally.Vitals.HP <= 0 ? ally : null,
			fraction = Mathf.Clamp01(rank.Scaling.ScaleChance(HpFraction, rank.Rank)),
		};

	internal IEnumerable<Ally> Targets(Character from)
	{
		var game = Game.Instance;
		if (game == null || from == null || game.DownedAllies == null) return Enumerable.Empty<Ally>();
		return game.DownedAllies.Where(a => a != null && a.Team == from.Team && (Scope == ReviveScope.Adjacent
			? TileWorldDungeon.ChevDistance(a.TilemapPosition, from.TilemapPosition) <= 1
			: game.CurrentDungeon.CanSee(from, a))).ToList();
	}

	public bool CanCast(Character caster, out string reason)
	{
		bool any = Targets(caster).Any();
		reason = any ? "" : "No downed ally in range";
		return any;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;
		revived.Clear();
		foreach (var ally in selected != null ? new[] { selected }.Where(a => a.Team == caster.Team && game.DownedAllies.Contains(a)) : Targets(caster))
		{
			int hp = Mathf.Max(1, Mathf.RoundToInt(ally.FinalStats.HPMax * fraction));
			PartyRules.MarkStanding(game, ally);
			TrackAnimationTarget(ally);
			AddMetricsModification(ally, (stats, vitals) => vitals.HP = hp);
			revived.Add(ally);
		}
		return new();
	}

    internal override void RecordOutcome(Character character) { foreach (var ally in revived) GameMessages.ForCharacter(ally, $"{GameMessages.Name(ally)} was revived."); }

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		foreach (var ally in revived)
		{
			if (ally == null) continue;
			ally.PlayIdleAnimation();

		}
		if (!skipAnimation && revived.Count > 0) yield return new WaitForSecondsRealtime(0.3f);
	}

	internal override bool IsValid(Character character) => true;
}
