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
	public InventoryTargetSelector InventoryTargetSelector = new();
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

	internal List<InventoryItem> GetInventoryTargets(Character caster)
	{
		if (Targeting != SkillTargeting.InventoryItem || InventoryTargetSelector == null ||
			ActionEffects == null || ActionEffects.Count == 0 || ActionEffects.Any(effect => effect is not InventorySkillEffect)) return new();
		return InventoryTargetSelector.GetTargets(caster)
			.Where(item => ActionEffects.Cast<InventorySkillEffect>().All(effect => effect.CanTarget(caster, item))).ToList();
	}

	internal List<GameAction> GetInventoryEffects(Character caster, InventoryItem item) =>
		ActionEffects.Cast<InventorySkillEffect>().Select(effect => effect.Bind(caster, item)).ToList();

	internal List<Vector3Int> GetTargets(Character caster) => GetTargetCharacters(caster).Select(c => c.TilemapPosition).Distinct().ToList();

	internal List<Character> GetTargetCharacters(Character caster) =>
		Targeting == SkillTargeting.InventoryItem ? new() : TargetSelector.GetCharacters(caster);

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
	AllTargets,     // Cast immediately on every character allowed by the selector.
	InventoryItem  // Select one eligible item from the party inventory.
}
