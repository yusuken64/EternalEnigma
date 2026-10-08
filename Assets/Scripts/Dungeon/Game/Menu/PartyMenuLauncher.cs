using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenuLauncher : MonoBehaviour
{
    private Func<bool> ready;
    private Action<PartyMenuTab> open;
    private PartyMenu menu;
    private Canvas canvas;
    private int defaultSortingOrder;
    [SerializeField] private TMP_Text inventory,skills,equipment,stats;
    [SerializeField] private Button inventoryButton, skillsButton, equipmentButton, statsButton;
    private Button capabilitiesButton;
    private PartyMenuTab[] tabs;
    private bool? lastPad;
    private PartyMenuTab? lastTab;
    #if UNITY_EDITOR
    public static void AuthorLayout(Transform parent,Func<bool> ready,Action<PartyMenuTab> open)
    {
        var canvas=GameUISkin.Canvas("Party menu shortcuts",parent,85);
        var launcher=canvas.gameObject.AddComponent<PartyMenuLauncher>();launcher.canvas=canvas;launcher.ready=ready;
        var safe=GameUISkin.Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<SafeAreaPanel>();
        var inventory=GameUISkin.Button(safe,"Inventory",new Vector2(.46f,.935f),new Vector2(.58f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Inventory);});
        var equipment=GameUISkin.Button(safe,"Equipment",new Vector2(.59f,.935f),new Vector2(.71f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Equipment);});
        var skills=GameUISkin.Button(safe,"Skills",new Vector2(.72f,.935f),new Vector2(.82f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Skills);});
        var stats=GameUISkin.Button(safe,"Stats",new Vector2(.83f,.935f),new Vector2(.94f,.985f),()=>{if(ready() && MenuUIInputModule.Active?.HasDialog!=true)open(PartyMenuTab.Stats);});
        inventory.navigation=skills.navigation=equipment.navigation=stats.navigation=new Navigation{mode=Navigation.Mode.None};
        launcher.inventoryButton=inventory;launcher.skillsButton=skills;
        launcher.equipmentButton=equipment;launcher.statsButton=stats;
        launcher.inventory=inventory.GetComponentInChildren<TMP_Text>();launcher.skills=skills.GetComponentInChildren<TMP_Text>();
        launcher.equipment=equipment.GetComponentInChildren<TMP_Text>();launcher.stats=stats.GetComponentInChildren<TMP_Text>();
        launcher.ApplyAppearance();
    }
#endif
    public static void Create(Transform parent,Func<bool> ready,Action<PartyMenuTab> open,bool includeCapabilities=false)
    {
        var view=AuthoredUI.Require<PartyMenuLauncher>(parent);
        view.canvas=view.GetComponent<Canvas>(); view.ready=ready; view.open=open;
        view.defaultSortingOrder=view.canvas.sortingOrder;
        view.menu=AuthoredUI.Require<PartyMenu>(parent);
        if(view.equipmentButton==null){view.equipmentButton=Instantiate(view.skillsButton,view.skillsButton.transform.parent);view.equipmentButton.name="Equipment shortcut";view.equipment=view.equipmentButton.GetComponentInChildren<TMP_Text>();}
        if(view.statsButton==null){view.statsButton=Instantiate(view.skillsButton,view.skillsButton.transform.parent);view.statsButton.name="Stats shortcut";view.stats=view.statsButton.GetComponentInChildren<TMP_Text>();}
        if(includeCapabilities)
        {
            view.capabilitiesButton=Instantiate(view.skillsButton,view.skillsButton.transform.parent);
            view.capabilitiesButton.name="Capabilities shortcut";
        }
        view.tabs=includeCapabilities
            ? new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats,PartyMenuTab.Capabilities}
            : new[]{PartyMenuTab.Inventory,PartyMenuTab.Equipment,PartyMenuTab.Skills,PartyMenuTab.Stats};
        view.ApplyAppearance();
        for(int i=0;i<view.tabs.Length;i++)
        {
            var tab=view.tabs[i];
            var button=view.ButtonFor(tab);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>view.Activate(tab));
        }
        view.Refresh();
    }

    public void ApplyAppearance()
    {
        var buttons=new[]{inventoryButton,equipmentButton,skillsButton,statsButton,capabilitiesButton};
        for(int i=0;i<buttons.Length;i++)if(buttons[i]!=null)StyleTab(buttons[i],i);
    }

    private static void StyleTab(Button button,int index)
    {
        // Reserve the same space for each header, including overworld's fifth tab.
        var rect=(RectTransform)button.transform;
        rect.anchorMin=new Vector2(.40f+index*.115f,.935f);
        rect.anchorMax=new Vector2(.40f+(index+1)*.115f-.01f,.985f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        var theme=GameUITheme.Current;
        var background=(Image)button.targetGraphic;
        var role=background.GetComponent<DungeonUIRole>()??background.gameObject.AddComponent<DungeonUIRole>();
        role.Role=DungeonVisualRole.WoodButton;
        theme.StyleButton(button);
        theme.Surface(background,theme.DungeonWoodButton,1);
        background.preserveAspect=false;
        if(background.transform!=button.transform)
        {
            background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
        }
        var label=button.GetComponentInChildren<TMP_Text>();
        label.font=TMP_Settings.defaultFontAsset;label.fontStyle=FontStyles.Normal;label.color=GameUITheme.LightInk;
        label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=true;
        label.fontSize=26;label.fontSizeMin=16;label.fontSizeMax=26;label.textWrappingMode=TextWrappingModes.NoWrap;
        label.rectTransform.anchorMin=new Vector2(.04f,.1f);label.rectTransform.anchorMax=new Vector2(.96f,.9f);
        label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;label.margin=Vector4.zero;
        var textStyle=label.GetComponent<DungeonTextStyle>()??label.gameObject.AddComponent<DungeonTextStyle>();
        textStyle.OnWood=true;
    }

    public Button ButtonFor(PartyMenuTab tab) => tab switch
    {
        PartyMenuTab.Inventory=>inventoryButton,
        PartyMenuTab.Equipment=>equipmentButton,
        PartyMenuTab.Skills=>skillsButton,
        PartyMenuTab.Stats=>statsButton,
        PartyMenuTab.Capabilities=>capabilitiesButton,
        _=>null
    };

    private bool PartyOpen => menu!=null && menu.Owner!=null;
    private bool Available => ready!=null && Common.Instance!=null && !AutoplayRunner.BlocksPlayerInput &&
        !Common.Instance.GlobalSettings.IsOpen && !Common.Instance.Travel.IsTransitioning;
    private bool CanUseTabs => Available && (PartyOpen
        ? menu.IsRoot && MenuUIInputModule.Active?.OwnsFocus(menu)==true
        : ready() && MenuUIInputModule.Active?.HasDialog!=true);

    private void Activate(PartyMenuTab tab)
    {
        if(!CanUseTabs)return;
        if(PartyOpen)menu.SwitchTab(tab);
        else open(tab);
        Refresh();
    }

    public void CycleTab(int direction)
    {
        if(!PartyOpen || !CanUseTabs || direction==0)return;
        int index=Array.IndexOf(tabs,menu.Tab);
        Activate(tabs[(index+(direction>0?1:-1)+tabs.Length)%tabs.Length]);
    }

    public bool Allows(GameObject obj) => canvas!=null && canvas.enabled && CanUseTabs &&
        obj!=null && obj.transform.IsChildOf(transform);

    public void Refresh()
    {
        if(ready==null || tabs==null)return;
        bool partyOpen=PartyOpen;
        canvas.enabled=Available && (partyOpen || ready() && MenuUIInputModule.Active?.HasDialog!=true);
        // The same HUD buttons sit above the party menu's full-screen input shield.
        canvas.sortingOrder=partyOpen?menu.GetComponent<Canvas>().sortingOrder+1:defaultSortingOrder;
        bool interactable=CanUseTabs;
        foreach(var tab in tabs)
        {
            var button=ButtonFor(tab);
            button.interactable=interactable;
            if(!partyOpen)button.navigation=new Navigation{mode=Navigation.Mode.None};
        }
        bool pad=ControlDeviceState.Gamepad;
        PartyMenuTab? selected=partyOpen?menu.Tab:null;
        if(lastPad==pad && lastTab==selected)return;
        lastPad=pad;lastTab=selected;
        foreach(var tab in tabs)
        {
            string shortcut=tab==PartyMenuTab.Inventory?" ["+InputPrompts.Inventory+"]":
                tab==PartyMenuTab.Skills?" ["+InputPrompts.Skills+"]":"";
            var button=ButtonFor(tab);
            button.GetComponentInChildren<TMP_Text>().text=(selected==tab?"> ":"")+tab+shortcut;
            var colors=button.colors;colors.normalColor=selected==tab?GameUITheme.Selected:Color.white;button.colors=colors;
        }
    }
    private void Update()=>Refresh();
}
