using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public class AllyGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    public List<GameObject> AllyModelPrefabs;

    public GameObject AllyPrefab;
    public string allyAssetFolderPath = "Assets/Prefabs/Overworld/Allies";
    public string weaponAssetFolderPath = "Assets/Prefabs/Dungeon/Items/Weapons";

    public ItemEffectDefinition EquipItemDefinition;

    [ContextMenu("Generate Ally Prefabs and Definitions")]
    public void GenerateEnemies()
    {
        foreach (var enemyModelPrefab in AllyModelPrefabs)
        {
            var copiedPrefab = AllyPrefab;
            if (copiedPrefab != null)
            {
                var allyName = enemyModelPrefab.name
                    .TrimSuffix("Default")
                    .TrimSuffix("PA");
                string newPath = $"{allyAssetFolderPath}/Ally_{allyName}.prefab"; // Set your desired path for the copied prefab
                var newPrefab = PrefabUtility.SaveAsPrefabAsset(copiedPrefab, newPath);
                var newChildInstance = Instantiate<GameObject>(enemyModelPrefab);

                newPrefab.GetComponent<OverworldAlly>().AnimatedModel = newChildInstance;

                ReplaceChildGameObject(newPrefab, "GameObject/RPGHeroHP", newChildInstance);

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

    [ContextMenu("Generate Weapon Definitions")]
    public void GenerateWeaponDefinitions()
    {
        var rightHandObjects = new List<GameObject>();
        var leftHandObjects = new List<GameObject>();

        var rightHand = AllyModelPrefabs[0].transform.FindChildRecursively("weapon_r");
        foreach (Transform child in rightHand)
		{
			rightHandObjects.Add(child.gameObject);
			Debug.Log(child.name, child);

			var path = $"{weaponAssetFolderPath}/RightHand_{child.gameObject.name}.asset";
			var weapon = AssetUtilities.EnsureAssetExists<EquipmentItemDefinition>(path);
			weapon.ItemName = child.name;
			weapon.WeaponModelName = child.name;
			weapon.WeaponType = GetWeaponType(child.name, true);
            if (weapon.WeaponType == WeaponType.TwoHandSword)
            {
                weapon.EquipmentSlot = EquipmentSlot.TwoHand;
            }
			else
			{
                weapon.EquipmentSlot = EquipmentSlot.MainHand;
			}
            weapon.ItemEffectDefinition = EquipItemDefinition;
        }
		var leftHand = AllyModelPrefabs[0].transform.FindChildRecursively("weapon_l");
        foreach (Transform child in leftHand)
        {
            leftHandObjects.Add(child.gameObject);
            Debug.Log(child.name, child);

            var path = $"{weaponAssetFolderPath}/LeftHand_{child.gameObject.name}.asset";
            var weapon = AssetUtilities.EnsureAssetExists<EquipmentItemDefinition>(path);
            weapon.ItemName = child.name;
            weapon.WeaponModelName = child.name;
            weapon.WeaponType = GetWeaponType(child.name, false);
            weapon.EquipmentSlot = EquipmentSlot.OffHand;
            weapon.ItemEffectDefinition = EquipItemDefinition;
        }

        EditorUtility.SetDirty(this.gameObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

	private static WeaponType GetWeaponType(string name, bool mainhand)
	{
        if (mainhand)
		{
            if (name.Contains("OHS", System.StringComparison.InvariantCultureIgnoreCase))
			{
                return WeaponType.SingleSword;
            }
            if (name.Contains("Spear", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return WeaponType.Spear;
            }
            if (name.Contains("THS", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return WeaponType.TwoHandSword;
            }
            if (name.Contains("Wand", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return WeaponType.MagicWand;
            }
        }
		else
        {
            if (name.Contains("OHS", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return WeaponType.OffhandSword;
            }
            if (name.Contains("Shield", System.StringComparison.InvariantCultureIgnoreCase))
            {
                return WeaponType.OffhandShield;
            }
        }
		return WeaponType.SingleSword;
    }
#endif
}