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
