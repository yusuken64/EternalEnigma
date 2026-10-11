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
            panel.pivot=Vector2.one;panel.anchoredPosition=new Vector2(-18,-76);panel.sizeDelta=new Vector2(510,820);ui.Expanded=panel;
            ui.Title=Label(panel,"Autoplay",.92f,.98f,32);ui.Title.font=GameUITheme.Current.HeadingFont;
            ui.Status=Label(panel,"Starting",.78f,.91f,23);ui.Status.enableAutoSizing=true;ui.Status.fontSizeMin=18;ui.Status.fontSizeMax=23;
            ui.Counts=Label(panel,"Run 1/1 · Seed 42\nActions: 0   Turns: 0",.70f,.78f,23);
            ui.Playback=Label(panel,"Playback: 4×",.64f,.70f,24);
            ui.Speeds=AutoplayPanel.Rates.Select((r,i)=>Button(panel,r.ToString("0.#")+"×",.05f+(i%4)*.225f,.55f-(i/4)*.075f,.25f+(i%4)*.225f,.615f-(i/4)*.075f)).ToArray();
            ui.Animations=Button(panel,"Animations: Standard",.05f,.40f,.95f,.465f);ui.AnimationLabel=ui.Animations.GetComponentInChildren<TMP_Text>();
            ui.Pause=Button(panel,"Pause",.05f,.325f,.48f,.39f);ui.PauseLabel=ui.Pause.GetComponentInChildren<TMP_Text>();
            ui.Hide=Button(panel,"Hide · F8",.52f,.325f,.95f,.39f);
            ui.Stop=Button(panel,"Stop and report",.05f,.25f,.95f,.315f);
            ui.Return=Button(panel,"Stop autoplay…",.05f,.175f,.95f,.24f);
            ui.Footer=Label(panel,"",.02f,.16f,16);ui.Footer.overflowMode=TextOverflowModes.Ellipsis;
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
        BuildSetup();
    }

    private static void BuildSetup()
    {
        var canvas=GameUISkin.Canvas("Debug autoplay setup",null,300);
        try
        {
            var blocker=canvas.gameObject.AddComponent<Image>();blocker.color=new Color(0,0,0,.45f);
            var ui=canvas.gameObject.AddComponent<AutoplaySetup>();
            var panel=GameUISkin.Panel(canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f)).rectTransform;
            panel.sizeDelta=new Vector2(760,820);
            var title=Label(panel,"Debug autoplay",.89f,.97f,38);title.font=GameUITheme.Current.HeadingFont;
            Label(panel,"Starting seed",.80f,.87f,25);ui.Seed=Input(panel,"Starting seed",.80f,.87f,"42");
            Label(panel,"Run count",.70f,.77f,25);ui.RunCount=Input(panel,"Run count",.70f,.77f,"1");
            ui.Speed=Button(panel,"Speed: 1×",.06f,.60f,.94f,.67f);ui.SpeedLabel=ui.Speed.GetComponentInChildren<TMP_Text>();
            ui.Godmode=Button(panel,"Godmode + infinite strength: On",.06f,.51f,.94f,.58f);ui.GodmodeLabel=ui.Godmode.GetComponentInChildren<TMP_Text>();
            ui.InfiniteResources=Button(panel,"Infinite resources: On",.06f,.42f,.94f,.49f);ui.ResourcesLabel=ui.InfiniteResources.GetComponentInChildren<TMP_Text>();
            ui.Coverage=Button(panel,"Item and ability coverage: On",.06f,.33f,.94f,.40f);ui.CoverageLabel=ui.Coverage.GetComponentInChildren<TMP_Text>();
            ui.Validation=Label(panel,"",.18f,.31f,23);
            ui.Back=Button(panel,"Back",.06f,.06f,.46f,.15f);
            ui.Launch=Button(panel,"Start autoplay",.50f,.06f,.94f,.15f);
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,"Assets/Resources/UI/AutoplaySetup.prefab");
        }
        finally {Object.DestroyImmediate(canvas.gameObject);}
        AssetDatabase.SaveAssets();
    }

    private static TMP_InputField Input(Transform parent,string name,float bottom,float top,string value)
    {
        var panel=GameUISkin.Panel(parent,new Vector2(.53f,bottom),new Vector2(.94f,top));panel.name=name;
        panel.sprite=GameUITheme.Current.Button;panel.type=Image.Type.Sliced;
        var field=panel.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=panel;
        var viewport=GameUISkin.Rect("Viewport",panel.transform,new Vector2(.12f,.08f),new Vector2(.88f,.92f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text=GameUISkin.Label(viewport,value,Vector2.zero,Vector2.one,28);text.alignment=TextAlignmentOptions.MidlineLeft;
        field.textViewport=viewport;field.textComponent=text;field.contentType=TMP_InputField.ContentType.IntegerNumber;
        field.characterLimit=11;field.text=value;
        return field;
    }
    static TMP_Text Label(Transform parent,string text,float bottom,float top,float size)
    {var label=GameUISkin.Label(parent,text,new Vector2(.06f,bottom),new Vector2(.94f,top),size);label.alignment=TextAlignmentOptions.MidlineLeft;return label;}
    static Button Button(Transform parent,string text,float left,float bottom,float right,float top)
    {var button=GameUISkin.Button(parent,text,new Vector2(left,bottom),new Vector2(right,top),null);var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=24;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=24;return button;}
}
