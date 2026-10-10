#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using UnityEditor;
using UnityEngine;

public static class EncounterBossAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Encounters/Build Large Boss Catalogue")]
    public static void Build()
    {
        const string destination = "Assets/Resources/Bosses";
        Directory.CreateDirectory(destination);
        foreach (var profile in BossCatalog.All)
        {
            string stem = profile.Family switch {
                "Angry Mushroom" or "Smiling Mushroom" => "MushroomAngry",
                "Flying Demon" => "FylingDemon", "Stingray" => "StingRay", _ => profile.Family.Replace(" ", "")
            };
            var source = AssetDatabase.LoadAssetAtPath<Enemy>($"Assets/Prefabs/Dungeon/Enemies/Enemy_{stem}.prefab");
            if (source == null) throw new InvalidOperationException("Missing boss source: " + stem);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source.gameObject);
            try
            {
                var enemy = go.GetComponent<Enemy>();
                go.name = "Large " + profile.Family;
                enemy.DisplayName = go.name; enemy.IsBoss = true; enemy.FootPrint = FootPrint.Size3x3;
                enemy.StartingStats.HPMax = Mathf.Max(1,Mathf.RoundToInt(source.StartingStats.HPMax*profile.HealthMultiplier));
                enemy.StartingStats.Strength = Mathf.Max(1,Mathf.RoundToInt(source.StartingStats.Strength*profile.StrengthMultiplier));
                enemy.StartingStats.EXPOnKill = Mathf.Max(1,Mathf.RoundToInt(source.StartingStats.EXPOnKill*profile.ExperienceMultiplier));
                enemy.StartingStats.ActionsPerTurnMax = 1;
                enemy.VisualParent.transform.localScale *= 2.25f;
                var ordinary = enemy.GetComponent<EnemyBehavior>();
                if (ordinary != null) { ordinary.PackSize=1; ordinary.WaitUntilAttacked=false; ordinary.Mimic=false; ordinary.WarpWhenHitChance=0; }
                var boss = enemy.GetComponent<LargeBossBehavior>() ?? go.AddComponent<LargeBossBehavior>();
                boss.Family=profile.Family;boss.WarningTurns=profile.WarningTurns;boss.CooldownTurns=profile.CooldownTurns;boss.EffectTurns=profile.EffectTurns;
                if(profile.Family=="Smiling Mushroom")
                    foreach(var transform in go.GetComponentsInChildren<Transform>(true))
                    {
                        if(transform.name.IndexOf("mouthSmile",StringComparison.OrdinalIgnoreCase)>=0) transform.gameObject.SetActive(true);
                        if(transform.name.IndexOf("mouthAngry",StringComparison.OrdinalIgnoreCase)>=0) transform.gameObject.SetActive(false);
                    }
                PrefabUtility.SaveAsPrefabAsset(go,$"{destination}/{profile.Family.Replace(" ", "")}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"Authored {BossCatalog.All.Count} large boss variants.");
    }
}
#endif
