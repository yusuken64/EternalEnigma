using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassCatalog", menuName = "Game/Class Catalog")]
public class ClassCatalog : ScriptableObject
{
	public const string ResourcePath = "Classes/ClassCatalog";
	public List<ClassDefinition> Classes = new();

	// Null when the asset has not been generated yet (Tools > Eternal Enigma > Classes > Create Missing Class Definitions).
	public static ClassCatalog Load() => Resources.Load<ClassCatalog>(ResourcePath);

	public ClassDefinition Get(string id)
	{
		if (string.IsNullOrEmpty(id) || Classes == null) return null;
		return Classes.FirstOrDefault(c => c != null && string.Equals(c.Id, id, StringComparison.Ordinal));
	}

	// Structural checks only. Skill-table validation (ClassKitValidator) is added once kits have skills (Phase 7).
	public IReadOnlyList<string> Validate()
	{
		var errors = new List<string>();
		if (Classes == null) { errors.Add("Class list is missing."); return errors; }
		for (int i = 0; i < Classes.Count; i++)
		{
			var definition = Classes[i];
			if (definition == null) { errors.Add($"Entry {i} is empty."); continue; }
			if (string.IsNullOrEmpty(definition.Id)) errors.Add($"'{definition.name}' has no Id.");
			if (string.IsNullOrEmpty(definition.DisplayName)) errors.Add($"'{definition.name}' has no display name.");
			if (definition.AllowedWeapons == null || definition.AllowedWeapons.Count == 0)
				errors.Add($"'{definition.name}' allows no weapons.");
		}
		foreach (var duplicate in Classes.Where(c => c != null && !string.IsNullOrEmpty(c.Id))
			.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
			errors.Add($"Duplicate class Id '{duplicate.Key}'.");
		return errors;
	}
}
