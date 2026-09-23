using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using UnityEngine;

[CreateAssetMenu(fileName = "Class", menuName = "Game/Class")]
public class ClassDefinition : ScriptableObject
{
	public string Id;
	public string DisplayName;
	[TextArea] public string Role;
	public List<WeaponType> AllowedWeapons = new();
	public StatModification GrowthPerLevel = new();
	public StatModification StartingStatBonus = new();
	public List<ClassSkillEntryData> Skills = new();

	// Converts to the Core rules form. Entries with no Skill are skipped.
	public ClassSkillTable ToSkillTable() =>
		new ClassSkillTable(Id, (Skills ?? new List<ClassSkillEntryData>())
			.Where(entry => entry != null && entry.Skill != null)
			.Select(entry => new ClassSkillEntry(entry.Skill.SkillName, entry.Tier, entry.MaxRank, entry.Kind)));

	public bool AllowsWeapon(WeaponType type) => AllowedWeapons != null && AllowedWeapons.Contains(type);
}
