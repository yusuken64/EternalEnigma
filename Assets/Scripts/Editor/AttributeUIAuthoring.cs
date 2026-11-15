using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AttributeUIAuthoring
{
    public static void ConfigurePickerPreview(PartyMenuPicker picker, bool dungeon)
    {
        if (picker.OptionDescription == null) return;
        var panel = (RectTransform)picker.OptionDescription.transform.parent;
        panel.anchorMin = new Vector2(dungeon ? .285f : .71f, .22f);
        panel.anchorMax = new Vector2(dungeon ? .59f : .96f, .78f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        panel.gameObject.SetActive(false);
    }

    [MenuItem("Tools/Eternal Enigma/Repair Picker Preview Layout")]
    public static void RepairPickerPreviewLayout()
    {
        foreach (var path in new[] { "Assets/Resources/UI/Authored/PartyPicker.prefab", "Assets/Resources/UI/Dungeon/PartyPicker.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigurePickerPreview(root.GetComponent<PartyMenuPicker>(), path.Contains("/Dungeon/"));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    [MenuItem("Tools/Eternal Enigma/Author Level Up Popup")]
    public static void AuthorLevelUpPopup()
    {
        var canvas=GameUISkin.Canvas("Level up choice",null,120);
        var dialog=canvas.gameObject.AddComponent<LevelUpChoiceDialog>();
        // Keep clicks in the modal without obscuring the dungeon behind the popup.
        GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=Color.clear;
        var panel=GameUISkin.Panel(canvas.transform,new Vector2(.23f,.37f),new Vector2(.77f,.83f));
        panel.name="Level up popup";
        var title=GameUISkin.Label(panel.transform,"Level up!",new Vector2(.08f,.87f),new Vector2(.92f,.97f),38);
        title.alignment=TextAlignmentOptions.Center;title.fontStyle=FontStyles.Bold;
        title.enableAutoSizing=true;title.fontSizeMin=30;title.fontSizeMax=38;
        dialog.Heading=GameUISkin.Label(panel.transform,"Hero",new Vector2(.06f,.79f),new Vector2(.94f,.87f),24);
        dialog.Heading.alignment=TextAlignmentOptions.Center;
        dialog.Heading.enableAutoSizing=true;dialog.Heading.fontSizeMin=18;dialog.Heading.fontSizeMax=24;
        dialog.StrButton=GameUISkin.Button(panel.transform,"STR",new Vector2(.05f,.12f),new Vector2(.34f,.76f),null);
        dialog.IntButton=GameUISkin.Button(panel.transform,"INT",new Vector2(.355f,.12f),new Vector2(.645f,.76f),null);
        dialog.AgiButton=GameUISkin.Button(panel.transform,"AGI",new Vector2(.66f,.12f),new Vector2(.95f,.76f),null);
        foreach(var button in new[]{dialog.StrButton,dialog.IntButton,dialog.AgiButton})
        {
            button.gameObject.AddComponent<LevelUpChoiceView>();
            var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=26;label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=26;
            label.fontStyle=FontStyles.Bold;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.rectTransform.anchorMin=new Vector2(.06f,.84f);label.rectTransform.anchorMax=new Vector2(.94f,.96f);
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;label.margin=Vector4.zero;
        }
        dialog.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(dialog.gameObject,"Assets/Resources/UI/LevelUpChoiceDialog.prefab");
        Object.DestroyImmediate(dialog.gameObject);
    }

    [MenuItem("Tools/Eternal Enigma/Author Attribute UI")]
    public static void Author()
    {
        AuthorLevelUpPopup();
        foreach(var path in new[]{"Assets/Resources/UI/PartyMenu.prefab","Assets/Resources/UI/Dungeon/PartyMenu.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var menu=root.GetComponent<PartyMenu>();
                if(menu.EquipmentTab==null){menu.EquipmentTab=Object.Instantiate(menu.SkillsTab,menu.SkillsTab.transform.parent);menu.EquipmentTab.name="Equipment tab";}
                if(menu.StatsTab==null){menu.StatsTab=Object.Instantiate(menu.SkillsTab,menu.SkillsTab.transform.parent);menu.StatsTab.name="Stats tab";}
                if(path.Contains("/Dungeon/"))
                    foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                        if(label.text=="Inventory & Skills")label.text="Party";
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var path in new[]{"Assets/Resources/UI/Authored/PartyPicker.prefab","Assets/Resources/UI/Dungeon/PartyPicker.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var picker=root.GetComponent<PartyMenuPicker>();
                if(picker.OptionDescription==null)
                {
                    var previewPanel=GameUISkin.Panel(root.transform,new Vector2(.71f,.22f),new Vector2(.96f,.78f));previewPanel.name="Option preview";
                    picker.OptionDescription=GameUISkin.Label(previewPanel.transform,"Stat preview",new Vector2(.06f,.04f),new Vector2(.94f,.96f),22);
                    picker.OptionDescription.textWrappingMode=TextWrappingModes.Normal;
                }
                ConfigurePickerPreview(picker, path.Contains("/Dungeon/"));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var shortcutRoot=new GameObject("Shortcut authoring");
        PartyMenuLauncher.AuthorLayout(shortcutRoot.transform,()=>false,_=>{});
        var launcher=shortcutRoot.GetComponentInChildren<PartyMenuLauncher>();
        launcher.transform.SetParent(null,false);
        PrefabUtility.SaveAsPrefabAsset(launcher.gameObject,"Assets/Resources/UI/Authored/PartyShortcuts.prefab");
        Object.DestroyImmediate(launcher.gameObject);Object.DestroyImmediate(shortcutRoot);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }
}
