#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Serialize semantic cancel roles, independent of translated button labels.</summary>
public static class MenuAudioAuthoring
{
    private static readonly HashSet<string> CancelCallbacks = new()
    {
        "Cancel_Clicked", "Cancel_ClickeD", "Close_Clicked", "CloseDialog", "NoClicked", "Back"
    };

    [MenuItem("Tools/Audio/Migrate native menu buttons")]
    public static void MigrateNativeButtons()
    {
        const string native = "4e29b1a8efbd4b44bb3f3716e73f07ff";
        const string withAudio = "7df1d0d9bdb94b79bb0dd87dac48d09b";
        var paths = Directory.GetFiles("Assets/Resources/UI", "*.prefab", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories))
            .Concat(new[] { "Assets/Scenes/Common.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/DungeonScene.unity", "Assets/Scenes/Town.unity", "Assets/Scenes/Overworld.unity" })
            .OrderBy(path => path).ToArray();
        var report = new List<string>();
        int controls = 0, references = 0;
        foreach (string path in paths)
        {
            string original = File.ReadAllText(path);
            int authored = 0, stripped = 0;
            string updated = Regex.Replace(original, @"(?ms)^--- !u!114 &-?\d+(?: stripped)?\r?\n.*?(?=^--- !u!|\z)", match =>
            {
                string block = match.Value;
                if (!block.Contains("m_Script: {fileID: 11500000, guid: " + native)) return block;
                block = block.Replace("m_Script: {fileID: 11500000, guid: " + native,
                    "m_Script: {fileID: 11500000, guid: " + withAudio);
                block = Regex.Replace(block, @"(?m)^  m_EditorClassIdentifier:[^\r\n]*", "  m_EditorClassIdentifier:");
                if (match.Value.Split('\n')[0].Contains(" stripped")) { stripped++; return block; }
                string newline = block.Contains("\r\n") ? "\r\n" : "\n";
                bool cancel = CancelCallbacks.Any(callback => Regex.IsMatch(block, @"(?m)^\s+m_MethodName: " + Regex.Escape(callback) + @"\r?$"));
                // Keep the original Button component ID, listener data, references
                // and one-click behavior. Only its audio-capable subclass changes.
                authored++;
                return block + "  activateOnFirstPointerPress: 1" + newline + "  soundRole: " + (cancel ? "1" : "0") + newline;
            });
            if (updated == original) continue;
            File.WriteAllText(path, updated, new UTF8Encoding(false));
            controls += authored; references += stripped;
            report.Add(path.Replace('\\', '/') + ": " + authored + " controls, " + stripped + " inherited references");
        }
        AssetDatabase.Refresh();
        string summary = "Native menu button migration: " + controls + " controls, " + references + " inherited references across " + report.Count + " files.\n" + string.Join("\n", report);
        Directory.CreateDirectory("Temp/SoundAudit");
        File.WriteAllText("Temp/SoundAudit/menu-button-migration.txt", summary);
        Debug.Log(summary);
    }

    public static bool Configure(GameObject root)
    {
        bool changed = false;
        void Cancel(UnityEngine.Object value)
        {
            if (value is not SelectToActivateButton button || button.AudioRole == SelectToActivateButton.SoundRole.Cancel) return;
            button.AudioRole = SelectToActivateButton.SoundRole.Cancel;
            EditorUtility.SetDirty(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            changed = true;
        }
        foreach (var button in root.GetComponentsInChildren<SelectToActivateButton>(true))
        {
            if (button.GetComponent<DungeonDialogClose>() != null) Cancel(button);
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                string method = button.onClick.GetPersistentMethodName(index);
                if (CancelCallbacks.Contains(method) || method == "Exit_Clicked" && button.onClick.GetPersistentTarget(index) is GlobalSettings)
                    Cancel(button);
            }
        }
        // These buttons use runtime listeners, so their serialized owner references
        // establish the role even when the prefab has no persistent callback.
        foreach (var owner in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (owner == null) continue;
            string[] fields = owner switch
            {
                GlobalSettings => new[] { "ResumeButton" },
                CampaignChoice => new[] { "Back" },
                CampaignSlots or ProtagonistHeroPicker or ProtagonistClassPicker or PartyMenu or PartyMenuPicker or ShopMenuDialog => new[] { "BackButton" },
                BuyConfirmationDialog or BallistaPurchaseDialog or EntranceDialog or InnDialog or StatueDialog => new[] { "CancelButton" },
                AllyRecruitDialog => new[] { "CancelButton", "CancelButton2" },
                HomeBedDialog => new[] { "back" },
                StairConfirm => new[] { "NoButton" },
                TrainerLayout => new[] { "Close" },
                EventHistoryDialog => new[] { "Back" },
                _ => System.Array.Empty<string>()
            };
            if (fields.Length == 0) continue;
            var serialized = new SerializedObject(owner);
            foreach (var field in fields) Cancel(serialized.FindProperty(field)?.objectReferenceValue);
        }
        return changed;
    }

    [MenuItem("Tools/Audio/Author menu sound roles")]
    public static void Author()
    {
        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/UI", "Assets/Prefabs/Menu" })
            .Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { if (Configure(root)) PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in new[] { "Assets/Scenes/Common.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/DungeonScene.unity", "Assets/Scenes/Town.unity", "Assets/Scenes/Overworld.unity" })
        {
            if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= Configure(root);
                if (changed) EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
#endif
