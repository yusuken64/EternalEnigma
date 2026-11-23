#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class EncounterBossAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Encounters/Build Large Boss Catalogue")]
    public static void Build()
    {
        const string destination = "Assets/Resources/Bosses";
        Directory.CreateDirectory(destination);
        using var silhouette = new MonsterSilhouetteCamera();
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
                FitFootprint(enemy, silhouette);
                PrefabUtility.SaveAsPrefabAsset(go,$"{destination}/{profile.Family.Replace(" ", "")}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"Authored {BossCatalog.All.Count} large boss variants.");
    }

    [MenuItem("Tools/Eternal Enigma/Encounters/Center Large Monster Footprints")]
    public static void CenterLargeMonsterFootprints()
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Dungeon/Enemies", "Assets/Resources/Bosses", "Assets/Resources/Finale" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);
        using var silhouette = new MonsterSilhouetteCamera();
        int count = 0, changed = 0;
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>(path);
            if (prefab == null || prefab.FootPrint != FootPrint.Size3x3) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (FitFootprint(root.GetComponent<Enemy>(), silhouette))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                count++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"Checked {count} three-by-three monster footprints and updated {changed}.");
    }

    private static bool FitFootprint(Enemy enemy, MonsterSilhouetteCamera silhouette)
    {
        // Pose a disposable copy so animation sampling never writes bones back into the prefab.
        var posed = UnityEngine.Object.Instantiate(enemy);
        var graph = PlayableGraph.Create("Resting monster footprint");
        try
        {
            var model = posed.ModelRoot;
            if (model == null) throw new InvalidOperationException("Missing monster model on " + enemy.name);
            var pivot = posed.VisualParent.transform;
            pivot.rotation = Quaternion.identity;
            posed.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            posed.Animator.Rebind();
            posed.PlayIdleAnimation();
            var idle = posed.Animator.GetCurrentAnimatorClipInfo(0).FirstOrDefault().clip ??
                posed.Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.EndsWith("IdleNormal", StringComparison.OrdinalIgnoreCase));
            if (idle == null) throw new InvalidOperationException("Missing resting animation on " + enemy.name);
            // Evaluate through the actual avatar so humanoid hands carry their weapons into the idle pose.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playback = AnimationClipPlayable.Create(graph, idle);
            var output = AnimationPlayableOutput.Create(graph, "Idle", posed.Animator);
            output.SetSourcePlayable(playback);
            playback.SetTime(0);
            playback.SetSpeed(0);
            graph.Play();
            graph.Evaluate(0f);
            Vector2 Correction(Quaternion heading)
            {
                pivot.rotation = heading;
                var offset = (Vector2)pivot.position - silhouette.Center(posed);
                return pivot.InverseTransformVector(offset);
            }
            // The square grid has two distinct pairs of projection axes across its eight headings.
            var cardinal = Correction(Quaternion.identity);
            var diagonal = Correction(Quaternion.Euler(0, 0, 45)) - cardinal;
            if (cardinal.sqrMagnitude < 1e-10f && (diagonal - enemy.DiagonalFootprintOffset).sqrMagnitude < 1e-10f) return false;
            var sourceModel = enemy.ModelRoot;
            sourceModel.localPosition += new Vector3(cardinal.x, cardinal.y, 0);
            enemy.DiagonalFootprintOffset = diagonal;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sourceModel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
            return true;
        }
        finally
        {
            graph.Destroy();
            UnityEngine.Object.DestroyImmediate(posed.gameObject);
        }
    }
}
#endif
