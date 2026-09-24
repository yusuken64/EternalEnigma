using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateCombatStatusPrefabs
{
	private const string Folder = "Assets/Prefabs/Dungeon/StatusEffects";
	private const string ScenePath = "Assets/Scenes/DungeonScene.unity";

	[MenuItem("Tools/Eternal Enigma/Classes/Create Combat Status Prefabs")]
	public static void Create()
	{
		// Save current scene if modified
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
		{
			return;
		}

		// Define the list of status prefabs to create
		var prefabsToCreate = new List<(string prefabName, System.Type type, int turns, System.Action<StatusEffect> configure)>
		{
			("ArmBindStatus", typeof(ArmBindStatusEffect), 3, null),
			("StunStatus", typeof(StunStatusEffect), 1, null),
			("ParalysisStatus", typeof(ParalysisStatusEffect), 4, null),
			("BurnStatus", typeof(BurnStatusEffect), 3, null),
			("CurseStatus", typeof(CurseStatusEffect), 4, null),
			("WeakenStatus", typeof(WeakenStatusEffect), 5, null),
			("BlindStatus", typeof(BlindStatusEffect), 4, null),
			("ExposedStatus", typeof(ResistanceShiftStatusEffect), 5, null),
			("TauntStatus", typeof(TauntStatusEffect), 3, null),
			("StealthStatus", typeof(StealthStatusEffect), 5, null),
			("FearStatus", typeof(FearStatusEffect), 3, null),
			("TimedBuffStatus", typeof(TimedBuffStatusEffect), 5, null),
			("FireBarrierStatus", typeof(BarrierStatusEffect), 1, (status) =>
			{
				var barrier = status as BarrierStatusEffect;
				barrier.Element = DamageElement.Fire;
				barrier.Family = BuffFamily.Barrier;
				barrier.BuffName = "Fire Barrier";
			}),
			("IceBarrierStatus", typeof(BarrierStatusEffect), 1, (status) =>
			{
				var barrier = status as BarrierStatusEffect;
				barrier.Element = DamageElement.Ice;
				barrier.Family = BuffFamily.Barrier;
				barrier.BuffName = "Ice Barrier";
			}),
			("LightningBarrierStatus", typeof(BarrierStatusEffect), 1, (status) =>
			{
				var barrier = status as BarrierStatusEffect;
				barrier.Element = DamageElement.Lightning;
				barrier.Family = BuffFamily.Barrier;
				barrier.BuffName = "Lightning Barrier";
			}),
		};

		int created = 0;
		int registered = 0;

		// Step 2: Create prefabs if they don't exist
		foreach (var (prefabName, type, turns, configure) in prefabsToCreate)
		{
			var path = $"{Folder}/{prefabName}.prefab";
			var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

			if (existing != null)
			{
				continue; // Skip if already exists
			}

			// Create new GameObject and add component
			var go = new GameObject(prefabName);
			var status = go.AddComponent(type) as StatusEffect;

			// Set TurnsLeft
			status.TurnsLeft = turns;

			// Call configure action if provided
			configure?.Invoke(status);

			// Save as prefab
			PrefabUtility.SaveAsPrefabAsset(go, path);
			UnityEngine.Object.DestroyImmediate(go);

			created++;
		}

		// Step 3: Open the scene
		var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

		// Find the Game component
		var game = UnityEngine.Object.FindFirstObjectByType<Game>();
		if (game == null)
		{
			Debug.LogError("Game component not found in DungeonScene.");
			return;
		}

		// Step 4: Register prefabs in the Game component
		game.StatusEffectPrefabs ??= new List<StatusEffect>();

		foreach (var (prefabName, _, _, _) in prefabsToCreate)
		{
			var path = $"{Folder}/{prefabName}.prefab";
			var prefab = AssetDatabase.LoadAssetAtPath<StatusEffect>(path);

			if (prefab != null && !game.StatusEffectPrefabs.Contains(prefab))
			{
				game.StatusEffectPrefabs.Add(prefab);
				registered++;
			}
		}

		// Step 5: Save if anything was added
		if (registered > 0)
		{
			EditorUtility.SetDirty(game);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}

		// Step 6: Save assets and log
		AssetDatabase.SaveAssets();
		Debug.Log($"Combat status prefabs: {created} created, {registered} registered.");
	}
}
