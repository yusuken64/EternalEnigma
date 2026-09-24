using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
	public List<Skill> SkillPrefabs;

	internal Skill GetSkillByName(string skillName)
	{
		return SkillPrefabs.FirstOrDefault(x => x.SkillName == skillName) ??
			FindClassSkill(skillName) ??
			DemoDungeonLoadout.Load()?.Skills.FirstOrDefault(x => x.SkillName == skillName);
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
