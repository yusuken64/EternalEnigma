using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
	public List<Skill> SkillPrefabs;
	private Dictionary<string, Skill> authoredSkills;

	internal Skill GetSkillByName(string skillName)
	{
		if (string.IsNullOrEmpty(skillName)) return null;
		if (!GameContent.Ready) authoredSkills = null;
		else if (authoredSkills == null && Application.isPlaying)
		{
			var catalog = ClassCatalog.Load();
			if (catalog?.Classes != null)
			{
				authoredSkills = new Dictionary<string, Skill>(StringComparer.Ordinal);
				foreach (var skill in SkillPrefabs) Index(skill);
				foreach (var heroClass in catalog.Classes)
					if (heroClass != null && heroClass.Skills != null)
						foreach (var entry in heroClass.Skills) Index(entry?.Skill);
			}
		}

		Skill authored;
		if (authoredSkills != null) authoredSkills.TryGetValue(skillName, out authored);
		else authored = SkillPrefabs.FirstOrDefault(x => x != null && x.SkillName == skillName) ?? FindClassSkill(skillName);
		if (authored != null) return authored;
		// Clear rewards depend on the active campaign. Resolve them against its current progress.
		return DungeonClearAbilities.Find(skillName) ??
			DemoDungeonLoadout.Load()?.Skills.FirstOrDefault(x => x.SkillName == skillName);
	}

	private void Index(Skill skill)
	{
		// The prefab list takes precedence, followed by class entries in catalog order.
		if (skill != null && !string.IsNullOrEmpty(skill.SkillName)) authoredSkills.TryAdd(skill.SkillName, skill);
	}

	// Class skills live in ClassCatalog (Resources/Classes) rather than in SkillPrefabs.
	internal static Skill FindClassSkill(string skillName)
	{
		var catalog = ClassCatalog.Load();
		if (catalog == null || catalog.Classes == null || string.IsNullOrEmpty(skillName)) return null;
		return catalog.Classes.Where(c => c != null && c.Skills != null)
			.SelectMany(c => c.Skills)
			.Select(e => e?.Skill)
			.FirstOrDefault(s => s != null && s.SkillName == skillName);
	}
	internal Skill GetSkillInstanceByName(string skillName)
	{
		return Instantiate(GetSkillByName(skillName));
	}
}
