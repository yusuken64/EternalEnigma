using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AttributeUIAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Author Attribute UI")]
    public static void Author()
    {
        var canvas=GameUISkin.Canvas("Level up choice",null,120);
        var dialog=canvas.gameObject.AddComponent<LevelUpChoiceDialog>();
        GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=new Color(0,0,0,.55f);
        var panel=GameUISkin.Panel(canvas.transform,new Vector2(.18f,.18f),new Vector2(.82f,.82f));
        dialog.Heading=GameUISkin.Label(panel.transform,"Level up",new Vector2(.06f,.83f),new Vector2(.94f,.97f),30);
        dialog.StrButton=GameUISkin.Button(panel.transform,"STR",new Vector2(.06f,.64f),new Vector2(.94f,.81f),null);
        dialog.IntButton=GameUISkin.Button(panel.transform,"INT",new Vector2(.06f,.45f),new Vector2(.94f,.62f),null);
        dialog.AgiButton=GameUISkin.Button(panel.transform,"AGI",new Vector2(.06f,.26f),new Vector2(.94f,.43f),null);
        dialog.LaterButton=GameUISkin.Button(panel.transform,"Later",new Vector2(.36f,.06f),new Vector2(.64f,.22f),null);
        foreach(var button in new[]{dialog.StrButton,dialog.IntButton,dialog.AgiButton})
        {var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=21;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=24;}
        dialog.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(dialog.gameObject,"Assets/Resources/UI/LevelUpChoiceDialog.prefab");
        Object.DestroyImmediate(dialog.gameObject);
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
