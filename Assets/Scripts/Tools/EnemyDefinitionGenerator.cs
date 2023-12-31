#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class EnemyDefinitionGenerator : MonoBehaviour
{
	public List<GameObject> EnemyModelPrefabs;

    public GameObject EnemyPrefab;
	public string EnemyAssetFolderPath = "Assets/Prefabs/Enemies";

    public EnemyManager EnemyManager;

    [ContextMenu("Generate Enemy Prefabs and Definitions")]
	public void GenerateEnemies()
    {
        foreach (var enemyModelPrefab in EnemyModelPrefabs)
        {
            var copiedPrefab = EnemyPrefab;
            if (copiedPrefab != null)
            {
                var monsterName = enemyModelPrefab.name
                    .TrimSuffix("Default")
                    .TrimSuffix("PA");
                string newPath = $"{EnemyAssetFolderPath}/Enemy_{monsterName}.prefab"; // Set your desired path for the copied prefab
                var newPrefab = PrefabUtility.SaveAsPrefabAsset(copiedPrefab, newPath);
                var newChildInstance = Instantiate<GameObject>(enemyModelPrefab);

                var newEnemyPrefab = ReplaceChildGameObject(newPrefab, "GameObject/RPGHeroHP", newChildInstance);

                //EnemyManager.SpawnDefinitions.Add(new SpawnDefinition()
                //{
                //    EnemyCharacterPrefab = newEnemyPrefab.GetComponent<Enemy>(),
                //    FloorMin = 1,
                //    FloorMax = 10,
                //});
                Debug.Log("Prefab copied to: " + newPath);
            }
            else
            {
                Debug.LogError("Failed to load the prefab contents.");
            }
        }
    }

    public static GameObject ReplaceChildGameObject(GameObject prefab, string childPath, GameObject newChild)
    {
        if (prefab == null || newChild == null)
        {
            Debug.LogError("Prefab or new GameObject is null.");
            return null;
        }

        GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        Transform targetTransform = prefabInstance.transform.Find(childPath);

        if (targetTransform != null)
        {
            GameObject oldChild = targetTransform.gameObject;
            Undo.RegisterCompleteObjectUndo(prefabInstance, "Replace Child GameObject");

            // Replace the old child with the new GameObject
            newChild.transform.parent = oldChild.transform.parent;
            newChild.transform.localPosition = oldChild.transform.localPosition;
            newChild.transform.localRotation = oldChild.transform.localRotation;
            newChild.transform.localScale = oldChild.transform.localScale;
            newChild.transform.SetAsFirstSibling();

			Animator animator = newChild.GetComponent<Animator>();
            RemoveAllTransitions(animator.runtimeAnimatorController as AnimatorController);
			(prefabInstance.GetComponent<Enemy>()).Animator = animator;

            Undo.DestroyObjectImmediate(oldChild);
            PrefabUtility.ApplyPrefabInstance(prefabInstance, InteractionMode.UserAction);
            Debug.Log("Child GameObject replaced in the prefab.");
        }
        else
        {
            Debug.LogError("Child GameObject not found at the specified path.");
        }

        DestroyImmediate(prefabInstance);

        return prefabInstance;
    }

    public static void RemoveAllTransitions(AnimatorController animatorController)
    {
        if (animatorController == null)
        {
            Debug.LogError("Animator Controller is not assigned.");
            return;
        }

        foreach (var layer in animatorController.layers)
        {
            // Iterate through each state and remove its transitions
            foreach (var state in layer.stateMachine.states)
            {
                foreach (var transition in state.state.transitions)
                {
                    // Delete transition
                    state.state.RemoveTransition(transition);
                }
            }
        }

        Debug.Log("All transitions removed from the Animator Controller.");
    }
}
#endif