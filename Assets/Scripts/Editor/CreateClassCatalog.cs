using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates any missing class assets. Never overwrites fields on existing assets.
public static class CreateClassCatalog
{
	private const string Folder = "Assets/Resources/Classes";
	private const string DefinitionsFolder = Folder + "/Definitions";
	private const string CatalogPath = Folder + "/ClassCatalog.asset";

	private sealed class Spec
	{
		public string Id, Name, Role;
		public WeaponType[] Weapons;
		public int HP, SP, Strength, Defense, Hunger;
	}

	private static readonly Spec[] Specs = {
		new Spec { Id = "warrior", Name = "Warrior", Role = "Front-line physical attacker", Weapons = new[] { WeaponType.SingleSword, WeaponType.TwoHandSword, WeaponType.OffhandSword }, HP = 5, SP = 0, Strength = 3, Defense = 0, Hunger = 0 },
		new Spec { Id = "guardian", Name = "Guardian", Role = "Tank / defender", Weapons = new[] { WeaponType.SingleSword, WeaponType.Spear, WeaponType.OffhandShield }, HP = 7, SP = 0, Strength = 1, Defense = 1, Hunger = 0 },
		new Spec { Id = "archer", Name = "Archer", Role = "Ranged damage dealer", Weapons = new[] { WeaponType.BowAndArrow }, HP = 4, SP = 0, Strength = 3, Defense = 0, Hunger = 0 },
		new Spec { Id = "elementalist", Name = "Elementalist", Role = "Elemental mage", Weapons = new[] { WeaponType.MagicWand }, HP = 3, SP = 2, Strength = 1, Defense = 0, Hunger = 0 },
		new Spec { Id = "healer", Name = "Healer", Role = "Healing and support", Weapons = new[] { WeaponType.MagicWand, WeaponType.SingleSword }, HP = 4, SP = 2, Strength = 1, Defense = 0, Hunger = 0 },
		new Spec { Id = "bard", Name = "Bard", Role = "Buff and support fighter", Weapons = new[] { WeaponType.SingleSword, WeaponType.OffhandSword }, HP = 5, SP = 1, Strength = 2, Defense = 0, Hunger = 0 },
		new Spec { Id = "occultist", Name = "Occultist", Role = "Debuff and status specialist", Weapons = new[] { WeaponType.MagicWand, WeaponType.SingleSword }, HP = 3, SP = 2, Strength = 1, Defense = 0, Hunger = 0 },
		new Spec { Id = "rogue", Name = "Rogue", Role = "Mobility and disruption", Weapons = new[] { WeaponType.SingleSword, WeaponType.OffhandSword }, HP = 4, SP = 1, Strength = 2, Defense = 0, Hunger = 0 },
		new Spec { Id = "commander", Name = "Commander", Role = "Party-wide buffs and leadership", Weapons = new[] { WeaponType.SingleSword, WeaponType.OffhandShield }, HP = 5, SP = 2, Strength = 1, Defense = 0, Hunger = 0 },
		new Spec { Id = "scout", Name = "Scout", Role = "Utility and exploration", Weapons = new[] { WeaponType.BowAndArrow, WeaponType.SingleSword }, HP = 5, SP = 0, Strength = 2, Defense = 0, Hunger = 1 },
	};

	[MenuItem("Tools/Eternal Enigma/Classes/Create Missing Class Definitions")]
	public static void Create()
	{
		Directory.CreateDirectory(DefinitionsFolder);
		AssetDatabase.Refresh();

		var catalog = AssetDatabase.LoadAssetAtPath<ClassCatalog>(CatalogPath);
		if (catalog == null)
		{
			catalog = ScriptableObject.CreateInstance<ClassCatalog>();
			AssetDatabase.CreateAsset(catalog, CatalogPath);
		}

		int created = 0;
		int added = 0;

		foreach (var spec in Specs)
		{
			var path = $"{DefinitionsFolder}/{spec.Name}.asset";
			var definition = AssetDatabase.LoadAssetAtPath<ClassDefinition>(path);

			if (definition == null)
			{
				definition = ScriptableObject.CreateInstance<ClassDefinition>();
				definition.Id = spec.Id;
				definition.DisplayName = spec.Name;
				definition.Role = spec.Role;
				definition.AllowedWeapons = new List<WeaponType>(spec.Weapons);
				definition.GrowthPerLevel = new StatModification
				{
					HPMax = spec.HP,
					SPMax = spec.SP,
					Strength = spec.Strength,
					Defense = spec.Defense,
					HungerMax = spec.Hunger
				};
				definition.StartingStatBonus = new StatModification();
				definition.Skills = new List<ClassSkillEntryData>();

				AssetDatabase.CreateAsset(definition, path);
				created++;
			}

			if (!catalog.Classes.Contains(definition))
			{
				catalog.Classes.Add(definition);
				added++;
			}
		}

		catalog.Classes.RemoveAll(c => c == null);
		EditorUtility.SetDirty(catalog);
		AssetDatabase.SaveAssets();

		Debug.Log($"Class catalog: {created} definition(s) created, {added} added to catalog.");

		foreach (var error in catalog.Validate())
		{
			Debug.LogError(error);
		}
	}
}
