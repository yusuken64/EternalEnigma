using System;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenu : Dialog
{
    public AuthoredButton HeroTemplate, EntryTemplate, PlainEntryTemplate;
    public TMP_Text HeadingTemplate, EmptyTemplate;
    public RectTransform Panel;
    public Transform HeroesRoot, RowsRoot;
    public TMP_Text Details, Hints, HeroText;
    public Button InventoryTab, SkillsTab, EquipmentTab, StatsTab, BackButton;
    private Button capabilitiesTab;
    public PartyMenuLauncher HudTabs { get; private set; }
    public TrainerPreviewScroll DetailsControl;
    public IReadOnlyList<Button> EntryButtons => rows;
    private IPartyMenuContext context;
    private List<PartyMenuEntry> entries = new();
    private readonly List<Button> rows = new();
    private readonly Dictionary<(string, PartyMenuTab), ViewState> states = new();
    private sealed class ViewState { public object Identity; public int Index; public float Scroll = 1; }
    private int heroIndex, selectedIndex;
    private float? listSpacing;
    private string result;
    public PartyMenuTab Tab { get; private set; }
    public bool IsRoot => Owner?.Current == this;
    public PartyMenuHero Hero => context != null && context.Heroes.Count > 0 ? context.Heroes[heroIndex] : null;

    public static PartyMenu Create(Transform parent)
    {
        return AuthoredUI.Require<PartyMenu>(parent);
    }

    public void Setup(IPartyMenuContext source, PartyMenuTab tab, string heroId = null)
    {
        if (context != source) { context?.Dispose(); context = source; states.Clear(); }
        heroIndex = Math.Max(0, context.Heroes.ToList().FindIndex(h => h.Id == heroId));
        Tab = tab; result = null;
        UseHudTabs();
        Refresh();
    }
    private void UseHudTabs()
    {
        if(HudTabs!=null)return;
        HudTabs=AuthoredUI.Require<PartyMenuLauncher>(transform);
        // Replace the authored in-panel tabs with the scene's persistent HUD buttons.
        foreach(var button in new[]{InventoryTab,EquipmentTab,SkillsTab,StatsTab})
            if(button!=null)button.gameObject.SetActive(false);
        UseGameplayDock(Panel);
        HeroesRoot.gameObject.SetActive(false);
        HeroText.gameObject.SetActive(false);
        DetailsControl.Scroll.gameObject.SetActive(false);
        Hints.gameObject.SetActive(false);
        foreach(var label in Panel.GetComponentsInChildren<TMP_Text>(true))
            if(label.transform.parent==Panel)label.gameObject.SetActive(false);
        foreach(var name in new[]{"Description parchment","Heading ribbon"})
            Panel.Find(name)?.gameObject.SetActive(false);
        BackButton.gameObject.SetActive(false);
        foreach(var close in Panel.GetComponentsInChildren<DungeonDialogClose>(true))
            close.gameObject.SetActive(false);
        Fit(scrollView.transform,.035f,.025f,.965f,.975f);
        InventoryTab=HudTabs.ButtonFor(PartyMenuTab.Inventory);
        EquipmentTab=HudTabs.ButtonFor(PartyMenuTab.Equipment);
        SkillsTab=HudTabs.ButtonFor(PartyMenuTab.Skills);
        StatsTab=HudTabs.ButtonFor(PartyMenuTab.Stats);
        capabilitiesTab=HudTabs.ButtonFor(PartyMenuTab.Capabilities);
    }
    public void Shortcut(PartyMenuTab tab)
    {
        if (!IsRoot) return;
        if (Tab == tab) CloseDialog(); else SwitchTab(tab);
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void SwitchTab(PartyMenuTab tab)
    {
        if (!IsRoot || tab == Tab || (tab == PartyMenuTab.Capabilities && context is not OverworldPartyMenuContext)) return;
        Remember(); Tab = tab; result = null; Refresh(); SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void BrowseHero(int delta)
    {
        if (!IsRoot || context.Heroes.Count == 0) return;
        Remember();
        if(context is DungeonPartyMenuContext)
        {
            Game.Instance.PlayerController.CycleControlledAlly(delta);
            heroIndex=Math.Max(0,context.Heroes.ToList().FindIndex(h=>h.DungeonActor==Game.Instance.PlayerController.ControlledAlly));
        }
        else heroIndex = (heroIndex + delta + context.Heroes.Count) % context.Heroes.Count;
        result = null; Refresh(); SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    private void Update()
    {
        var input = MenuUIInputModule.Active;
        if (!IsRoot || input == null || input.InputConsumed || Common.Instance.GlobalSettings.IsOpen) return;
        if(context is DungeonPartyMenuContext && Hero?.DungeonActor!=Game.Instance.PlayerController.ControlledAlly)
        {
            Remember();heroIndex=Math.Max(0,context.Heroes.ToList().FindIndex(h=>h.DungeonActor==Game.Instance.PlayerController.ControlledAlly));
            Refresh();SetFirstSelect();
        }
        if (input.NextHero.WasPressedThisFrame()) BrowseHero(UnityEngine.InputSystem.Keyboard.current?.shiftKey.isPressed == true ? -1 : 1);
        else if (input.PreviousHero.WasPressedThisFrame()) BrowseHero(-1);
    }
    private void Remember()
    {
        if (Hero == null) return;
        states[(Hero.Id, Tab)] = new ViewState { Identity = entries.Count > selectedIndex ? entries[selectedIndex].Identity : null,
            Index = selectedIndex, Scroll = scrollView.verticalNormalizedPosition };
    }
    public void Refresh()
    {
        StopAutoScroll();scrollView.StopMovement();
        Clear(HeroesRoot); Clear(RowsRoot); rows.Clear();
        bool compactStats=Tab==PartyMenuTab.Stats;
        var layout=RowsRoot.GetComponent<VerticalLayoutGroup>();
        listSpacing ??= layout.spacing;
        layout.spacing=compactStats?2:listSpacing.Value;
        // Stats fills the viewport; other tabs retain their scrolling lists.
        RowsRoot.GetComponent<ContentSizeFitter>().enabled=!compactStats;
        Fit(RowsRoot,0,compactStats?0:1,1,1);
        scrollView.vertical=!compactStats;
        HudTabs.Refresh();
        entries = Hero == null ? new() : context.Entries(Hero, Tab);
        ViewState state = null;
        if (Hero != null) states.TryGetValue((Hero.Id,Tab), out state);
        int identityIndex = state == null ? -1 : entries.FindIndex(e => ReferenceEquals(e.Identity, state.Identity));
        selectedIndex = identityIndex >= 0 ? identityIndex : Mathf.Clamp(state?.Index ?? selectedIndex, 0, Math.Max(0, entries.Count-1));
        string section = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i]; int index = i;
            if (section != entry.Section)
            {
                section = entry.Section;
                var heading=Instantiate(HeadingTemplate,RowsRoot);heading.text=section;heading.gameObject.SetActive(true);
                if(compactStats)FitStatRow(heading.gameObject,heading,.7f,20);
            }
            var row=(entry.Icon!=null?EntryTemplate:PlainEntryTemplate).Spawn(RowsRoot,entry.Title,()=>OpenActions(index),entry.Icon);
            if(compactStats)
            {
                Fit(row.Label.transform,.035f,0,.965f,1);
                FitStatRow(row.gameObject,row.Label,1,24);
            }
            var button=row.Button;
            button.GetComponent<PartyMenuRow>().Selected = () => { selectedIndex = index; ShowDetails(); ScrollToSelected(button.gameObject); };
            rows.Add(button);
        }
        if(entries.Count==0) {var empty=Instantiate(EmptyTemplate,RowsRoot);empty.text=Tab==PartyMenuTab.Inventory?"The bag is empty.":Tab==PartyMenuTab.Capabilities?"No acquired capabilities.":"No learned skills.";empty.gameObject.SetActive(true);}
        Canvas.ForceUpdateCanvases(); scrollView.verticalNormalizedPosition = compactStats?1:state?.Scroll ?? 1;
        WireNavigation();
        ShowDetails();
    }
    private static void FitStatRow(GameObject row,TMP_Text label,float heightWeight,float maxFontSize)
    {
        var size=row.GetComponent<LayoutElement>();
        size.minHeight=0;size.preferredHeight=0;size.flexibleHeight=heightWeight;
        label.enableAutoSizing=true;label.fontSize=maxFontSize;label.fontSizeMin=14;label.fontSizeMax=maxFontSize;
        label.textWrappingMode=TextWrappingModes.NoWrap;label.margin=new Vector4(4,0,4,0);
    }
    private void WireNavigation()
    {
        var tab=Tab==PartyMenuTab.Inventory?InventoryTab:Tab==PartyMenuTab.Equipment?EquipmentTab:Tab==PartyMenuTab.Stats?StatsTab:Tab==PartyMenuTab.Capabilities?capabilitiesTab:SkillsTab;
        Selectable firstRow=rows.Count>0?rows[0]:tab;
        for(int i=0;i<rows.Count;i++)rows[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=i>0?rows[i-1]:tab,selectOnDown=i+1<rows.Count?rows[i+1]:null};
        InventoryTab.navigation=EquipmentTab.navigation=SkillsTab.navigation=StatsTab.navigation=
            new Navigation {mode=Navigation.Mode.Explicit,selectOnDown=firstRow};
        if(capabilitiesTab!=null && capabilitiesTab.gameObject.activeSelf)
            capabilitiesTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnDown=firstRow};
    }
    private void ShowDetails()
    {
        HeroText.text = Hero == null ? "No party members" : context.HeroDetails(Hero);
        if (entries.Count == 0)
        {
            Details.text = context is DungeonPartyMenuContext ?
                "No entries for this character. Switch tabs, or click a HUD portrait to change characters." : "Select a hero or switch tabs.";
            return;
        }
        var entry = entries[selectedIndex];
        var actions = Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment ? new List<PartyMenuAction>() : context.Actions(Hero,entry);
        string availability = string.Join("\n", actions.Select(a => a.Available ? a.Label : a.Label+": "+a.UnavailableReason));
        string restriction=Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment ? "" : context.Restriction(Hero,entry);
        Details.text = $"<b>{entry.Title}</b>\n\n{entry.Description}\n\n{restriction}\n{availability}\n\n{result}";
        Details.rectTransform.anchoredPosition = Vector2.zero;
    }
    private void OpenActions(int index)
    {
        if (!IsRoot) return;
        selectedIndex=index; Remember(); ShowDetails();
        var entry = entries[index];
        if(entry.SpendAttribute)
        {
            var dialog=LevelUpChoiceDialog.Build(transform.parent,Hero,attribute=>((PartyMenuContext)context).Spend(Hero,attribute),
                ()=>Complete("Attribute points updated."));
            Owner.Open(dialog);return;
        }
        if(entry.Slot.HasValue)
        {
            var options=((PartyMenuContext)context).EquipmentOptions(Hero,entry.Slot.Value)
                .Select(option=>(option.Label+(option.Enabled?"":"  "+option.Description),option.Description,(Action)(option.Enabled?()=>{
                    var itemEntry=new PartyMenuEntry{Item=option.Item,Equipped=Hero.Equipment.IsEquipped(option.Item)};
                    var action=context.Actions(Hero,itemEntry).FirstOrDefault(a=>a.Available);
                    action?.Execute(this);
                }:null))).ToList();
            PickDetailed("Change "+entry.Title,options);return;
        }
        if (context is IPartyMenuEntryHandler handler && handler.OpenEntry(this, Hero, entry)) return;
        var actions = entry.Slot.HasValue || entry.SpendAttribute ? new List<PartyMenuAction>() : context.Actions(Hero,entry);
        if (!actions.Any(a=>a.Available)) return;
        if (entry.Skill?.ActivationType == ActivationType.Active && actions.Count == 1)
        {
            Common.Instance.MenuInputHandler.ClearInputThisFrame();
            actions[0].Execute(this);
            return;
        }
        Pick(entry.Title, actions.Where(a=>a.Available).Select(a => (a.Label, (Action)(() => a.Execute(this)))).ToList(),false);
    }
    public void Pick(string title, List<(string Label, Action Execute)> options,bool closeOnChoose=true)
    {
        var picker = PartyMenuPicker.Build(transform.parent, title, options,closeOnChoose,showBackButton:false);
        picker.CloseAction = null;
        Owner.Open(picker);
    }
    public void PickDetailed(string title,List<(string Label,string Description,Action Execute)> options)
    {
        var picker=PartyMenuPicker.BuildDetailed(transform.parent,title,options,showBackButton:false);
        picker.CloseAction=null;Owner.Open(picker);
    }
    public void Complete(string message)
    {
        while(Owner!=null && Owner.Current!=this)Owner.Close(Owner.Current);
        result = message; Refresh(); SetFirstSelect();
    }
    internal override void SetFirstSelect()
    {
        if (rows.Count > 0) rows[Mathf.Clamp(selectedIndex,0,rows.Count-1)].Select();
        else (Tab == PartyMenuTab.Inventory ? InventoryTab : Tab == PartyMenuTab.Equipment ? EquipmentTab : Tab == PartyMenuTab.Stats ? StatsTab : Tab == PartyMenuTab.Capabilities ? capabilitiesTab : SkillsTab).Select();
    }
    private static void Clear(Transform parent)
    {
        while(parent.childCount>0)
        {
            var child=parent.GetChild(0);
            child.gameObject.SetActive(false);child.SetParent(null);Destroy(child.gameObject);
        }
    }
    private void OnDestroy() => context?.Dispose();
}
