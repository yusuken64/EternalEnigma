#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>Gameplay adapters only; imported Wave 4 assets and ordinary spawn pools are never edited.</summary>
public static class FinaleEnemyAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Finale/Capture Combat Scale")]
    public static void CaptureCombatScale()
    {
        const string destination = "Temp/FinaleDebug/Scale";
        Directory.CreateDirectory(destination);
        foreach (var family in FinaleRoster.Required)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                string stem = FinaleRoster.PrefabStem(family);
                string prefix = FinaleRoster.Towns.Contains(family) ? "Boss_" : "Enemy_";
                var paths = new[] { "Assets/Prefabs/Dungeon/Enemies/Enemy_Skeleton.prefab",
                    "Assets/Resources/Finale/" + prefix + stem + ".prefab" };
                float cursor = 0;
                Bounds combined = default;
                for (int i = 0; i < paths.Length; i++)
                {
                    var actor = DioramaFitAuthoring.Spawn(paths[i], scene);
                    var bounds = DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(actor));
                    // Only translate: render the same transforms used in combat.
                    actor.transform.position += new Vector3(cursor - bounds.min.x, -bounds.center.y, -bounds.max.z);
                    bounds = DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(actor));
                    if (i == 0) combined = bounds; else combined.Encapsulate(bounds);
                    cursor = bounds.max.x + .6f;
                }
                var cameraObject = new GameObject("Combat scale camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(2.2f, combined.size.x * .4f, combined.size.z * .7f);
                camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.backgroundColor = new Color(.22f, .29f, .24f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transform.position = combined.center + new Vector3(0, -14, -20);
                camera.transform.LookAt(combined.center, Vector3.up);
                var lighting = new GameObject("Combat scale daylight", typeof(Light));
                SceneManager.MoveGameObjectToScene(lighting, scene);
                var light = lighting.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f;
                lighting.transform.rotation = camera.transform.rotation * Quaternion.Euler(0, -20, 0);
                DioramaFitAuthoring.Save(camera, destination + "/" + stem + ".png");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Debug.Log("Combat-scale comparisons saved to " + destination);
    }

    [MenuItem("Tools/Eternal Enigma/Finale/Build Enemy Prefabs")]
    public static void Build()
    {
        const string destination = "Assets/Resources/Finale";
        Directory.CreateDirectory(destination);
        var source = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Skeleton.prefab");
        foreach (var family in FinaleRoster.Required)
        {
            string stem = FinaleRoster.PrefabStem(family);
            var adapter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Diorama/Wave4/" + stem + ".prefab");
            if (adapter == null) throw new InvalidOperationException("Missing styled adapter: " + stem);
            var root = UnityEngine.Object.Instantiate(source.gameObject);
            try
            {
                var enemy = root.GetComponent<Enemy>();
                bool boss = FinaleRoster.Towns.Contains(family);
                root.name = (boss ? "Boss_" : "Enemy_") + stem;
                foreach (Transform child in enemy.VisualParent.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                enemy.VisualParent.transform.localPosition = source.VisualParent.transform.localPosition;
                enemy.VisualParent.transform.localScale = source.VisualParent.transform.localScale;
                enemy.VisualParent.transform.localRotation = Quaternion.identity;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(adapter, enemy.VisualParent.transform);
                model.transform.localPosition = Vector3.zero;
                // Preserve the pack's natural proportions at the same combat scale as ordinary enemies.
                model.transform.localScale = Vector3.one;
                enemy.Animator = model.GetComponentInChildren<Animator>(true);
                if (enemy.Animator?.runtimeAnimatorController == null) throw new InvalidOperationException("Missing animator: " + stem);
                enemy.Animator.applyRootMotion = false;
                enemy.Animator.Rebind();
                enemy.PlayIdleAnimation();
                var bounds = DioramaFitAuthoring.Bounds(DioramaFitAuthoring.Renderers(model));
                model.transform.position += new Vector3(enemy.VisualParent.transform.position.x - bounds.center.x,
                    enemy.VisualParent.transform.position.y - bounds.center.y, -bounds.max.z);
                enemy.DisplayName = family; enemy.IsBoss = boss; enemy.FootPrint = FootPrint.Size1x1;
                enemy.StartingStats.HPMax = boss ? 500 : 100;
                enemy.StartingStats.Strength = boss ? 50 : 40;
                enemy.StartingStats.Defense = 30; enemy.StartingStats.EXPOnKill = boss ? 1000 : 200;
                enemy.StartingStats.ActionsPerTurnMax = 1; enemy.StartingStats.AttacksPerTurnMax = 1;
                var behavior = enemy.GetComponent<EnemyBehavior>();
                behavior.Stationary = false; behavior.WaitUntilAttacked = false; behavior.PackSize = 1;
                behavior.RangedAttack = family is "Little Demon" or "Skeleton Mage" or "Star Fish";
                behavior.RootsAdjacentTarget = family == "Flower Monster";
                if (boss)
                {
                    var special = root.AddComponent<LargeBossBehavior>();
                    special.Family = family; special.WarningTurns = 1; special.CooldownTurns = 3; special.EffectTurns = 2;
                }
                PrefabUtility.SaveAsPrefabAsset(root, destination + "/" + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Authored all ten finale enemy prefabs from styled Wave 4 adapters.");
    }
}
#endif
