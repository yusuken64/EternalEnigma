using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HeroAnimationAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Heroes/Repair Equipment Ownership")]
    public static void RepairEquipmentOwnership()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        int repaired = 0;
        foreach (var prefab in TownSceneLoader.Default.AllyCatalog)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hero = root.GetComponent<TownAlly>();
                var equipment = root.GetComponent<Equipment>();
                if (equipment == null) throw new InvalidOperationException(path + " has no local equipment.");
                if (hero.Equipment == equipment) continue;
                hero.Equipment = equipment;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                repaired++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"Repaired equipment ownership on {repaired} hero prefabs.");
    }

    [MenuItem("Tools/Eternal Enigma/Hero Animations/Rebuild Clip Pools")]
    public static void Rebuild()
    {
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/RPGTinyHeroWavePolyart/Animator/NoWeaponStance.controller");
        var extra = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/RPGTinyHeroWavePolyart/Animator/NoWeaponStanceExtraAnim.controller");
        var baseMachine = baseController.layers[0].stateMachine;
        foreach (var name in new[] { "Sleeping_NoWeapon", "DrinkPotion_NoWeapon" })
        {
            if (baseMachine.states.Any(s => s.state.name == name)) continue;
            var source = extra.layers[0].stateMachine.states.First(s => s.state.name == name).state;
            var state = baseMachine.AddState(name);
            state.motion = source.motion;
            state.speed = source.speed;
        }
        EditorUtility.SetDirty(baseController);

        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon", "Assets/Prefabs/Town" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/Player.prefab", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith("/TownPlayer.prefab", StringComparison.OrdinalIgnoreCase) ||
                        System.IO.Path.GetFileName(p).StartsWith("Ally_MC", StringComparison.OrdinalIgnoreCase))
            .Concat(new[] { "Assets/Prefabs/Dungeon/Player.prefab", "Assets/Prefabs/Town/TownPlayer.prefab" })
            .Distinct().ToArray();
        int rebuilt = 0;
        foreach (var path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var hero in root.GetComponentsInChildren<HeroAnimator>(true))
                {
                    hero.SetAnimations(); rebuilt++;
                    EditorUtility.SetDirty(hero);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hero);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        // Unity does not persist the dungeon player's clip-list mutation through SaveAsPrefabAsset.
        // It uses the same stance controllers as TownPlayer, so copy only their serialized pool.
        const string playerPath = "Assets/Prefabs/Dungeon/Player.prefab";
        const string townPath = "Assets/Prefabs/Town/TownPlayer.prefab";
        string player = System.IO.File.ReadAllText(playerPath), town = System.IO.File.ReadAllText(townPath);
        static string Pool(string yaml)
        {
            int start = yaml.IndexOf("  StanceAnimations:", StringComparison.Ordinal);
            int end = yaml.IndexOf("  RightHandObjects:", start, StringComparison.Ordinal);
            if (start < 0 || end < 0) throw new InvalidOperationException("Hero clip pool layout changed.");
            return yaml.Substring(start, end - start);
        }
        string oldPool = Pool(player);
        string newPool = Pool(town).Replace("\r\n", "\n");
        if (oldPool.Contains("\r\n")) newPool = newPool.Replace("\n", "\r\n");
        if (oldPool != newPool)
        {
            System.IO.File.WriteAllText(playerPath, player.Replace(oldPool, newPool));
            AssetDatabase.ImportAsset(playerPath);
        }
        Debug.Log($"Rebuilt {rebuilt} hero clip pools in {paths.Length} prefabs.");
    }
}
