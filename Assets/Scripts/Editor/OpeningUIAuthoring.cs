#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OpeningUIAuthoring
{
    private static readonly string[] OpeningScenes = { "Assets/Scenes/Common.unity", "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Town.unity", "Assets/Scenes/DungeonScene.unity", "Assets/Scenes/Overworld.unity" };
    [MenuItem("Tools/Eternal Enigma/UI/Polish Opening Menus")]
    public static void PolishOpeningMenus()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Polish opening menus outside Play mode.");
        foreach (string path in OpeningScenes)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new System.InvalidOperationException("Save " + path + " before polishing opening menus.");
        }
        EditPrefab("Assets/Resources/UI/GameButton.prefab", root => GameUISkin.PadButtonLabel(root.GetComponent<Button>()));
        EditPrefab("Assets/Prefabs/Menu/EconomyShopItem.prefab", root => root.GetComponent<ShopMenuItem>().ApplyPresentation());
        EditPrefab("Assets/Resources/UI/TrainerLayout.prefab", root => root.GetComponent<TrainerLayout>().ApplyPresentation());
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/UI/Authored", "Assets/Resources/UI/Dungeon", "Assets/Prefabs/Menu" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponentsInChildren<TMP_Text>(true).Length > 0) EditPrefab(path, PadHierarchy);
        }
        foreach (string path in OpeningScenes)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var message in root.GetComponentsInChildren<MessageDialog>(true)) message.AuthorLayout();
                    foreach (var picker in root.GetComponentsInChildren<ProtagonistHeroPicker>(true)) picker.AuthorPresentation();
                    foreach (var shop in root.GetComponentsInChildren<ShopMenuDialog>(true)) shop.ApplyPresentation();
                    foreach (var trainer in root.GetComponentsInChildren<TrainerLayout>(true)) trainer.ApplyPresentation();
                    foreach (var slot in root.GetComponentsInChildren<CampaignSlotView>(true))
                    {
                        // Slot labels sit on artwork, outside the ordinary panel/button rules.
                        var label = slot.Label;
                        label.rectTransform.anchorMin = new Vector2(.06f, .60f);
                        label.rectTransform.anchorMax = new Vector2(.94f, .92f);
                        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                        label.margin = Vector4.zero;
                        label.alignment = TextAlignmentOptions.TopLeft;
                        var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(.04f, .06f, .08f, .85f);
                        shadow.effectDistance = new Vector2(1.5f, -1.5f);
                    }
                    PadHierarchy(root);
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Authored opening menu padding, readable trainer previews and hero selection.");
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        var contents = PrefabUtility.LoadPrefabContents(path);
        try { edit(contents); PrefabUtility.SaveAsPrefabAsset(contents, path); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void PadHierarchy(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var button in root.GetComponentsInChildren<Button>(true)) GameUISkin.PadButtonLabel(button);
        var theme = GameUITheme.Current;
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label.GetComponentInParent<Button>() != null || label.GetComponentInParent<Slider>() != null ||
                label.GetComponentInParent<Scrollbar>() != null || label.transform.parent is not RectTransform panel) continue;
            var frame = GameUISkin.PanelGraphic(panel);
            if (frame == null || (frame.sprite != theme.Panel && frame.sprite != theme.DungeonPaper && frame.sprite != theme.DungeonWood)) continue;
            if (panel.rect.width < 180 || panel.rect.height < 100) continue;
            GameUISkin.PadInside(label, 24, 20);
        }
    }

    [MenuItem("Tools/Eternal Enigma/UI/Author Opening Preparation")]
    public static void AuthorOpeningPreparation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Author opening preparation outside Play mode.");
        const string path = "Assets/Resources/UI/Authored/Home.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            contents.GetComponent<HomeBedDialog>().AuthorPreparationLayout();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        Debug.Log("Authored the home preparation, save-for-later and morning dialog.");
    }

    [MenuItem("Tools/Eternal Enigma/UI/Repair Campaign Slot Wiring")]
    public static void RepairCampaignSlots()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Repair campaign slots outside Play mode.");
        const string path = "Assets/Scenes/MainMenu.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (scene.isDirty) throw new System.InvalidOperationException("Save MainMenu before repairing its slot wiring.");
        var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenu>(true)).Single();
        var browser = menu.CampaignStartMenu.SlotViewer.GetComponent<CampaignSlots>();
        browser.EnsureTitle(true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(browser);
        for (int i = 0; i < browser.Slots.Length; i++)
        {
            var configured = browser.Slots[i];
            var components = configured.GetComponents<CampaignSlotView>();
            var slot = components[0];
            if (slot != configured) EditorUtility.CopySerialized(configured, slot);
            browser.Slots[i] = slot;
            menu.CampaignStartMenu.CampaignSlotViews[i] = slot;
            foreach (var duplicate in components.Where(component => component != slot))
                Object.DestroyImmediate(duplicate);
            slot.Button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(slot.Button.onClick, slot.Click);
            slot.Label.enableAutoSizing = true;
            slot.Label.fontSize = slot.Label.fontSizeMax = 48;
            slot.Label.fontSizeMin = 30;
            slot.Label.textWrappingMode = TextWrappingModes.Normal;
            PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
            PrefabUtility.RecordPrefabInstancePropertyModifications(slot.Button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(slot.Label);
        }
        var serialized = new SerializedObject(browser);
        var details = (TMP_Text)serialized.FindProperty("detail").objectReferenceValue;
        details.enableAutoSizing = true;
        details.fontSize = details.fontSizeMax = 28;
        details.fontSizeMin = 22;
        details.textWrappingMode = TextWrappingModes.Normal;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Campaign slots now have one configured view and one activation callback per card.");
    }
}
#endif
