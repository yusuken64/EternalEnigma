#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Preserves the roster-to-model mapping authored in Assets/Data/MonsterData.txt.</summary>
public static class EnemyBehaviorAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Enemies/Author Dungeon Behaviors")]
    public static void Author()
    {
        const string statusPath = "Assets/Prefabs/Dungeon/StatusEffects/ConfusionStatus.prefab";
        var status = AssetDatabase.LoadAssetAtPath<ConfusionStatusEffect>(statusPath);
        if (status == null)
        {
            var go = new GameObject("ConfusionStatus");
            go.AddComponent<ConfusionStatusEffect>().TurnsLeft = 3;
            status = PrefabUtility.SaveAsPrefabAsset(go, statusPath).GetComponent<ConfusionStatusEffect>();
            Object.DestroyImmediate(go);
        }
        Configure("EvilMage", b => b.WaitUntilAttacked = true);
        Configure("Orc", b => b.WaitUntilAttacked = true);
        Configure("RatAssassin", b => { b.RangedAttack = true; b.Range = 6; });
        Configure("MonsterPlant", b => { b.Steals = true; b.StealGold = true; b.StealItems = false; });
        Configure("Salamander", b => { b.WaitUntilAttacked = true; b.Steals = true; b.StealGold = false; b.StealItems = true; });
        Configure("Beholder", b => { b.Confusion = status; b.Range = 5; });
        Configure("ChestMonster", b => {
            b.Mimic = true;
            b.ChestVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/3D Props - Adorable Items/Adorable 3D Items/Prefabs/treasure chest.prefab");
        });
        AssetDatabase.SaveAssets();
    }

    private static void Configure(string name, System.Action<EnemyBehavior> configure)
    {
        string path = "Assets/Prefabs/Dungeon/Enemies/Enemy_" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var behavior = root.GetComponent<EnemyBehavior>() ?? root.AddComponent<EnemyBehavior>();
            configure(behavior);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
