using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DungeonThemeExplorerAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Art/Install Dungeon Theme Explorer")]
    public static void InstallCurrent()
    {
        var playground=UnityEngine.Object.FindFirstObjectByType<EnvironmentPlayground>();
        if(playground==null) throw new InvalidOperationException("Open EnvironmentPlayground first.");
        Install(playground);playground.ShowGallery();EditorSceneManager.MarkSceneDirty(playground.gameObject.scene);EditorSceneManager.SaveScene(playground.gameObject.scene);
    }
    public static void Install(EnvironmentPlayground p)
    {
        var explorer=p.DungeonExplorer;
        if(explorer==null)
        {
            var host=new GameObject("Dungeon theme explorer");host.transform.SetParent(p.transform,false);
            explorer=host.AddComponent<DungeonThemeExplorer>();explorer.Creator=host.AddComponent<TileWorldCreator>();p.DungeonExplorer=explorer;
        }
        explorer.Playground=p;explorer.RegularTemplate=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonAsset.asset");
        explorer.ThroneTemplate=AssetDatabase.LoadAssetAtPath<TileWorldCreatorAsset>("Assets/Prefabs/Dungeon/DungeonThroneAsset.asset");
        explorer.Catalog=Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog");explorer.Creator.twcAsset=explorer.RegularTemplate;
        var bar=p.Status.transform.parent;
        if(!bar.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Dungeon themes"))
        {
            var button=GameUISkin.Button(bar,"Dungeon themes",Vector2.zero,Vector2.one,null);button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,p.ShowDungeon);
        }
        var navigation=bar.GetComponentsInChildren<Button>(true);
        for(int i=0;i<navigation.Length;i++)
        {
            var rect=(RectTransform)navigation[i].transform;rect.anchorMin=new Vector2(.01f+i*.123f,.48f);rect.anchorMax=new Vector2(.129f+i*.123f,.96f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        if(explorer.Controls!=null) UnityEngine.Object.DestroyImmediate(explorer.Controls);
        var panel=GameUISkin.Panel(bar.parent,new Vector2(0,.73f),new Vector2(1,.90f));panel.name="Dungeon theme controls";explorer.Controls=panel.gameObject;
        explorer.Summary=GameUISkin.Label(panel.transform,"Dungeon theme explorer",new Vector2(.01f,.74f),new Vector2(.99f,.98f),24);
        for(int i=0;i<8;i++)
        {
            var button=GameUISkin.Button(panel.transform,((OverworldBiome)i).ToString(),new Vector2(.01f+i*.123f,.41f),new Vector2(.129f+i*.123f,.71f),null);
            button.onClick.RemoveAllListeners();UnityEventTools.AddIntPersistentListener(button.onClick,explorer.SelectBiome,i);
        }
        Button Control(string label,float min,float max,UnityEngine.Events.UnityAction action)
        {
            var button=GameUISkin.Button(panel.transform,label,new Vector2(min,.06f),new Vector2(max,.35f),null);
            button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,action);return button;
        }
        explorer.EnvironmentLabel=Control("Interior",.01f,.16f,explorer.ToggleEnvironment).GetComponentInChildren<TMP_Text>();
        explorer.LayoutLabel=Control("Regular floor",.17f,.32f,explorer.ToggleLayout).GetComponentInChildren<TMP_Text>();
        GameUISkin.Label(panel.transform,"Seed",new Vector2(.34f,.06f),new Vector2(.395f,.34f),24);
        var inputPanel=GameUISkin.Panel(panel.transform,new Vector2(.40f,.06f),new Vector2(.50f,.35f));inputPanel.color=new Color(.16f,.19f,.21f,1);
        var input=inputPanel.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=inputPanel;
        var text=GameUISkin.Label(inputPanel.transform,p.Seed.ToString(),new Vector2(.06f,.05f),new Vector2(.94f,.95f),24);
        input.textViewport=(RectTransform)inputPanel.transform;input.textComponent=text;input.contentType=TMP_InputField.ContentType.IntegerNumber;
        UnityEventTools.AddPersistentListener(input.onEndEdit,explorer.SetSeed);explorer.SeedInput=input;
        Control("Next seed",.51f,.63f,explorer.NextSeed);Control("Rebuild",.64f,.78f,explorer.Rebuild);Control("Frame dungeon",.79f,.99f,explorer.Frame);
        explorer.enabled=false;explorer.enabled=true;explorer.SetVisible(false);
        EditorUtility.SetDirty(p);EditorUtility.SetDirty(explorer);
    }
    [MenuItem("Tools/Eternal Enigma/Art/Preview Dungeon Themes")]
    public static void Preview()=>UnityEngine.Object.FindFirstObjectByType<EnvironmentPlayground>().ShowDungeon();
}

[CustomEditor(typeof(DungeonThemeExplorer))]
public sealed class DungeonThemeExplorerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var explorer=(DungeonThemeExplorer)target;
        using(new EditorGUI.DisabledScope(explorer.IsBuilding))
        {
            if(GUILayout.Button("Show / rebuild dungeon")) {explorer.Playground.ShowDungeon();if(explorer.IsReady)explorer.Rebuild();}
            if(GUILayout.Button("Frame dungeon")) explorer.Frame();
        }
    }
}
