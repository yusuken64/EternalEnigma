using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AutoplayPanelAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Diorama/Build Autoplay UI")]
    public static void Build()
    {
        var canvas=GameUISkin.Canvas("Autoplay controls",null,300);
        try
        {
            var ui=canvas.gameObject.AddComponent<AutoplayPanel>();
            var panel=GameUISkin.Panel(canvas.transform,Vector2.one,Vector2.one).rectTransform;
            panel.pivot=Vector2.one;panel.anchoredPosition=new Vector2(-18,-76);panel.sizeDelta=new Vector2(450,640);ui.Expanded=panel;
            ui.Title=Label(panel,"Autoplay",.89f,.97f,32);ui.Title.font=GameUITheme.Current.HeadingFont;
            ui.Status=Label(panel,"Starting",.69f,.86f,23);ui.Status.enableAutoSizing=true;ui.Status.fontSizeMin=18;ui.Status.fontSizeMax=23;
            ui.Counts=Label(panel,"Actions: 0   Turns: 0",.62f,.69f,23);
            ui.Playback=Label(panel,"Playback: 4×",.55f,.62f,24);
            ui.Speeds=AutoplayPanel.Rates.Select((r,i)=>Button(panel,r.ToString("0.#")+"×",.05f+(i%4)*.225f,.47f-(i/4)*.075f,.25f+(i%4)*.225f,.535f-(i/4)*.075f)).ToArray();
            ui.Animations=Button(panel,"Animations: Standard",.05f,.31f,.95f,.38f);ui.AnimationLabel=ui.Animations.GetComponentInChildren<TMP_Text>();
            ui.Pause=Button(panel,"Pause",.05f,.23f,.48f,.30f);ui.PauseLabel=ui.Pause.GetComponentInChildren<TMP_Text>();
            ui.Hide=Button(panel,"Hide · F8",.52f,.23f,.95f,.30f);
            ui.Stop=Button(panel,"Stop and report",.05f,.15f,.95f,.22f);
            ui.Return=Button(panel,"Stop autoplay…",.05f,.07f,.95f,.14f);
            ui.Footer=Label(panel,"",.025f,.065f,16);
            var collapsed=GameUISkin.Rect("Collapsed",canvas.transform,Vector2.one,Vector2.one);collapsed.pivot=Vector2.one;collapsed.anchoredPosition=new Vector2(-18,-76);collapsed.sizeDelta=new Vector2(210,55);ui.Collapsed=collapsed;
            ui.Show=Button(collapsed,"Autoplay · F8",0,0,1,1);
            var prompt=GameUISkin.Panel(canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f)).rectTransform;prompt.sizeDelta=new Vector2(560,370);ui.Prompt=prompt;
            var heading=Label(prompt,"Stop autoplay?",.76f,.92f,34);heading.font=GameUITheme.Current.HeadingFont;
            Label(prompt,"Your saved game is unchanged.",.61f,.76f,24);
            ui.TakeControl=Button(prompt,"Take control · T / X",.07f,.42f,.93f,.57f);
            ui.Menu=Button(prompt,"Return to main menu",.07f,.24f,.93f,.39f);
            ui.KeepWatching=Button(prompt,"Keep watching",.07f,.06f,.93f,.21f);
            prompt.gameObject.SetActive(false);collapsed.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,"Assets/Resources/UI/AutoplayPanel.prefab");
        }
        finally {Object.DestroyImmediate(canvas.gameObject);}
        AssetDatabase.SaveAssets();
    }
    static TMP_Text Label(Transform parent,string text,float bottom,float top,float size)
    {var label=GameUISkin.Label(parent,text,new Vector2(.06f,bottom),new Vector2(.94f,top),size);label.alignment=TextAlignmentOptions.MidlineLeft;return label;}
    static Button Button(Transform parent,string text,float left,float bottom,float right,float top)
    {var button=GameUISkin.Button(parent,text,new Vector2(left,bottom),new Vector2(right,top),null);var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=24;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=24;return button;}
}
