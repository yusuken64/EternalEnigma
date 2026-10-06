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
    public TrainerPreviewScroll DetailsControl;
    public IReadOnlyList<Button> EntryButtons => rows;
    private IPartyMenuContext context;
    private List<PartyMenuEntry> entries = new();
    private readonly List<Button> rows = new();
    private readonly Dictionary<(string, PartyMenuTab), ViewState> states = new();
    private sealed class ViewState { public object Identity; public int Index; public float Scroll = 1; }
    private int heroIndex, selectedIndex;
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
        EnsureTabs();
        if (context != source) { context?.Dispose(); context = source; states.Clear(); }
        heroIndex = Math.Max(0, context.Heroes.ToList().FindIndex(h => h.Id == heroId));
        Tab = tab; result = null;
        if(source is OverworldPartyMenuContext && capabilitiesTab==null)
        {
            capabilitiesTab=Instantiate(SkillsTab,SkillsTab.transform.parent);
            capabilitiesTab.name="Capabilities tab";
            var rect=(RectTransform)capabilitiesTab.transform;
            var inventoryRect=(RectTransform)InventoryTab.transform;
            var skillsRect=(RectTransform)SkillsTab.transform;
            float left=inventoryRect.anchorMin.x, right=skillsRect.anchorMax.x;
            float width=(right-left)/3;
            inventoryRect.anchorMax=new Vector2(left+width,inventoryRect.anchorMax.y);
            skillsRect.anchorMin=new Vector2(left+width,skillsRect.anchorMin.y);
            skillsRect.anchorMax=new Vector2(left+width*2,skillsRect.anchorMax.y);
            rect.anchorMin=new Vector2(left+width*2,rect.anchorMin.y);
            rect.anchorMax=new Vector2(right,rect.anchorMax.y);
            capabilitiesTab.onClick.RemoveAllListeners();
            capabilitiesTab.onClick.AddListener(()=>SwitchTab(PartyMenuTab.Capabilities));
        }
        if(capabilitiesTab!=null)capabilitiesTab.gameObject.SetActive(source is OverworldPartyMenuContext);
        LayoutTabs();
        if(source is not DungeonPartyMenuContext)
        {
            var summary=HeroText.rectTransform;
            if(summary.parent!=Panel)summary.SetParent(Panel,false);
            summary.anchorMin=new Vector2(.36f,.82f);summary.anchorMax=new Vector2(.94f,.96f);
            summary.offsetMin=summary.offsetMax=Vector2.zero;
        }
        InventoryTab.onClick.RemoveAllListeners(); SkillsTab.onClick.RemoveAllListeners(); BackButton.onClick.RemoveAllListeners();
        EquipmentTab.onClick.RemoveAllListeners(); StatsTab.onClick.RemoveAllListeners();
        InventoryTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Inventory));
        SkillsTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Skills));
        EquipmentTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Equipment));
        StatsTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Stats));
        BackButton.onClick.AddListener(CloseDialog);
        Refresh();
    }
    private void EnsureTabs()
    {
        if(EquipmentTab==null){EquipmentTab=Instantiate(SkillsTab,SkillsTab.transform.parent);EquipmentTab.name="Equipment tab";}
        if(StatsTab==null){StatsTab=Instantiate(SkillsTab,SkillsTab.transform.parent);StatsTab.name="Stats tab";}
    }
    private void LayoutTabs()
    {
        var tabs=new List<Button>{InventoryTab,EquipmentTab,SkillsTab,StatsTab};
        if(capabilitiesTab!=null && capabilitiesTab.gameObject.activeSelf)tabs.Add(capabilitiesTab);
        float left=.035f,right=.965f;
        for(int i=0;i<tabs.Count;i++)
        {
            var rect=(RectTransform)tabs[i].transform;
            rect.anchorMin=new Vector2(Mathf.Lerp(left,right,(float)i/tabs.Count),rect.anchorMin.y);
            rect.anchorMax=new Vector2(Mathf.Lerp(left,right,(float)(i+1)/tabs.Count),rect.anchorMax.y);
        }
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
        Clear(HeroesRoot); Clear(RowsRoot); rows.Clear();
        for (int i = 0; context is not DungeonPartyMenuContext && i < context.Heroes.Count; i++)
        {
            int slot = i;
            var hero=context.Heroes[i];
            var row=HeroTemplate.Spawn(HeroesRoot, i==heroIndex ? "> "+hero.Name : hero.Name,()=>BrowseHero(slot-heroIndex),hero.Portrait);
        }
        InventoryTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Inventory ? "> Inventory" : "Inventory";
        SkillsTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Skills ? "> Skills" : "Skills";
        EquipmentTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Equipment ? "> Equipment" : "Equipment";
        StatsTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Stats ? "> Stats" : "Stats";
        if(capabilitiesTab!=null)capabilitiesTab.GetComponentInChildren<TMP_Text>().text=Tab==PartyMenuTab.Capabilities?"> Capabilities":"Capabilities";
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
            }
            var row=(entry.Icon!=null?EntryTemplate:PlainEntryTemplate).Spawn(RowsRoot,entry.Title,()=>OpenActions(index),entry.Icon);
            var button=row.Button;
            button.GetComponent<PartyMenuRow>().Selected = () => { selectedIndex = index; ShowDetails(); ScrollToSelected(button.gameObject); };
            rows.Add(button);
        }
        if(entries.Count==0) {var empty=Instantiate(EmptyTemplate,RowsRoot);empty.text=Tab==PartyMenuTab.Inventory?"The bag is empty.":Tab==PartyMenuTab.Capabilities?"No acquired capabilities.":"No learned skills.";empty.gameObject.SetActive(true);}
        Canvas.ForceUpdateCanvases(); scrollView.verticalNormalizedPosition = state?.Scroll ?? 1;
        WireNavigation();
        ShowDetails();
    }
    private void WireNavigation()
    {
        var tab=Tab==PartyMenuTab.Inventory?InventoryTab:Tab==PartyMenuTab.Equipment?EquipmentTab:Tab==PartyMenuTab.Stats?StatsTab:Tab==PartyMenuTab.Capabilities?capabilitiesTab:SkillsTab;
        for(int i=0;i<rows.Count;i++)rows[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=i>0?rows[i-1]:tab,selectOnDown=i+1<rows.Count?rows[i+1]:BackButton,selectOnRight=DetailsControl};
        InventoryTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnRight=EquipmentTab,selectOnDown=rows.Count>0?rows[0]:BackButton};
        EquipmentTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=InventoryTab,selectOnRight=SkillsTab,selectOnDown=rows.Count>0?rows[0]:BackButton};
        SkillsTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=EquipmentTab,selectOnRight=StatsTab,selectOnDown=rows.Count>0?rows[0]:BackButton};
        StatsTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=SkillsTab,selectOnRight=DetailsControl,selectOnDown=rows.Count>0?rows[0]:BackButton};
        if(capabilitiesTab!=null && capabilitiesTab.gameObject.activeSelf)
        {
            var nav=StatsTab.navigation;nav.selectOnRight=capabilitiesTab;StatsTab.navigation=nav;
            capabilitiesTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=StatsTab,selectOnRight=DetailsControl,selectOnDown=rows.Count>0?rows[0]:BackButton};
        }
        BackButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=rows.Count>0?rows[selectedIndex]:tab};
        var heroes=HeroesRoot.GetComponentsInChildren<Button>();
        for(int i=0;i<heroes.Length;i++)heroes[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnLeft=i>0?heroes[i-1]:null,selectOnRight=i+1<heroes.Length?heroes[i+1]:null,selectOnDown=tab};
        if(heroes.Length>0)
        {
            var nav=InventoryTab.navigation;nav.selectOnUp=heroes[heroIndex];InventoryTab.navigation=nav;
            nav=SkillsTab.navigation;nav.selectOnUp=heroes[heroIndex];SkillsTab.navigation=nav;
            nav=EquipmentTab.navigation;nav.selectOnUp=heroes[heroIndex];EquipmentTab.navigation=nav;
            nav=StatsTab.navigation;nav.selectOnUp=heroes[heroIndex];StatsTab.navigation=nav;
        }
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
        DetailsControl.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=rows[selectedIndex],selectOnRight=BackButton};
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
        var picker = PartyMenuPicker.Build(transform.parent, title, options,closeOnChoose);
        picker.CloseAction = null;
        Owner.Open(picker);
    }
    public void PickDetailed(string title,List<(string Label,string Description,Action Execute)> options)
    {
        var picker=PartyMenuPicker.BuildDetailed(transform.parent,title,options);
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
