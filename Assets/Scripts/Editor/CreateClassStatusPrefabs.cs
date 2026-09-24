using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateClassStatusPrefabs
{
	private const string Folder = "Assets/Prefabs/Dungeon/StatusEffects";
	private const string ScenePath = "Assets/Scenes/DungeonScene.unity";

	[MenuItem("Tools/Eternal Enigma/Classes/Create Class Status Prefabs")]
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
			("FireMarkStatus", typeof(FollowUpMarkStatusEffect), 3, (status) =>
			{
				var mark = status as FollowUpMarkStatusEffect;
				mark.Element = DamageElement.Fire;
				mark.DamagePercent = 0.5f;
			}),
			("IceMarkStatus", typeof(FollowUpMarkStatusEffect), 3, (status) =>
			{
				var mark = status as FollowUpMarkStatusEffect;
				mark.Element = DamageElement.Ice;
				mark.DamagePercent = 0.5f;
			}),
			("LightningMarkStatus", typeof(FollowUpMarkStatusEffect), 3, (status) =>
			{
				var mark = status as FollowUpMarkStatusEffect;
				mark.Element = DamageElement.Lightning;
				mark.DamagePercent = 0.5f;
			}),
			("ParryStatus", typeof(ParryStatusEffect), 2, (status) =>
			{
				var parry = status as ParryStatusEffect;
				parry.BlockChance = 0.5f;
			}),
			("BulwarkStatus", typeof(DamageReductionStatusEffect), 1, (status) =>
			{
				var bulwark = status as DamageReductionStatusEffect;
				bulwark.Reduction = 0.5f;
			}),
			("SanctuaryShieldStatus", typeof(DamageShieldStatusEffect), 3, (status) =>
			{
				var shield = status as DamageShieldStatusEffect;
				shield.Absorb = 10;
			}),
			("EndureStatus", typeof(EndureStatusEffect), 3, null),
			("AmplifyStatus", typeof(AmplifyStatusEffect), 5, (status) =>
			{
				var amplify = status as AmplifyStatusEffect;
				amplify.Bonus = 0.5f;
			}),
			("DefenseDownStatus", typeof(TimedBuffStatusEffect), 5, (status) =>
			{
				var buff = status as TimedBuffStatusEffect;
				buff.BuffName = "Defense Down";
				buff.Family = BuffFamily.None;
				buff.Modification = new StatModification { Defense = -3 };
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
		Debug.Log($"Class status prefabs: {created} created, {registered} registered.");
	}
}
