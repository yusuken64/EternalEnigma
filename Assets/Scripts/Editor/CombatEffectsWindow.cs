using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class CombatEffectsWindow : EditorWindow
{
    string search = "";
    int tab;
    Vector2 scroll;
    UnityEngine.Object[] assets = Array.Empty<UnityEngine.Object>();
    CombatEffectProfile selected;
    PreviewRenderUtility preview;
    readonly List<GameObject> previewObjects = new();
    float elapsed;
    double lastTime;

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Assignments And Preview")]
    public static void Open() => GetWindow<CombatEffectsWindow>("Combat Effects");
    void OnEnable() { Reload(); EditorApplication.update += Tick; }
    void OnDisable() { EditorApplication.update -= Tick; ClearPreview(); preview?.Cleanup(); preview = null; }
    void Reload()
    {
        string filter = tab == 0 ? "t:Skill" : tab == 1 ? "t:Prefab" : tab == 2 ? "t:StatusVisualProfile" : "t:CombatEffectProfile";
        assets = AssetDatabase.FindAssets(filter, tab == 1 ? new[] { "Assets/Prefabs" } : new[] { "Assets" })
            .Select(g => AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(g)))
            .Where(o => o != null && (tab != 1 || ((GameObject)o).GetComponent<CharacterCombatEffects>() != null)).OrderBy(o => o.name).ToArray();
    }
    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Assign missing")) { CombatEffectAuthoring.Author(); Reload(); }
        if (GUILayout.Button("Validate")) CombatEffectAuthoring.Validate();
        if (GUILayout.Button("Save")) AssetDatabase.SaveAssets();
        EditorGUILayout.EndHorizontal();
        int next = GUILayout.Toolbar(tab, new[] { "Abilities + icons", "Attacks", "Status auras", "Profiles" });
        if (next != tab) { tab = next; Reload(); }
        search = EditorGUILayout.TextField("Search", search);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180));
        foreach (var asset in assets.Where(a => a != null && a.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            EditorGUILayout.BeginHorizontal();
            if (asset is Skill skill)
            {
                EditorGUI.BeginChangeCheck();
                var icon = (Sprite)EditorGUILayout.ObjectField(skill.Icon, typeof(Sprite), false, GUILayout.Width(65));
                if (GUILayout.Button(skill.name, GUILayout.Width(170))) Selection.activeObject = skill;
                var profile = (CombatEffectProfile)EditorGUILayout.ObjectField(skill.VisualProfile, typeof(CombatEffectProfile), false);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(skill, "Edit ability visuals"); skill.Icon = icon; skill.VisualProfile = profile; EditorUtility.SetDirty(skill); }
                if (GUILayout.Button("Preview", GUILayout.Width(65))) StartPreview(profile);
            }
            else
            {
                EditorGUILayout.ObjectField(asset, asset.GetType(), false);
                if (GUILayout.Button("Edit", GUILayout.Width(45))) Selection.activeObject = asset;
                if (asset is CombatEffectProfile profile && GUILayout.Button("Preview", GUILayout.Width(65))) StartPreview(profile);
                if (asset is StatusVisualProfile status && GUILayout.Button("Preview", GUILayout.Width(65))) StartAuraPreview(status);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (preview != null && previewObjects.Count > 0)
        {
            var rect = GUILayoutUtility.GetRect(100, 230, GUILayout.ExpandWidth(true));
            preview.BeginPreview(rect, GUIStyle.none); preview.Render(); GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            EditorGUILayout.LabelField(selected != null ? selected.name + " — muzzle / circle / flight / impact / area" : "Persistent aura preview");
        }
    }
    void Prepare()
    {
        ClearPreview();
        preview ??= new PreviewRenderUtility();
        preview.camera.transform.position = new Vector3(0, -5, -12);
        preview.camera.transform.LookAt(Vector3.zero, Vector3.up);
        preview.camera.orthographic = true; preview.camera.orthographicSize = 5;
        preview.camera.nearClipPlane = .05f; preview.camera.farClipPlane = 100;
        preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.06f, .07f, .1f);
        elapsed = 0; lastTime = EditorApplication.timeSinceStartup;
    }
    void ClearPreview() { foreach (var go in previewObjects) if (go != null) DestroyImmediate(go); previewObjects.Clear(); }
    void Add(CombatEffectStage stage, Vector3 point)
    {
        if (stage?.Prefab == null) { previewObjects.Add(null); return; }
        var go = Instantiate(stage.Prefab); go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = point + stage.Offset; go.transform.rotation = Quaternion.Euler(stage.Rotation) * stage.Prefab.transform.localRotation;
        go.transform.localScale *= CombatEffectPlayer.StageScale(stage, 1, 2.5f);
        preview.AddSingleGO(go); previewObjects.Add(go);
    }
    void StartPreview(CombatEffectProfile profile)
    {
        if (profile == null) return;
        Prepare(); selected = profile;
        Add(profile.Muzzle, new Vector3(-3, 0, -.5f)); Add(profile.GroundCircle, new Vector3(-3, 0, 0));
        Add(profile.Projectile, new Vector3(-3, 0, -.5f)); Add(profile.Impact, new Vector3(3, 0, -.5f)); Add(profile.Area, new Vector3(3, 0, 0));
    }
    void StartAuraPreview(StatusVisualProfile status) { Prepare(); selected = null; Add(status.Aura, Vector3.zero); }
    void Tick()
    {
        if (preview == null || previewObjects.Count == 0) return;
        float delta = Mathf.Min(.05f, (float)(EditorApplication.timeSinceStartup - lastTime)); lastTime = EditorApplication.timeSinceStartup;
        elapsed = (elapsed + delta) % 3.5f;
        Simulate();
        Repaint();
    }
    void Simulate()
    {
        for (int i = 0; i < previewObjects.Count; i++)
        {
            var go = previewObjects[i]; if (go == null) continue;
            float start = selected == null ? 0 : i < 2 ? 0 : i == 2 ? selected.CastSeconds : selected.CastSeconds + .6f;
            bool visible = elapsed >= start && (i != 2 || selected == null || elapsed < start + .6f);
            go.SetActive(visible);
            if (!visible) continue;
            if (selected != null && i == 2)
            {
                go.transform.position = Vector3.Lerp(new Vector3(-3, 0, -.5f), new Vector3(3, 0, -.5f), (elapsed - start) / .6f);
                go.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.back) * Quaternion.Euler(selected.Projectile.Rotation) * selected.Projectile.Prefab.transform.localRotation;
            }
            foreach (var particles in go.GetComponentsInChildren<ParticleSystem>())
            { particles.Play(false); particles.Simulate(Mathf.Max(0, elapsed - start), false, true); }
        }
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Export Preview Gallery")]
    public static void ExportGallery()
    {
        Directory.CreateDirectory("Temp/CombatEffectPreviews");
        var window = CreateInstance<CombatEffectsWindow>();
        try
        {
            foreach (var name in new[] { "Fire Flight", "Frost Flight", "Lightning Flight", "Life Direct", "Shadow Flight", "Arrow Flight", "Melee Direct", "Fire Flight Area" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(CombatEffectAuthoring.Root + "/Profiles/" + name + ".asset");
                if (profile == null) continue;
                window.StartPreview(profile); window.elapsed = profile.CastSeconds + .95f; window.Simulate();
                File.WriteAllLines("Temp/CombatEffectPreviews/" + name + ".txt", window.previewObjects.Where(o => o != null).SelectMany(o => o.GetComponentsInChildren<ParticleSystem>()).Select(p => $"{p.name}: particles={p.particleCount} size={p.main.startSize.constantMax} scale={p.transform.lossyScale} bounds={p.GetComponent<Renderer>()?.bounds} material={p.GetComponent<Renderer>()?.sharedMaterial?.shader?.name}"));
                window.preview.BeginPreview(new Rect(0, 0, 640, 384), GUIStyle.none); window.preview.Render();
                var source = window.preview.EndPreview();
                var rt = RenderTexture.GetTemporary(640, 384, 0); var old = RenderTexture.active;
                var image = new Texture2D(640, 384, TextureFormat.RGB24, false);
                try
                {
                    Graphics.Blit(source, rt); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 640, 384), 0, 0); image.Apply();
                    File.WriteAllBytes("Temp/CombatEffectPreviews/" + name + ".png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); DestroyImmediate(image); }
            }
        }
        finally { DestroyImmediate(window); }
        Debug.Log("Combat preview gallery exported to Temp/CombatEffectPreviews.");
    }
}
