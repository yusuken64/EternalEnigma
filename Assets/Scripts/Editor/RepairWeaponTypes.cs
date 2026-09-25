using System;
using UnityEditor;
using UnityEngine;

// Weapon assets were authored before WeaponType was reordered; derive the intended type from the model name.
public static class RepairWeaponTypes
{
	[MenuItem("Tools/Eternal Enigma/Classes/Repair Weapon Types")]
	public static void Repair()
	{
		int changed = 0;
		foreach (var guid in AssetDatabase.FindAssets("t:EquipmentItemDefinition"))
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				if (asset is not EquipmentItemDefinition definition) continue;
				var expected = ExpectedWeaponType(definition.WeaponModelName, definition.EquipmentSlot);
				if (expected == null || definition.WeaponType == expected.Value) continue;
				Debug.Log($"{path} ({definition.name}): WeaponType {definition.WeaponType} -> {expected.Value}");
				definition.WeaponType = expected.Value;
				EditorUtility.SetDirty(definition);
				changed++;
			}
		}
		AssetDatabase.SaveAssets();
		Debug.Log($"Repair Weapon Types: {changed} asset(s) changed.");
	}

	public static WeaponType? ExpectedWeaponType(string weaponModelName, EquipmentSlot slot)
	{
		if (string.IsNullOrEmpty(weaponModelName)) return null;
		var name = weaponModelName;
		if (name == "Arrows" || name.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0) return WeaponType.BowAndArrow;
		if (name.StartsWith("THS", StringComparison.Ordinal)) return WeaponType.TwoHandSword;
		if (name.StartsWith("Wand", StringComparison.Ordinal)) return WeaponType.MagicWand;
		if (name.StartsWith("Spear", StringComparison.Ordinal)) return WeaponType.Spear;
		if (name.StartsWith("Shield", StringComparison.Ordinal)) return WeaponType.OffhandShield;
		if (name.StartsWith("OHS", StringComparison.Ordinal))
			return slot == EquipmentSlot.OffHand ? WeaponType.OffhandSword : WeaponType.SingleSword;
		return null;
	}
}
