using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GenerateAllClassContent
{
	[MenuItem("Tools/Eternal Enigma/Classes/Generate All Class Content")]
	public static void GenerateAll()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		CreateClassCatalog.Create();
		CreateCombatStatusPrefabs.Create();
		CreateClassStatusPrefabs.Create();
		ClassContentShared.Build();
		ClassContent_Warrior.Build();
		ClassContent_Guardian.Build();
		ClassContent_Archer.Build();
		ClassContent_Elementalist.Build();
		ClassContent_Healer.Build();
		ClassContent_Bard.Build();
		ClassContent_Occultist.Build();
		ClassContent_Rogue.Build();
		ClassContent_Commander.Build();
		ClassContent_Scout.Build();
		AssignHeroClasses.Build();
		//TuneEnemyResistances.Build();
		ClassContentTownSetup.Build();
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		var catalog = ClassCatalog.Load();
		foreach (var error in catalog != null ? catalog.Validate() : new[] { "Class catalog missing." }) Debug.LogError(error);
		Debug.Log("Generate All Class Content finished.");
	}
}
