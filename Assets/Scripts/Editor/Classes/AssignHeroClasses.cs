using System;
using UnityEditor;
using UnityEngine;

public static class AssignHeroClasses
{
	public static readonly (string Prefab, string Primary, string Secondary)[] Table = new[]
	{
		("Ally_MC01", "warrior", null),
		("Ally_MC02", "guardian", null),
		("Ally_MC03", "warrior", null),
		("Ally_MC04", "archer", null),
		("Ally_MC05", "elementalist", null),
		("Ally_MC06", "healer", null),
		("Ally_MC07", "bard", null),
		("Ally_MC08", "occultist", null),
		("Ally_MC09", "rogue", null),
		("Ally_MC10", "commander", null),
		("Ally_MC11", "scout", null),
		("Ally_MC12", "guardian", null),
		("Ally_MC13", "healer", null),
		("Ally_MC14", "archer", null),
		("Ally_MC15", "elementalist", null),
		("Ally_MC16", "bard", null),
		("Ally_MC17", "occultist", null),
		("Ally_MC18", "rogue", null),
		("Ally_MC19", "commander", null),
		("Ally_MC20", "scout", null),
		("Ally_MC21", "warrior", "guardian"),
		("Ally_MC22", "healer", "elementalist"),
		("Ally_MC23", "archer", "scout"),
		("Ally_MC24", "rogue", "occultist"),
	};

	[MenuItem("Tools/Eternal Enigma/Classes/Assign Hero Classes")]
	public static void Build()
	{
		var catalog = AssetDatabase.LoadAssetAtPath<ClassCatalog>("Assets/Resources/Classes/ClassCatalog.asset")
			?? throw new InvalidOperationException("Class catalog missing.");

		int count = 0;

		foreach (var row in Table)
		{
			var path = $"Assets/Prefabs/Town/Allies/{row.Prefab}.prefab";
			var root = PrefabUtility.LoadPrefabContents(path);

			try
			{
				var ally = root.GetComponent<TownAlly>();
				if (ally == null)
				{
					Debug.LogError($"TownAlly component not found on {path}");
					continue;
				}

				ally.PrimaryClass = catalog.Get(row.Primary)
					?? throw new InvalidOperationException($"Primary class '{row.Primary}' not found in catalog");

				ally.SecondaryClass = row.Secondary == null
					? null
					: catalog.Get(row.Secondary)
						?? throw new InvalidOperationException($"Secondary class '{row.Secondary}' not found in catalog");

				PrefabUtility.SaveAsPrefabAsset(root, path);
				count++;
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		Debug.Log($"Assigned classes to {count} heroes.");
	}
}
