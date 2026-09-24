using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Classes;
using UnityEditor;
using UnityEngine;

public static class ClassContentBuilder
{
	public const int SpLow = 1, SpMed = 2, SpHigh = 3;
	public const string SkillsRoot = "Assets/Resources/Classes/Skills";
	public const string CatalogPath = "Assets/Resources/Classes/ClassCatalog.asset";
	public const string StatusFolder = "Assets/Prefabs/Dungeon/StatusEffects";
	public const string TemplateFolder = "Assets/Prefabs/Dungeon/Skills/SkillsData";

	public static int Cost(int tier) => tier == 1 ? 50 : tier == 2 ? 150 : 400;
	public static int MasteryCost(int tier) => tier == 1 ? 0 : tier == 2 ? 300 : 800;

	public static ClassCatalog LoadCatalog()
	{
		var catalog = AssetDatabase.LoadAssetAtPath<ClassCatalog>(CatalogPath);
		return catalog ?? throw new InvalidOperationException("Run Tools/Eternal Enigma/Classes/Create Missing Class Definitions first.");
	}

	public static string Sanitize(string skillName)
	{
		// Replace invalid characters with '-'
		var invalid = new[] { ':', '/', '\\', '?', '*', '"', '<', '>', '|' };
		var result = skillName;
		foreach (var c in invalid)
		{
			result = result.Replace(c, '-');
		}
		return result.Trim();
	}

	public static Skill Upsert(string folder, string skillName)
	{
		var dir = $"{SkillsRoot}/{folder}";
		Directory.CreateDirectory(dir);
		var path = $"{dir}/{Sanitize(skillName)}.asset";

		var skill = AssetDatabase.LoadAssetAtPath<Skill>(path);
		if (skill == null)
		{
			skill = ScriptableObject.CreateInstance<Skill>();
			AssetDatabase.CreateAsset(skill, path);
		}

		// Reset every field
		skill.name = Sanitize(skillName);
		skill.SkillName = skillName;
		skill.LearnCost = 0;
		skill.MaxRank = 1;
		skill.RankScaling = new SkillRankScaling();
		skill.ActivationType = ActivationType.Passive;
		skill.SPCost = 0;
		skill.TargetSelector = new TargetSelector { Team = TargetTeam.Self, Area = TargetArea.Self };
		skill.Targeting = SkillTargeting.Self;
		skill.AreaRadius = 0;
		skill.MissileRange = 8;
		skill.MissileProjectilePrefab = null;
		skill.ActionEffects = new List<GameAction>();
		skill.PassiveResponses = new List<PassiveResponse>();
		skill.PassiveStatModification = new StatModification();
		skill.ArrowCost = 0;
		skill.ArrowCostMode = ArrowCostMode.Fixed;
		skill.IsWeaponSkill = false;
		skill.SkillAnimation = null;
		skill.Description = "";

		EditorUtility.SetDirty(skill);
		return skill;
	}

	public static Skill LoadShared(string skillName)
	{
		var skill = AssetDatabase.LoadAssetAtPath<Skill>($"{SkillsRoot}/Shared/{Sanitize(skillName)}.asset");
		return skill ?? throw new InvalidOperationException($"Shared skill '{skillName}' missing; run ClassContentShared.Build first.");
	}

	public static StatusEffect Status(string prefabName)
	{
		var status = AssetDatabase.LoadAssetAtPath<StatusEffect>($"{StatusFolder}/{prefabName}.prefab");
		return status ?? throw new InvalidOperationException($"Status prefab {prefabName} missing; run the status prefab generators first.");
	}

	public static GameObject ArrowProjectile()
	{
		var usableItem = AssetDatabase.LoadAssetAtPath<UsableItemDefinition>("Assets/Prefabs/Dungeon/Items/Arrows_WoodenArrows.asset");
		return usableItem?.MissileProjectilePrefab ?? throw new InvalidOperationException("Arrow projectile not found.");
	}

	public static SkillAnimation Animation(string templateName)
	{
		var skill = AssetDatabase.LoadAssetAtPath<Skill>($"{TemplateFolder}/{templateName}.asset");
		return skill?.SkillAnimation ?? throw new InvalidOperationException($"Template animation '{templateName}' not found.");
	}

	public static Skill ConfigurePassive(Skill s, int maxRank, int learnCost, string description, StatModification stats, params PassiveResponse[] responses)
	{
		s.ActivationType = ActivationType.Passive;
		s.SPCost = 0;
		s.MaxRank = maxRank;
		s.LearnCost = learnCost;
		s.Description = description;
		s.PassiveStatModification = stats ?? new StatModification();
		s.PassiveResponses = responses?.Where(r => r != null).ToList() ?? new List<PassiveResponse>();

		EditorUtility.SetDirty(s);
		return s;
	}

	public static Skill ConfigureActive(Skill s, int maxRank, int learnCost, int sp, string description, SkillTargetSpec target, params GameAction[] effects)
	{
		s.ActivationType = ActivationType.Active;
		s.SPCost = sp;
		s.MaxRank = maxRank;
		s.LearnCost = learnCost;
		s.Description = description;
		s.Targeting = target.Targeting;
		s.TargetSelector = new TargetSelector { Team = target.Team, Area = target.Area };
		s.AreaRadius = target.Radius;
		s.MissileRange = target.MissileRange;
		s.MissileProjectilePrefab = target.Targeting == SkillTargeting.Missile ? ArrowProjectile() : null;
		s.ActionEffects = effects.Where(e => e != null).ToList();
		s.SkillAnimation = Animation(target.Team == TargetTeam.Allies || target.Team == TargetTeam.Self ? "Healing" : "Damage");

		EditorUtility.SetDirty(s);
		return s;
	}

	// Effect factories
	public static ScaledDamageAction Strike(float percent, int hits = 1, DamageCategory category = DamageCategory.Weapon,
		DamageElement element = DamageElement.Physical, bool rollToHit = true, DamageScaling scaling = DamageScaling.Strength,
		EquipmentRequirement requires = EquipmentRequirement.None, float executeBelow = 0f)
	{
		return new ScaledDamageAction
		{
			Scaling = scaling,
			Category = category,
			Percent = percent,
			Hits = hits,
			Element = element,
			RollToHit = rollToHit,
			Requires = requires,
			ExecuteBelowFraction = executeBelow
		};
	}

	public static ScaledDamageAction Spell(DamageElement element, int baseDamage, float perLevel, bool canEcho = false,
		float perAilment = 0f, int healPerAilment = 0)
	{
		return new ScaledDamageAction
		{
			Scaling = DamageScaling.Magic,
			Category = DamageCategory.Magic,
			Percent = 1f,
			BaseDamage = baseDamage,
			PerLevel = perLevel,
			Element = element,
			RollToHit = false,
			CanEcho = canEcho,
			PerAilmentBonus = perAilment,
			HealCasterPerAilment = healPerAilment
		};
	}

	public static ScaledHealAction Heal(int baseHeal, float perLevel)
	{
		return new ScaledHealAction
		{
			BaseHeal = baseHeal,
			PerLevel = perLevel
		};
	}

	public static ApplyStatusChanceAction Apply(string prefabName, float chance = 1f, bool ailment = false, bool onCaster = false)
	{
		return new ApplyStatusChanceAction
		{
			StatusEffect = Status(prefabName),
			Chance = chance,
			IsAilment = ailment,
			OnCaster = onCaster
		};
	}
}

// Fluent tweaks on the returned Skill (extension methods)
public static class SkillExtensions
{
	public static Skill Weapon(this Skill skill)
	{
		skill.IsWeaponSkill = true;
		EditorUtility.SetDirty(skill);
		return skill;
	}

	public static Skill Arrows(this Skill skill, int cost, ArrowCostMode mode = ArrowCostMode.Fixed)
	{
		skill.ArrowCost = cost;
		skill.ArrowCostMode = mode;
		EditorUtility.SetDirty(skill);
		return skill;
	}

	public static Skill RankStep(this Skill skill, int buffStepPerRank)
	{
		skill.RankScaling.BuffStepPerRank = buffStepPerRank;
		EditorUtility.SetDirty(skill);
		return skill;
	}
}

public readonly struct SkillTargetSpec
{
	public SkillTargetSpec(SkillTargeting targeting, TargetTeam team, TargetArea area, int radius, int missileRange)
	{
		Targeting = targeting;
		Team = team;
		Area = area;
		Radius = radius;
		MissileRange = missileRange;
	}

	public SkillTargeting Targeting { get; }
	public TargetTeam Team { get; }
	public TargetArea Area { get; }
	public int Radius { get; }
	public int MissileRange { get; }

	public static SkillTargetSpec OnSelf() => new(SkillTargeting.Self, TargetTeam.Self, TargetArea.Self, 0, 8);
	public static SkillTargetSpec AroundSelf(TargetTeam team, int radius) => new(SkillTargeting.Self, team, TargetArea.Visible, radius, 8);
	public static SkillTargetSpec Selected(TargetTeam team, TargetArea area = TargetArea.Visible, int radius = 0) => new(SkillTargeting.SelectedTarget, team, area, radius, 8);
	public static SkillTargetSpec Melee() => new(SkillTargeting.SelectedTarget, TargetTeam.Enemies, TargetArea.Melee, 0, 8);
	public static SkillTargetSpec AllVisible(TargetTeam team) => new(SkillTargeting.AllTargets, team, TargetArea.Visible, 0, 8);
	public static SkillTargetSpec Missile(int range = 8, int radius = 0, bool arrow = false) => new(SkillTargeting.Missile, TargetTeam.Enemies, TargetArea.All, radius, range);
}

public sealed class ClassKitBuilder
{
	private readonly string classId, folder;
	private readonly List<ClassSkillEntryData> entries = new();

	public ClassKitBuilder(string classId, string folder)
	{
		this.classId = classId;
		this.folder = folder;
	}

	public Skill Passive(int tier, SkillKind kind, string name, int maxRank, int learnCost, string description,
		StatModification stats = null, params PassiveResponse[] responses)
	{
		var s = ClassContentBuilder.ConfigurePassive(ClassContentBuilder.Upsert(folder, name), maxRank, learnCost, description, stats, responses);
		Add(s, tier, kind, maxRank);
		return s;
	}

	public Skill Active(int tier, string name, int maxRank, int learnCost, int sp, string description,
		SkillTargetSpec target, params GameAction[] effects)
	{
		var s = ClassContentBuilder.ConfigureActive(ClassContentBuilder.Upsert(folder, name), maxRank, learnCost, sp, description, target, effects);
		Add(s, tier, SkillKind.Normal, maxRank);
		return s;
	}

	public Skill Shared(int tier, SkillKind kind, string skillName, int maxRank)
	{
		var s = ClassContentBuilder.LoadShared(skillName);
		Add(s, tier, kind, maxRank);
		return s;
	}

	public Skill Existing(int tier, SkillKind kind, string assetPath, int maxRank)
	{
		var s = AssetDatabase.LoadAssetAtPath<Skill>(assetPath) ?? throw new InvalidOperationException("Missing " + assetPath);
		Add(s, tier, kind, maxRank);
		return s;
	}

	private void Add(Skill s, int tier, SkillKind kind, int maxRank) =>
		entries.Add(new ClassSkillEntryData { Skill = s, Tier = tier, Kind = kind, MaxRank = maxRank });

	public void Save(StatModification startingBonus)
	{
		var definition = ClassContentBuilder.LoadCatalog().Get(classId) ?? throw new InvalidOperationException("Class " + classId + " not in catalog.");
		definition.Skills = new List<ClassSkillEntryData>(entries);
		definition.StartingStatBonus = startingBonus ?? new StatModification();
		EditorUtility.SetDirty(definition);
		AssetDatabase.SaveAssets();
		foreach (var error in ClassKitValidator.Validate(definition.ToSkillTable()))
			Debug.LogError(error);
		Debug.Log($"{definition.DisplayName}: {entries.Count} skills written.");
	}
}
