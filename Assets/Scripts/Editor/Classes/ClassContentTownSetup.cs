using UnityEditor;
using UnityEngine;

public static class ClassContentTownSetup
{
	public static void Build()
	{
		var assetGuids = AssetDatabase.FindAssets("t:TownConfiguration");
		int count = 0;

		foreach (var guid in assetGuids)
		{
			var assetPath = AssetDatabase.GUIDToAssetPath(guid);
			var config = AssetDatabase.LoadAssetAtPath<TownConfiguration>(assetPath);

			if (config != null && config.LearnableSkills != null && config.LearnableSkills.Count > 0)
			{
				config.LearnableSkills.Clear();
				EditorUtility.SetDirty(config);
				count++;
			}
		}

		AssetDatabase.SaveAssets();
		Debug.Log($"Cleared legacy trainer lists on {count} town configuration(s).");
	}
}
