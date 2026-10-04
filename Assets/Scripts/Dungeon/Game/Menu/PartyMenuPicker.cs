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
    private Button first;
    private bool committed;
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
    public static PartyMenuPicker Build(Transform parent,string title,List<(string Label,Action Execute)> choices,bool closeOnChoose=true)
    {
        var picker=parent.GetComponentsInChildren<PartyMenuPicker>(true).FirstOrDefault(p=>p.Owner==null);
        if(picker==null)throw new InvalidOperationException("No free authored party picker for this dialog stack.");
        picker.committed=false;picker.Title.text=title;picker.first=null;
        foreach(Transform child in picker.Rows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        foreach(var choice in choices)
        {
            var row=picker.RowTemplate.Spawn(picker.Rows,choice.Label,()=> {
                if(picker.committed || picker.Owner?.Current!=picker)return;
                picker.committed=closeOnChoose;if(closeOnChoose)picker.CloseDialog();
                Common.Instance.MenuInputHandler.ClearInputThisFrame();choice.Execute();
            });
            row.Button.GetComponent<PartyMenuRow>().Selected=()=>picker.ScrollToSelected(row.gameObject);
            if(picker.first==null)picker.first=row.Button;
        }
        picker.BackButton.onClick.RemoveAllListeners();picker.BackButton.onClick.AddListener(picker.CloseDialog);
        if(picker.first==null)picker.first=picker.BackButton;
        return picker;
    }
    internal override void SetFirstSelect()=>first.Select();
}
