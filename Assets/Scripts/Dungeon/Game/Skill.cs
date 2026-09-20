using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Game/SkilllData")]
public class Skill : ScriptableObject
{
	public string SkillName;
	public int LearnCost;
	public ActivationType ActivationType;

	public int SPCost;
	public TargetSelector TargetSelector;
	public SkillTargeting Targeting;
	[Min(0)] public int AreaRadius;
	internal bool RequiresTargetSelection => Targeting == SkillTargeting.SelectedTarget &&
		TargetSelector.Team != TargetTeam.Self && TargetSelector.Area != TargetArea.Self;
	[SerializeReference]
	public List<GameAction> ActionEffects;

	public SkillAnimation SkillAnimation;

	private void OnEnable()
	{
		if (string.IsNullOrEmpty(SkillName)) SkillName = name;
		if (ActionEffects == null) ActionEffects = new();
	}

	internal List<GameAction> GetEffects(Character caster, Character target)
	{
		return 
			ActionEffects.Select(x => x.AsTargetedSkill(caster, target))
			.ToList();
	}

	internal IEnumerator ExecuteRoutine(Character caster, Character target)
	{
		if (SkillAnimation != null) yield return SkillAnimation.ExecuteRoutine(caster, target);
	}

	internal bool IsValid(Character caster)
	{
		return caster != null && caster.CanCast(this, out _);
	}

	internal List<Vector3Int> GetTargets(Character caster) => TargetSelector.GetTargets(caster);

	internal List<Character> GetTargetCharacters(Character caster) => TargetSelector.GetCharacters(caster);

	internal List<Character> GetAffectedCharacters(Character caster, Character selectedTarget)
	{
		var candidates = GetTargetCharacters(caster);
		if (Targeting == SkillTargeting.AllTargets) return candidates;
		var center = RequiresTargetSelection ? selectedTarget : caster;
		if (center == null || (RequiresTargetSelection && !candidates.Contains(center))) return new();
		return candidates.Where(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, center.TilemapPosition) <= AreaRadius).ToList();
	}

	public StatModification PassiveStatModification;

	[TextArea]
	public string Description;
}

public enum ActivationType
{
	Active,
	Passive
}

public enum SkillTargeting
{
	SelectedTarget, // Radius zero affects only the selected character.
	Self,           // Cast immediately, centered on the caster.
	AllTargets      // Cast immediately on every character allowed by the selector.
}
