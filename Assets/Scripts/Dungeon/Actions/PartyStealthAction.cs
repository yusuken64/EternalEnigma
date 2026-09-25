using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scout "Safe Passage": party stealth for BaseTurns (rank-scaled) or until anyone attacks.
[Serializable]
internal class PartyStealthAction : GameAction
{
	public int BaseTurns = 5;
	private Character caster;
	private int turns;

	private static SafePassageStatusEffect template;

	public PartyStealthAction() { }
	private PartyStealthAction(Character caster, int baseTurns, int turns) { this.caster = caster; BaseTurns = baseTurns; this.turns = turns; }

	internal override GameAction AsTargetedSkill(Character caster, Character target) =>
		new PartyStealthAction(caster, BaseTurns, BaseTurns);
	internal override GameAction AsTargetedSkill(Character caster, Character target, SkillRankContext rank) =>
		new PartyStealthAction(caster, BaseTurns, rank.Scaling.ScaleDuration(BaseTurns, rank.Rank));

	private static SafePassageStatusEffect Template()
	{
		if (template != null) return template;
		var go = new GameObject("SafePassageTemplate");
		go.SetActive(false);
		go.hideFlags = HideFlags.HideAndDontSave;
		template = go.AddComponent<SafePassageStatusEffect>();
		return template;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		int duration = turns > 0 ? turns : BaseTurns;
		var prefab = Template();
		foreach (var ally in Game.Instance.Allies.Where(a => a != null && a.Vitals.HP > 0))
		{
			var existing = ally.StatusEffects.OfType<SafePassageStatusEffect>().FirstOrDefault();
			if (existing != null) { existing.TurnsLeft = Mathf.Max(existing.TurnsLeft, duration); continue; }
			prefab.TurnsLeft = duration;
			ally.ApplyStatusEffect(prefab);
		}
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		var who = caster ?? character;
		Game.Instance.DoFloatingText("Safe Passage", Color.cyan, who.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => true;
}
