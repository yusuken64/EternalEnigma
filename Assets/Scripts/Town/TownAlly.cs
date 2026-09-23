using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalEnigma.Core.Classes;

public class TownAlly : TownCharacter
{
	public string Name;
	public string Id;
	public string Description;

	public GameObject AnimatedModel;

	public List<string> Skills;
	// Parallel to Skills; a learned skill without an entry is rank 1.
	public List<SkillRankSaveData> SkillRanks = new();
	// Highest dungeon level reached; the trainer's level gate.
	[Min(1)] public int HighestLevel = 1;

	// Fixed per hero prefab (assigned in Phase 7). The protagonist's instance is overwritten from the save.
	public ClassDefinition PrimaryClass;
	public ClassDefinition SecondaryClass;
	public int RecruitCost { get; internal set; }

	// 0 when the skill is not learned.
	public int GetRank(string skillName)
	{
		if (string.IsNullOrEmpty(skillName) || Skills == null || !Skills.Contains(skillName)) return 0;
		var entry = SkillRanks?.FirstOrDefault(r => r != null && r.SkillName == skillName);
		return entry == null ? 1 : Mathf.Max(1, entry.Rank);
	}

	// rank <= 0 forgets the skill; otherwise learns it (if needed) at exactly that rank.
	public void SetRank(string skillName, int rank)
	{
		if (string.IsNullOrEmpty(skillName)) return;
		Skills ??= new();
		SkillRanks ??= new();
		SkillRanks.RemoveAll(r => r == null || r.SkillName == skillName);
		if (rank <= 0)
		{
			Skills.Remove(skillName);
			return;
		}
		if (!Skills.Contains(skillName)) Skills.Add(skillName);
		SkillRanks.Add(new SkillRankSaveData { SkillName = skillName, Rank = rank });
	}

	public IReadOnlyList<LearnedSkill> ToLearnedSkills() =>
		(Skills ?? new List<string>()).Where(s => !string.IsNullOrEmpty(s)).Distinct()
			.Select(s => new LearnedSkill(s, GetRank(s))).ToList();

	// Primary class tier 1 mastery ids (Novice Training); empty without a class.
	public static IReadOnlyList<string> StartingSkillNames(ClassDefinition primary, ClassDefinition secondary)
	{
		if (primary == null) return new List<string>();
		return SkillLearningRules.StartingSkills(HeroClass.ToKit(primary, secondary));
	}

	// Learns any missing starting skill at rank 1. Safe to call repeatedly.
	public void EnsureStartingSkills()
	{
		foreach (var name in StartingSkillNames(PrimaryClass, SecondaryClass))
			if (GetRank(name) == 0) SetRank(name, 1);
	}
	private void Awake()
	{
		if (HeroAnimator?.Animator != null) HeroAnimator.Animator.applyRootMotion = false;
		Skills ??= new();
		SkillRanks ??= new();
		if (HighestLevel < 1) HighestLevel = 1;
	}
	public void RefreshEquipmentVisuals() => HeroAnimator?.SetWeapon(
		Equipment.EquippedWeapon?.EquipmentItemDefinition, Equipment.EquippedShield?.EquipmentItemDefinition);

	public SpriteRenderer CirlcleRenderer;
	public Color AllyColor;
	public Color PlayerColor;
	internal void SetToCPU()
	{
		CirlcleRenderer.color = AllyColor;
	}

	internal void SetToPlayer()
	{
		CirlcleRenderer.color = PlayerColor;
	}
}
