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
	public Sprite Portrait;

	public List<string> Skills;
	// Parallel to Skills; a learned skill without an entry is rank 1.
	public List<SkillRankSaveData> SkillRanks = new();
	// Highest dungeon level reached; the trainer's level gate.
	[Min(1)] public int HighestLevel = 1;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Level")] private int level = 1;
    public int Level { get => level; set { if (level == value) return; level = value; HudRefresh.PartyChanged(); } }
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Experience")] private int experience;
    public int Experience { get => experience; set { if (experience == value) return; experience = value; HudRefresh.PartyChanged(); } }
	public AttributePoints Attributes;
	public int PendingAttributePoints => HeroAttributes.Pending(Level, Attributes);
	// Vitals carried between dungeon runs; -1 means full.
	[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Hp")] private int hp = -1;
    public int Hp { get => hp; set { if (hp == value) return; hp = value; HudRefresh.PartyChanged(); } }
	[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Mana")] private int mana = -1;
    public int Mana { get => mana; set { if (mana == value) return; mana = value; HudRefresh.PartyChanged(); } }
    // Explicit presence flag distinguishes old saves from genuine zero hunger.
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("HasHunger")] private bool hasHunger;
    public bool HasHunger { get => hasHunger; set { if (hasHunger == value) return; hasHunger = value; HudRefresh.PartyChanged(); } }
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("Hunger")] private int hunger;
    public int Hunger { get => hunger; set { if (hunger == value) return; hunger = value; HudRefresh.PartyChanged(); } }
    public int HungerAccumulate;

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
        HudRefresh.PartyChanged();
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
		DungeonClearAbilities.Apply(this);
		foreach (var name in StartingSkillNames(PrimaryClass, SecondaryClass))
			if (GetRank(name) == 0) SetRank(name, 1);
	}
	// Respec: drops every learned skill except the free starting ones. Skill points are derived from ranks, so this refunds them all.
	public void ForgetAllSkills()
	{
		Skills = new();
		SkillRanks = new();
		EnsureStartingSkills();
	}

	private void Awake()
	{
        SelectionRing.Apply(CirlcleRenderer);
        AllyColor=new Color(.23f,.69f,.58f,.9f);PlayerColor=new Color(.12f,.87f,.76f,.95f);
        SilhouetteParticipant.Register(transform,SilhouetteRole.Caster,true);
		if (HeroAnimator?.Animator != null) HeroAnimator.Animator.applyRootMotion = false;
		Skills ??= new();
		SkillRanks ??= new();
		if (HighestLevel < 1) HighestLevel = 1;
	}
    private void OnEnable() => HudRefresh.PartyChanged();
    private void OnDisable() => HudRefresh.PartyChanged();
	public void RefreshEquipmentVisuals()
    {
        HudRefresh.PartyChanged();
        HeroAnimator?.SetWeapon(Equipment.EquippedWeapon?.EquipmentItemDefinition, Equipment.EquippedShield?.EquipmentItemDefinition);
        SilhouetteParticipant.Register(transform,SilhouetteRole.Caster,true);
    }

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
