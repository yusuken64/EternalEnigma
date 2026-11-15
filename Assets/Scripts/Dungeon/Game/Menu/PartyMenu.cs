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
    private Button closeButton;
    private AuthoredButton capabilityFilter;
    public Button CapabilityFilterButton => capabilityFilter?.Button;
    public bool ShowAllCapabilities { get; private set; }
    public int SpentCapabilityCount { get; private set; }
    private Transform descriptionPaper;
    private EquipmentComparisonView equipmentComparison;
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
    private float rowWidth, rowViewportHeight;
    private string result;
    public PartyMenuTab Tab { get; private set; }
    private bool UsesTenRowList => Tab==PartyMenuTab.Inventory || Tab==PartyMenuTab.Skills || Tab==PartyMenuTab.Capabilities;
    private bool UsesDetachedDescription => UsesTenRowList || Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment;
    public bool IsRoot => Owner?.Current == this;
    public PartyMenuHero Hero => context != null && context.Heroes.Count > 0 ? context.Heroes[heroIndex] : null;

    public static PartyMenu Create(Transform parent)
    {
        return AuthoredUI.Require<PartyMenu>(parent);
    }

    public void Setup(IPartyMenuContext source, PartyMenuTab tab, string heroId = null)
    {
        if (context != source)
        {
            context?.Dispose(); context = source; states.Clear();
            ShowAllCapabilities = (source as PartyMenuContext)?.ShowAllCapabilitiesByDefault == true;
        }
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
        Hints.gameObject.SetActive(false);
        foreach(var label in Panel.GetComponentsInChildren<TMP_Text>(true))
            if(label.transform.parent==Panel)label.gameObject.SetActive(false);
        Panel.Find("Heading ribbon")?.gameObject.SetActive(false);
        BackButton.gameObject.SetActive(false);
        closeButton = DungeonDialogClose.ShowSingle(Panel);
        HeroText.gameObject.SetActive(true);
        Fit(HeroText.transform,.05f,.905f,.80f,.975f);
        HeroText.fontSize=26;HeroText.enableAutoSizing=true;HeroText.fontSizeMin=18;HeroText.fontSizeMax=26;
        HeroText.textWrappingMode=TextWrappingModes.NoWrap;
        InventoryTab=HudTabs.ButtonFor(PartyMenuTab.Inventory);
        EquipmentTab=HudTabs.ButtonFor(PartyMenuTab.Equipment);
        SkillsTab=HudTabs.ButtonFor(PartyMenuTab.Skills);
        StatsTab=HudTabs.ButtonFor(PartyMenuTab.Stats);
        capabilitiesTab=HudTabs.ButtonFor(PartyMenuTab.Capabilities);
    }
    public void Shortcut(PartyMenuTab tab)
    {
        if (!IsRoot) return;
        if (Tab == tab) { AudioManager.Instance?.PlayCue(SoundCue.Cancel); CloseDialog(); } else SwitchTab(tab);
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void SwitchTab(PartyMenuTab tab)
    {
        if (!IsRoot || tab == Tab || (tab == PartyMenuTab.Capabilities && capabilitiesTab == null)) return;
        AudioManager.Instance?.PlayCue(SoundCue.Navigate);
        Remember(); Tab = tab; result = null; Refresh(); SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void BrowseHero(int delta)
    {
        if (!IsRoot || context.Heroes.Count == 0) return;
        int previousHero = heroIndex;
        Remember();
        if(context is DungeonPartyMenuContext)
        {
            Game.Instance.PlayerController.CycleControlledAlly(delta);
            heroIndex=Math.Max(0,context.Heroes.ToList().FindIndex(h=>h.DungeonActor==Game.Instance.PlayerController.ControlledAlly));
        }
        else heroIndex = (heroIndex + delta + context.Heroes.Count) % context.Heroes.Count;
        if (heroIndex != previousHero) AudioManager.Instance?.PlayCue(SoundCue.Navigate);
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
        HudRefresh.Request(HudChange.Party | HudChange.Resources);
        StopAutoScroll();scrollView.StopMovement();
        Clear(HeroesRoot); Clear(RowsRoot); rows.Clear();
        entries = Hero == null ? new() : context.Entries(Hero, Tab);
        SpentCapabilityCount = Tab == PartyMenuTab.Capabilities ? entries.Count(entry => entry.SpentCapability) : 0;
        if (Tab == PartyMenuTab.Capabilities && !ShowAllCapabilities) entries.RemoveAll(entry => entry.SpentCapability);
        RefreshCapabilityFilter();
        bool tenRowList=UsesTenRowList;
        bool detachedDescription=UsesDetachedDescription;
        bool compactStats=Tab==PartyMenuTab.Stats && !entries.Any(entry=>entry.Skill!=null);
        bool showDescription=detachedDescription && (entries.Count>0 || Tab==PartyMenuTab.Skills);
        HeroText.gameObject.SetActive(!detachedDescription);
        DetailsControl.enabled=false;
        DetailsControl.Scroll.gameObject.SetActive(showDescription);
        descriptionPaper ??= Panel.Find("Description parchment");
        var descriptionParent=detachedDescription?Panel.parent:Panel;
        if(descriptionPaper!=null)
        {
            descriptionPaper.SetParent(descriptionParent,false);
            descriptionPaper.gameObject.SetActive(showDescription);
            if(detachedDescription)Fit(descriptionPaper,.43f,.58f,.71f,.88f);
            else Fit(descriptionPaper,.035f,.025f,.965f,.46f);
        }
        DetailsControl.Scroll.transform.SetParent(descriptionParent,false);
        if(detachedDescription)Fit(DetailsControl.Scroll.transform,.45f,.605f,.69f,.855f);
        else Fit(DetailsControl.Scroll.transform,.055f,.045f,.945f,.44f);
        Fit(scrollView.transform,.035f,tenRowList ? .16f : .025f,.965f,.89f);
        var layout=RowsRoot.GetComponent<VerticalLayoutGroup>();
        listSpacing ??= layout.spacing;
        layout.spacing=compactStats?2:listSpacing.Value;
        // The basic stats fit the viewport. Learned passives extend it into a scrolling list.
        RowsRoot.GetComponent<ContentSizeFitter>().enabled=!compactStats;
        Fit(RowsRoot,0,compactStats?0:1,1,1);
        scrollView.vertical=!compactStats;
        HudTabs.Refresh();
        ViewState state = null;
        if (Hero != null) states.TryGetValue((Hero.Id,Tab), out state);
        int identityIndex = state == null ? -1 : entries.FindIndex(e => ReferenceEquals(e.Identity, state.Identity));
        selectedIndex = identityIndex >= 0 ? identityIndex : Mathf.Clamp(state?.Index ?? selectedIndex, 0, Math.Max(0, entries.Count-1));
        string section = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i]; int index = i;
            if (!tenRowList && section != entry.Section)
            {
                section = entry.Section;
                var heading=Instantiate(HeadingTemplate,RowsRoot);heading.text=GameplayText.Format(section);heading.gameObject.SetActive(true);
                if(compactStats)FitStatRow(heading.gameObject,heading,.7f,20);
            }
            var row=(entry.Icon!=null?EntryTemplate:PlainEntryTemplate).Spawn(RowsRoot,entry.Title,()=>OpenActions(index),entry.Icon);
            if(compactStats)
            {
                Fit(row.Label.transform,.035f,0,.965f,1);
                FitStatRow(row.gameObject,row.Label,1,24);
            }
            var button=row.Button;
            if(!compactStats)
            {
                row.Label.enableAutoSizing=tenRowList;
                if(tenRowList){row.Label.fontSize=24;row.Label.fontSizeMin=14;row.Label.fontSizeMax=24;}
                row.Label.textWrappingMode=TextWrappingModes.Normal;
            }
            button.GetComponent<PartyMenuRow>().Selected = () => { selectedIndex = index; ShowDetails(); ScrollToSelected(button.gameObject); };
            rows.Add(button);
        }
        if(entries.Count==0) {var empty=Instantiate(EmptyTemplate,RowsRoot);empty.text=GameplayText.Format(Tab==PartyMenuTab.Inventory?"The bag is empty.":Tab==PartyMenuTab.Capabilities?SpentCapabilityCount>0?"No remaining capability uses.":"No acquired capabilities.":"No learned skills.");empty.gameObject.SetActive(true);}
        Canvas.ForceUpdateCanvases(); FitEntryRows();
        scrollView.verticalNormalizedPosition = compactStats?1:state?.Scroll ?? 1;
        WireNavigation();
        ShowDetails();
    }
    private void LateUpdate()
    {
        if(!IsRoot)return;
        if(!Mathf.Approximately(rowWidth,((RectTransform)RowsRoot).rect.width) ||
            !Mathf.Approximately(rowViewportHeight,scrollView.viewport.rect.height))FitEntryRows();
        if(Tab==PartyMenuTab.Equipment || Tab==PartyMenuTab.Capabilities || equipmentComparison?.Preview!=null)FitEquipmentDescription();
    }
    private void FitEntryRows()
    {
        rowWidth=((RectTransform)RowsRoot).rect.width;
        rowViewportHeight=scrollView.viewport.rect.height;
        if(!scrollView.vertical)return;
        if(UsesTenRowList)
        {
            var layout=RowsRoot.GetComponent<VerticalLayoutGroup>();
            float height=Mathf.Max(1,(rowViewportHeight-layout.padding.vertical-layout.spacing*9)/10);
            foreach(var button in rows)
            {
                var size=button.GetComponent<LayoutElement>();
                size.minHeight=0;size.preferredHeight=height;size.flexibleHeight=0;
            }
            Canvas.ForceUpdateCanvases();return;
        }
        foreach(var button in rows)
        {
            var row=button.GetComponent<AuthoredButton>();
            if(row!=null)PartyMenuRow.FitHeight(row);
        }
        foreach(Transform child in RowsRoot)
            if(child.TryGetComponent<TMP_Text>(out var heading) && child.TryGetComponent<LayoutElement>(out var size))
                size.preferredHeight=Mathf.Max(40,heading.GetPreferredValues(heading.text,Mathf.Max(1,heading.rectTransform.rect.width),0).y+8);
        Canvas.ForceUpdateCanvases();
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
        var filter = capabilityFilter != null && capabilityFilter.gameObject.activeSelf ? capabilityFilter.Button : null;
        Selectable firstRow=rows.Count>0?rows[0]:filter!=null?filter:tab;
        for(int i=0;i<rows.Count;i++)rows[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=i>0?rows[i-1]:tab,selectOnDown=i+1<rows.Count?rows[i+1]:filter};
        if (filter != null) filter.navigation = new Navigation { mode=Navigation.Mode.Explicit,
            selectOnUp=rows.Count>0?rows[rows.Count-1]:tab, selectOnDown=rows.Count>0?rows[0]:tab };
        DetailsControl.navigation=new Navigation {mode=Navigation.Mode.None};
        InventoryTab.navigation=EquipmentTab.navigation=SkillsTab.navigation=StatsTab.navigation=
            new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=closeButton,selectOnDown=firstRow};
        if(capabilitiesTab!=null && capabilitiesTab.gameObject.activeSelf)
            capabilitiesTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=closeButton,selectOnDown=firstRow};
        if(closeButton!=null)closeButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=tab,selectOnDown=firstRow};
    }
    private void RefreshCapabilityFilter()
    {
        bool visible = Tab == PartyMenuTab.Capabilities && SpentCapabilityCount > 0;
        if (visible && capabilityFilter == null)
        {
            capabilityFilter = PlainEntryTemplate.Spawn(Panel, "", ToggleCapabilityFilter);
            capabilityFilter.name = "Capability list filter";
            capabilityFilter.GetComponent<LayoutElement>().ignoreLayout = true;
            Fit(capabilityFilter.transform, .05f, .035f, .95f, .12f);
            capabilityFilter.Label.enableAutoSizing = true;
            capabilityFilter.Label.fontSize = 24; capabilityFilter.Label.fontSizeMin = 14; capabilityFilter.Label.fontSizeMax = 24;
        }
        if (capabilityFilter == null) return;
        capabilityFilter.gameObject.SetActive(visible);
        capabilityFilter.Label.text = ShowAllCapabilities ? "Hide completed" : "Show all";
    }
    public void ToggleCapabilityFilter()
    {
        if (!IsRoot || Tab != PartyMenuTab.Capabilities) return;
        ShowAllCapabilities = !ShowAllCapabilities;
        if (Hero != null) states.Remove((Hero.Id, Tab));
        selectedIndex = 0; result = null;
        AudioManager.Instance?.PlayCue(SoundCue.Navigate);
        Refresh();
        if (capabilityFilter != null && capabilityFilter.gameObject.activeSelf) capabilityFilter.Button.Select();
        else SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    private void ShowDetails()
    {
        HeroText.text = GameplayText.Format(Hero == null ? "No party members" : Hero.Name);
        if (entries.Count == 0)
        {
            equipmentComparison?.Show(null);
            Details.text = GameplayText.Format(context is DungeonPartyMenuContext ?
                "No entries for this character. Switch tabs, or click a HUD portrait to change characters." : "Select a hero or switch tabs.");
            return;
        }
        var entry = entries[selectedIndex];
        var actions = Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment ? new List<PartyMenuAction>() : context.Actions(Hero,entry);
        string availability = UsesDetachedDescription
            ? string.Join("\n",actions.Where(a=>!a.Available).Select(a=>a.UnavailableReason).Distinct())
            : string.Join("\n",actions.Select(a=>a.Available?a.Label:a.Label+": "+a.UnavailableReason));
        string restriction=Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment ? "" : context.Restriction(Hero,entry);
        var description=entry.Status!=null ? StatusPresentation.Description(entry.Status,Hero?.DungeonActor) : entry.Description;
        string title=entry.EquipmentPreview!=null?entry.Item.DisplayName:entry.Title;
        Details.text = GameplayText.Format(string.Join(entry.EquipmentPreview!=null?"\n":"\n\n",new[]{ $"<b>{title}</b>",description,restriction,availability,result }
            .Where(text=>!string.IsNullOrWhiteSpace(text)).Distinct()));
        if(entry.EquipmentPreview!=null || equipmentComparison!=null)
        {
            equipmentComparison ??= EquipmentComparisonView.For(Details,(RectTransform)descriptionPaper,DetailsControl.Scroll);
            equipmentComparison.Show(entry.EquipmentPreview);
        }
        Details.rectTransform.anchoredPosition = Vector2.zero;
        DetailsControl.Scroll.StopMovement();
        if(Details.gameObject.activeInHierarchy)Canvas.ForceUpdateCanvases();
        if(Tab==PartyMenuTab.Equipment || Tab==PartyMenuTab.Capabilities || equipmentComparison!=null)FitEquipmentDescription();
        DetailsControl.Scroll.verticalNormalizedPosition=1;
    }
    private void FitEquipmentDescription()
    {
        // Refit after font or window changes too, without moving focus into a second scroll area.
        if(equipmentComparison?.Preview!=null && Panel.parent is RectTransform previewSurface && previewSurface.rect.height>0)
        {
            float height=Mathf.Max(.30f,equipmentComparison.RequiredPanelHeight/previewSurface.rect.height);
            Fit(descriptionPaper,.43f,Mathf.Max(.30f,.88f-height),.71f,.88f);
            equipmentComparison.Reflow();
            return;
        }
        float bottom=.605f;
        if(selectedIndex<entries.Count && (entries[selectedIndex].Status!=null || Tab==PartyMenuTab.Capabilities) && Panel.parent is RectTransform surface && surface.rect.height>0)
            bottom=Mathf.Clamp(.855f-(Details.preferredHeight+8)/surface.rect.height,.30f,.605f);
        var rect=(RectTransform)DetailsControl.Scroll.transform;
        if(rect.parent==Panel.parent && Mathf.Approximately(rect.anchorMin.y,bottom))return;
        rect.SetParent(Panel.parent,false);
        Fit(rect,.45f,bottom,.69f,.855f);
        if(descriptionPaper!=null)Fit(descriptionPaper,.43f,bottom-.025f,.71f,.88f);
        Canvas.ForceUpdateCanvases();
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
        if(Tab==PartyMenuTab.Stats || entry.Status!=null)return;
        if(entry.Strategy)
        {
            var actor=Hero.DungeonActor;
            if(actor==null || actor!=Game.Instance?.PlayerController.ControlledAlly)
            { Reject("Control this ally before changing their strategy.");return; }
            var choices=AllyStrategyInfo.Choices.Select(strategy=>(AllyStrategyInfo.Name(strategy),
                AllyStrategyInfo.Description(strategy),(Action)(()=>{
                    if(actor==null || actor!=Game.Instance?.PlayerController.ControlledAlly)
                    { Reject("Control this ally before changing their strategy.");return; }
                    actor.AllyStrategy=strategy;
                    Complete("Strategy set to "+AllyStrategyInfo.Name(strategy)+".");
                }))).ToList();
            PickDetailed("Strategy",choices,selectedIndex:Array.IndexOf(AllyStrategyInfo.Choices,actor.AllyStrategy));
            return;
        }
        if(entry.Slot.HasValue)
        {
            var equipmentOptions=((PartyMenuContext)context).EquipmentOptions(Hero,entry.Slot.Value);
            var options=equipmentOptions.Select(option=>(option.Label,option.Description,(Action)(()=>{
                    var itemEntry=new PartyMenuEntry{Item=option.Item,Equipped=Hero.Equipment.IsEquipped(option.Item)};
                    var action=context.Actions(Hero,itemEntry).FirstOrDefault();
                    action?.Execute(this);
                }))).ToList();
            PickDetailed("Change "+(entry.Slot==EquipmentSlot.MainHand?"weapon":entry.Slot==EquipmentSlot.OffHand?"off-hand":"accessory"),
                options,previews:equipmentOptions.Select(option=>option.Preview).ToList());return;
        }
        if (context is IPartyMenuEntryHandler handler && handler.OpenEntry(this, Hero, entry)) return;
        var actions = Tab==PartyMenuTab.Stats || Tab==PartyMenuTab.Equipment ? new List<PartyMenuAction>() : context.Actions(Hero,entry);
        if (actions.Count == 0)
        {
            if(!string.IsNullOrEmpty(entry.Description))
            {
                var details=PartyMenuPicker.Build(transform.parent,entry.Title,new List<(string,Action)>());
                details.Description(entry.Description);
                Owner.Open(details);
            }
            return;
        }
        if (entry.Skill?.ActivationType == ActivationType.Active && actions.Count == 1)
        {
            Common.Instance.MenuInputHandler.ClearInputThisFrame();
            actions[0].Execute(this);
            return;
        }
        Pick(entry.Title, actions.Select(a => (a.Label, (Action)(() => a.Execute(this)))).ToList(),false);
    }
    public void Pick(string title, List<(string Label, Action Execute)> options,bool closeOnChoose=true)
    {
        var picker = PartyMenuPicker.Build(transform.parent, title, options,closeOnChoose,showBackButton:false);
        picker.CloseAction = null;
        Owner.Open(picker);
    }
    public void PickDetailed(string title,List<(string Label,string Description,Action Execute)> options,string description=null,IReadOnlyList<EquipmentStatPreview> previews=null,int selectedIndex=0)
    {
        var picker=PartyMenuPicker.BuildDetailed(transform.parent,title,options,closeOnChoose:false,showBackButton:false,previews:previews,selectedIndex:selectedIndex);
        if(Tab==PartyMenuTab.Equipment)picker.UseEquipmentDescriptionLayout();
        if(!string.IsNullOrEmpty(description))picker.Description(description);
        picker.CloseAction=null;Owner.Open(picker);
    }
    public void Reject(string message)
    {
        result = message;
        GameMessages.Reject(message);
    }
    public void Complete(string message)
    {
        while(Owner!=null && Owner.Current!=this)Owner.Close(Owner.Current);
        if(!string.IsNullOrWhiteSpace(message))GameMessages.Post(message);
        result = message; Refresh(); SetFirstSelect();
    }
    internal override void SetFirstSelect()
    {
        Canvas.ForceUpdateCanvases();FitEntryRows();
        if (rows.Count > 0) rows[Mathf.Clamp(selectedIndex,0,rows.Count-1)].Select();
        else if (Tab == PartyMenuTab.Capabilities && capabilityFilter != null && capabilityFilter.gameObject.activeSelf) capabilityFilter.Button.Select();
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
