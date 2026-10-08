using System;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class PartyMenuPicker : Dialog
{
    public Transform Rows;
    public TMP_Text Title;
    public Button BackButton;
    public AuthoredButton RowTemplate;
    public TMP_Text OptionDescription;
    private Button first;
    private bool committed;
    private int submittedFrame = -1;
    private TMP_Text description;
    #if UNITY_EDITOR
    public static PartyMenuPicker AuthorLayout(Transform parent, string title, List<(string Label, Action Execute)> choices,bool closeOnChoose=true)
    {
        var canvas = GameUISkin.Canvas("Party picker", parent, 120);
        GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=new Color(0,0,0,.12f);
        var picker = canvas.gameObject.AddComponent<PartyMenuPicker>();
        var panel = GameUISkin.Panel(canvas.transform,new Vector2(.2f,.2f),new Vector2(.7f,.78f));
        picker.Title=GameUISkin.Label(panel.transform,title,new Vector2(.05f,.85f),new Vector2(.95f,.98f),30);
        var viewport = GameUISkin.Rect("Choices",panel.transform,new Vector2(.05f,.2f),new Vector2(.95f,.84f));
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        picker.scrollView = viewport.gameObject.AddComponent<ScrollRect>();
        var content = GameUISkin.Rect("Rows",viewport,new Vector2(0,1),Vector2.one); content.pivot = new Vector2(.5f,1);
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childControlHeight=true; layout.childForceExpandHeight=false;
        layout.childControlWidth=true;layout.childForceExpandWidth=true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        picker.scrollView.content=content; picker.scrollView.viewport=viewport; picker.scrollView.horizontal=false;
        picker.Rows=content;
        foreach (var choice in choices)
        {
            var button=GameUISkin.Button(content,choice.Label,Vector2.zero,Vector2.one,()=>
            {
                if (picker.committed || picker.Owner?.Current != picker) return;
                picker.committed=closeOnChoose;
                if(closeOnChoose)picker.CloseDialog();
                Common.Instance.MenuInputHandler.ClearInputThisFrame();choice.Execute();
            });
            button.gameObject.AddComponent<LayoutElement>().preferredHeight=64;
            button.gameObject.AddComponent<PartyMenuRow>().Selected=()=>picker.ScrollToSelected(button.gameObject);
            if(picker.first==null)picker.first=button;
        }
        var back=picker.BackButton=GameUISkin.Button(panel.transform,"Back",new Vector2(.3f,.035f),new Vector2(.7f,.16f),picker.CloseDialog);
        if(picker.first==null)picker.first=back;
        return picker;
    }
#endif
    public static PartyMenuPicker Build(Transform parent,string title,List<(string Label,Action Execute)> choices,bool closeOnChoose=true,bool showBackButton=true)
        => BuildDetailed(parent,title,choices.Select(c=>(c.Label,(string)null,c.Execute)).ToList(),closeOnChoose,showBackButton);

    public static PartyMenuPicker BuildDetailed(Transform parent,string title,List<(string Label,string Description,Action Execute)> choices,bool closeOnChoose=true,bool showBackButton=true)
    {
        var picker=parent.GetComponentsInChildren<PartyMenuPicker>(true).FirstOrDefault(p=>p.Owner==null);
        if(picker==null)throw new InvalidOperationException("No free authored party picker for this dialog stack.");
        picker.UseGameplayDock(picker.Title.transform.parent);
        picker.BackButton.gameObject.SetActive(showBackButton);
        foreach(var close in picker.Title.transform.parent.GetComponentsInChildren<DungeonDialogClose>(true))
            close.gameObject.SetActive(showBackButton);
        var viewport=(RectTransform)picker.scrollView.transform;
        Fit(viewport,viewport.anchorMin.x,showBackButton ? .2f : .035f,viewport.anchorMax.x,viewport.anchorMax.y);
        picker.committed=false;picker.submittedFrame=-1;picker.Title.text=title;picker.Title.enableAutoSizing=true;picker.Title.fontSizeMin=18;picker.Title.fontSizeMax=30;picker.first=null;picker.description=null;
        if(picker.OptionDescription!=null)
        {
            Fit(picker.OptionDescription.transform.parent,.285f,.22f,.59f,.78f);
            picker.OptionDescription.transform.parent.gameObject.SetActive(choices.Any(c=>!string.IsNullOrEmpty(c.Description)));
            picker.OptionDescription.gameObject.SetActive(true);
            picker.OptionDescription.text="Select an option for its stat preview.";
        }
        foreach(Transform child in picker.Rows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var buttons = new List<Button>();
        foreach(var choice in choices)
        {
            var row=picker.RowTemplate.Spawn(picker.Rows,choice.Label,()=> {
                if(picker.committed || picker.Owner?.Current!=picker || picker.submittedFrame==Time.frameCount || choice.Execute==null)return;
                picker.submittedFrame=Time.frameCount;
                picker.committed=closeOnChoose;if(closeOnChoose)picker.CloseDialog();
                Common.Instance.MenuInputHandler.ClearInputThisFrame();choice.Execute();
            });
            row.Button.GetComponent<PartyMenuRow>().Selected=()=>{picker.ScrollToSelected(row.gameObject);if(picker.OptionDescription!=null)picker.OptionDescription.text=choice.Description??"";};
            row.Button.interactable=choice.Execute!=null;
            if(row.Button.interactable)buttons.Add(row.Button);
            if(picker.first==null && row.Button.interactable)picker.first=row.Button;
        }
        picker.BackButton.onClick.RemoveAllListeners();picker.BackButton.onClick.AddListener(picker.CloseDialog);
        if(showBackButton)
        {
            if(picker.first==null)picker.first=picker.BackButton;
            buttons.Add(picker.BackButton);
        }
        for(int i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=buttons[(i+buttons.Count-1)%buttons.Count],selectOnDown=buttons[(i+1)%buttons.Count]};
        picker.scrollView.verticalNormalizedPosition=1;
        return picker;
    }
    public void Description(string text)
    {
        var label=description=Instantiate(Title,Rows);
        label.name="Interaction description";
        label.text=text+"\n\n<size=18>Up / Down: scroll   |   Right: actions</size>"; label.fontSize=24; label.enableAutoSizing=false;
        label.textWrappingMode=TextWrappingModes.Normal;
        label.transform.SetAsFirstSibling();
        label.gameObject.AddComponent<LayoutElement>();
        var read=label.gameObject.AddComponent<TrainerPreviewScroll>();
        read.Scroll=scrollView;
        read.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=BackButton.gameObject.activeSelf?BackButton:null,selectOnRight=first};
        foreach(var button in Rows.GetComponentsInChildren<Button>().Append(BackButton).Where(b=>b!=read && b.gameObject.activeSelf))
        {var nav=button.navigation;nav.selectOnLeft=read;button.navigation=nav;}
        first=read;
        label.gameObject.SetActive(true);
    }
    internal override void SetFirstSelect()
    {
        Canvas.ForceUpdateCanvases();
        float width=Mathf.Max(200,((RectTransform)Rows).rect.width);
        if(description!=null)description.GetComponent<LayoutElement>().preferredHeight=description.GetPreferredValues(description.text,width,0).y+24;
        foreach(var row in Rows.GetComponentsInChildren<AuthoredButton>())
        {
            var label=row.GetComponentInChildren<TMP_Text>();
            var layout=row.GetComponent<LayoutElement>();
            if(layout!=null && label!=null)layout.preferredHeight=Mathf.Max(64,label.GetPreferredValues(label.text,width-40,0).y+24);
        }
        Canvas.ForceUpdateCanvases();
        scrollView.verticalNormalizedPosition=1;
        first?.Select();
    }
}
