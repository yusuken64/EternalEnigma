using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
	public List<Enemy> EnemyPrefabs;
	public List<SpawnDefinition> SpawnDefinitions;

	internal Enemy GetEnemyPrefab(int floor)
	{
		var spawnDefinition = SpawnDefinitions.Where(
			x => x.FloorMin <= floor &&
				 x.FloorMax >= floor)
			.OrderBy(x => Guid.NewGuid())
			.First();
		return spawnDefinition.EnemyCharacterPrefab;
	}

#if UNITY_EDITOR
	[ContextMenu("Generate Spawn Definitions")]
	public void GenerateSpawnDefinitions()
	{
		var enemiesFolderAssetPath = "Assets/Prefabs/Enemies";

		//UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(enemiesFolderAssetPath);
		string[] assets = AssetDatabase.FindAssets("t:prefab", new[] { enemiesFolderAssetPath });

		foreach (var guid in assets)
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			SpawnDefinitions.Add(new SpawnDefinition()
			{
				SpawnName = go.name,
				EnemyCharacterPrefab = ((GameObject)go).GetComponent<Enemy>(),
				FloorMin = 1,
				FloorMax = 10
			});
		}
	}

	[ContextMenu("Set Animation Looping")]
	public void SetAnimationLooping()
	{
		foreach (var enemy in EnemyPrefabs)
		{
			var animator = enemy.Animator;
			var clips = animator.runtimeAnimatorController.animationClips;
			foreach (var clip in clips
				.Where(x => x.name.Contains("Attack")))
			{
				Debug.Log($"Updating {enemy.name} {clip.name}");

				clip.wrapMode = WrapMode.Once;
				AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
				clipSettings.loopTime = false;
				AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
			}
			EditorUtility.SetDirty(animator);
			AssetDatabase.SaveAssets();
		}
		EnemyPrefabs.ForEach(x =>
		{
			EditorUtility.SetDirty(x);
			AssetDatabase.SaveAssets();
		});
		AssetDatabase.Refresh();
	}

	[ContextMenu("AssignMonsterStats")]
	public void AssignMonsterStats()
	{
		var filePath = @"C:\Users\yusuk\Documents\GitHub\Dungeongeneration\Assets\Data\MonsterData.txt";
		MonsterDataReader monsterDataReader = new MonsterDataReader();
		List<MonsterData> monsters = monsterDataReader.ReadMonstersFromCSV(filePath);

		// Do something with the monsters list (e.g., print or process the data)
		foreach (var monster in monsters)
		{
			Debug.Log($"{monster.Name}: HP={monster.HP}, Attack={monster.Attack}, Defense={monster.Defense}");
			// Print other properties as needed
			var enemyPrefab = EnemyPrefabs.FirstOrDefault(x => x.name == monster.ThisName);
			if (enemyPrefab != null)
			{
				enemyPrefab.StartingStats.HPMax = monster.HP;
				enemyPrefab.StartingStats.Strength = monster.Attack;
				enemyPrefab.StartingStats.Defense = monster.Defense;
				enemyPrefab.StartingStats.EXPOnKill = monster.EXP;
				if (enemyPrefab.StartingStats.ActionsPerTurnMax == 0)  enemyPrefab.StartingStats.ActionsPerTurnMax = 1;
				if (enemyPrefab.StartingStats.AttacksPerTurnMax == 0) enemyPrefab.StartingStats.AttacksPerTurnMax = 1;
				if (ExpressionEvaluator.Evaluate<float>(monster.Drop, out float dropValule))
				{
					enemyPrefab.BaseStats.DropRate = dropValule;
				};
				enemyPrefab.Description = monster.Behavior + monster.SpecialAbilities + monster.AdditionalInfo;
				enemyPrefab.Team = Team.Enemy;

				var selectedObject = enemyPrefab.gameObject;
				PrefabUtility.SaveAsPrefabAsset(selectedObject, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedObject), out bool success);
				EditorUtility.SetDirty(enemyPrefab);
				AssetDatabase.SaveAssets();
			}
			else
			{
				Debug.Log($"{monster.Name} not found");
			}
		}
	}

	[ContextMenu("AssignMonsterSpawnRates")]
	public void AssignMonsterSpawnRates()
	{
		var filePath = @"C:\Users\yusuk\Documents\GitHub\Dungeongeneration\Assets\Data\MonsterData.txt";
		MonsterDataReader monsterDataReader = new MonsterDataReader();
		List<MonsterData> monsters = monsterDataReader.ReadMonstersFromCSV(filePath);

		var filePath2 = @"C:\Users\yusuk\Documents\GitHub\Dungeongeneration\Assets\Data\MonsterSpawnData.txt";
		MonsterSpawnRateReader monsterSpawnRateReader = new MonsterSpawnRateReader();
		List<MonsterSpawnData> parsedData = monsterSpawnRateReader.ParseMonsterData(filePath2);

		foreach (var monsterData in parsedData)
		{
			Debug.Log($"Floor {monsterData.Floor}:");
			for (int i = 0; i < monsterData.Monsters.Length; i++)
			{
				Debug.Log($"{monsterData.Monsters[i]} - {monsterData.Appearances[i]}/256");
			}
		}
		var spawnDefinitions = parsedData
			.SelectMany(data => data.Monsters
				.Select((monster, index) => new
				{
					Floor = data.Floor,
					Monster = monster,
					Appearance = data.Appearances[index],
					ThisName = monsters.FirstOrDefault(x => x.Name == monster)?.ThisName
				}))
			.GroupBy(item => item.Monster)
			.Select(group =>
			{
				var spawnName = $"{EnemyPrefabs.FirstOrDefault(x => x.name == (group.First().ThisName))} {group.Min(item => item.Floor)}-{group.Max(item => item.Floor)}";
				return new SpawnDefinition
				{
					SpawnName = spawnName,
					FloorMin = group.Min(item => item.Floor),
					FloorMax = group.Max(item => item.Floor),
					EnemyCharacterPrefab = EnemyPrefabs.FirstOrDefault(x => x.name == (group.First().ThisName))
				};
			})
			.ToList();

		foreach (var spawnDefinition in spawnDefinitions)
		{
			Debug.Log($"Spawn {spawnDefinition.SpawnName} {spawnDefinition.FloorMin}-{spawnDefinition.FloorMax}");
		}

		this.SpawnDefinitions = spawnDefinitions;
		EditorUtility.SetDirty(this.gameObject);
		AssetDatabase.SaveAssets();
	}
#endif
}

[Serializable]
public class SpawnDefinition
{
	public string SpawnName;
	public int FloorMin;
	public int FloorMax;
	public Enemy EnemyCharacterPrefab;
}