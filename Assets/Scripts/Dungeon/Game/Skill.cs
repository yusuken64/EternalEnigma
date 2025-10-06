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
		yield return SkillAnimation.ExecuteRoutine(caster, target);
	}

	internal bool IsValid(Character caster)
	{
		return caster.Vitals.SP >= SPCost && GetTargets(caster).Any();
	}

	internal List<Vector3Int> GetTargets(Character caster) => TargetSelector.GetTargets(caster);

	public StatModification PassiveStatModification;
}

public enum ActivationType
{
	Active,
	Passive
}