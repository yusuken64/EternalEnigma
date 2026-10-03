using System;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PartyMenu : Dialog
{
    public RectTransform Panel;
    public Transform HeroesRoot, RowsRoot;
    public TMP_Text Details, Hints, HeroText;
    public Button InventoryTab, SkillsTab, BackButton;
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
        var prefab = Resources.Load<PartyMenu>("UI/PartyMenu");
        var menu = prefab != null ? Instantiate(prefab, parent) : Build(parent);
        menu.gameObject.SetActive(false);
        return menu;
    }

    // Used by the prefab authoring command and as a development fallback before import.
    public static PartyMenu Build(Transform parent)
    {
        var canvas = GameUISkin.Canvas("PartyMenu", parent, 100);
        var menu = canvas.gameObject.AddComponent<PartyMenu>();
        GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=Color.clear;
        var safe = GameUISkin.Rect("Safe area", canvas.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaPanel>();
        menu.Panel = GameUISkin.Panel(safe, new Vector2(.08f,.12f), new Vector2(.82f,.86f)).rectTransform;
        menu.HeroesRoot = GameUISkin.Rect("Heroes", menu.Panel, new Vector2(.025f,.82f), new Vector2(.975f,.975f));
        menu.HeroText = GameUISkin.Label(menu.Panel, "", new Vector2(.49f,.66f), new Vector2(.97f,.79f), 23);
        menu.InventoryTab = GameUISkin.Button(menu.Panel, "Inventory", new Vector2(.025f,.72f), new Vector2(.235f,.80f), null);
        menu.SkillsTab = GameUISkin.Button(menu.Panel, "Skills", new Vector2(.245f,.72f), new Vector2(.455f,.80f), null);
        var viewport = GameUISkin.Rect("List viewport", menu.Panel, new Vector2(.025f,.14f), new Vector2(.455f,.70f));
        viewport.gameObject.AddComponent<Image>().color = new Color(.15f,.20f,.19f,.08f);
        viewport.gameObject.AddComponent<RectMask2D>();
        menu.scrollView = viewport.gameObject.AddComponent<ScrollRect>();
        menu.RowsRoot = GameUISkin.Rect("Entries", viewport, new Vector2(0,1), Vector2.one);
        ((RectTransform)menu.RowsRoot).pivot = new Vector2(.5f,1);
        var layout = menu.RowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.padding = new RectOffset(4,4,4,4);
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        menu.RowsRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        menu.scrollView.viewport = viewport; menu.scrollView.content = (RectTransform)menu.RowsRoot;
        menu.scrollView.horizontal = false; menu.scrollView.movementType = ScrollRect.MovementType.Clamped;
        menu.scrollView.scrollSensitivity = 32;
        var detailViewport = GameUISkin.Rect("Details viewport", menu.Panel, new Vector2(.49f,.14f), new Vector2(.97f,.64f));
        detailViewport.gameObject.AddComponent<RectMask2D>();
        var detailBackground=detailViewport.gameObject.AddComponent<Image>();detailBackground.color = new Color(.8f,.7f,.5f,.08f);
        var detailScroll = detailViewport.gameObject.AddComponent<ScrollRect>();
        menu.DetailsControl=detailViewport.gameObject.AddComponent<TrainerPreviewScroll>();
        menu.DetailsControl.Scroll=detailScroll;menu.DetailsControl.targetGraphic=detailBackground;
        menu.Details = GameUISkin.Label(detailViewport, "", new Vector2(0,1), Vector2.one, 26);
        menu.Details.rectTransform.pivot = new Vector2(.5f,1);
        menu.Details.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailScroll.viewport = detailViewport; detailScroll.content = menu.Details.rectTransform;
        detailScroll.horizontal = false; detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.scrollSensitivity = 32;
        menu.Hints = GameUISkin.Label(menu.Panel, "", new Vector2(.025f,.015f), new Vector2(.80f,.12f), 21);
        MenuControlHints.Bind(menu.Hints,true);
        menu.BackButton = GameUISkin.Button(menu.Panel, "Back", new Vector2(.82f,.03f), new Vector2(.97f,.11f), null);
        return menu;
    }

    private void Awake()
    {
        // Serialized prefab buttons bind here; runtime-built buttons bind in Setup.
    }
    public void Setup(IPartyMenuContext source, PartyMenuTab tab, string heroId = null)
    {
        if (context != source) { context?.Dispose(); context = source; states.Clear(); }
        heroIndex = Math.Max(0, context.Heroes.ToList().FindIndex(h => h.Id == heroId));
        Tab = tab; result = null;
        InventoryTab.onClick.RemoveAllListeners(); SkillsTab.onClick.RemoveAllListeners(); BackButton.onClick.RemoveAllListeners();
        InventoryTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Inventory));
        SkillsTab.onClick.AddListener(() => SwitchTab(PartyMenuTab.Skills));
        BackButton.onClick.AddListener(CloseDialog);
        Refresh();
    }
    public void Shortcut(PartyMenuTab tab)
    {
        if (!IsRoot) return;
        if (Tab == tab) CloseDialog(); else SwitchTab(tab);
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void SwitchTab(PartyMenuTab tab)
    {
        if (!IsRoot || tab == Tab) return;
        Remember(); Tab = tab; result = null; Refresh(); SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    public void BrowseHero(int delta)
    {
        if (!IsRoot || context.Heroes.Count == 0) return;
        Remember(); heroIndex = (heroIndex + delta + context.Heroes.Count) % context.Heroes.Count;
        result = null; Refresh(); SetFirstSelect();
        Common.Instance.MenuInputHandler.ClearInputThisFrame();
    }
    private void Update()
    {
        var input = MenuUIInputModule.Active;
        if (!IsRoot || input == null || input.InputConsumed || Common.Instance.GlobalSettings.IsOpen) return;
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
        for (int i = 0; i < context.Heroes.Count; i++)
        {
            int slot = i;
            var hero = context.Heroes[i]; float width = 1f / Math.Max(4, context.Heroes.Count);
            var button = GameUISkin.Button(HeroesRoot, hero.Name, new Vector2(i*width+.003f,0), new Vector2((i+1)*width-.003f,1), () => BrowseHero(slot-heroIndex));
            var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 23;
            label.rectTransform.anchorMin = new Vector2(.35f,0);
            var backing=GameUISkin.Panel(button.transform,new Vector2(.015f,.06f),new Vector2(.34f,.94f));
            backing.color=new Color(.09f,.17f,.17f,.88f);backing.raycastTarget=false;
            var portrait = GameUISkin.Rect("Portrait", button.transform, new Vector2(.015f,.06f), new Vector2(.34f,.94f)).gameObject.AddComponent<Image>();
            portrait.sprite = hero.Portrait; portrait.preserveAspect = true; portrait.raycastTarget = false;
            if (i == heroIndex) label.text = "> " + hero.Name;
        }
        InventoryTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Inventory ? "> Inventory" : "Inventory";
        SkillsTab.GetComponentInChildren<TMP_Text>().text = Tab == PartyMenuTab.Skills ? "> Skills" : "Skills";
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
                GameUISkin.Label(RowsRoot, section, Vector2.zero, Vector2.one, 22).gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            }
            var button = GameUISkin.Button(RowsRoot, entry.Title, Vector2.zero, Vector2.one, () => OpenActions(index));
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 24;
            label.enableAutoSizing = true; label.fontSizeMin = 19; label.fontSizeMax = 24;
            if (entry.Icon != null)
            {
                var icon = GameUISkin.Rect("Icon", button.transform, new Vector2(.015f,.1f), new Vector2(.13f,.9f)).gameObject.AddComponent<Image>();
                icon.sprite = entry.Icon; icon.preserveAspect = true; icon.raycastTarget = false;
                label.rectTransform.anchorMin = new Vector2(.15f,0);
            }
            button.gameObject.AddComponent<PartyMenuRow>().Selected = () => { selectedIndex = index; ShowDetails(); ScrollToSelected(button.gameObject); };
            rows.Add(button);
        }
        if (entries.Count == 0) GameUISkin.Label(RowsRoot, Tab == PartyMenuTab.Inventory ? "The bag is empty." : "No learned skills.", Vector2.zero,Vector2.one,25).gameObject.AddComponent<LayoutElement>().preferredHeight=90;
        Canvas.ForceUpdateCanvases(); scrollView.verticalNormalizedPosition = state?.Scroll ?? 1;
        WireNavigation();
        ShowDetails();
    }
    private void WireNavigation()
    {
        var tab=Tab==PartyMenuTab.Inventory?InventoryTab:SkillsTab;
        for(int i=0;i<rows.Count;i++)rows[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnUp=i>0?rows[i-1]:tab,selectOnDown=i+1<rows.Count?rows[i+1]:BackButton,selectOnRight=DetailsControl};
        InventoryTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnRight=SkillsTab,selectOnDown=rows.Count>0?rows[0]:BackButton};
        SkillsTab.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=InventoryTab,selectOnRight=DetailsControl,selectOnDown=rows.Count>0?rows[0]:BackButton};
        BackButton.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnUp=rows.Count>0?rows[selectedIndex]:tab};
        var heroes=HeroesRoot.GetComponentsInChildren<Button>();
        for(int i=0;i<heroes.Length;i++)heroes[i].navigation=new Navigation {mode=Navigation.Mode.Explicit,
            selectOnLeft=i>0?heroes[i-1]:null,selectOnRight=i+1<heroes.Length?heroes[i+1]:null,selectOnDown=tab};
        if(heroes.Length>0)
        {
            var nav=InventoryTab.navigation;nav.selectOnUp=heroes[heroIndex];InventoryTab.navigation=nav;
            nav=SkillsTab.navigation;nav.selectOnUp=heroes[heroIndex];SkillsTab.navigation=nav;
        }
    }
    private void ShowDetails()
    {
        HeroText.text = Hero == null ? "No party members" : context.HeroDetails(Hero);
        if (entries.Count == 0) { Details.text = "Select a hero or switch tabs."; return; }
        var entry = entries[selectedIndex];
        DetailsControl.navigation=new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=rows[selectedIndex],selectOnRight=BackButton};
        var actions = context.Actions(Hero,entry);
        string availability = string.Join("\n", actions.Select(a => a.Available ? a.Label : a.Label+": "+a.UnavailableReason));
        Details.text = $"<b>{entry.Title}</b>\n\n{entry.Description}\n\n{context.Restriction(Hero,entry)}\n{availability}\n\n{result}";
        Details.rectTransform.anchoredPosition = Vector2.zero;
    }
    private void OpenActions(int index)
    {
        if (!IsRoot) return;
        selectedIndex=index; Remember(); ShowDetails();
        var entry = entries[index];
        var actions = context.Actions(Hero,entry);
        if (!actions.Any(a=>a.Available)) return;
        Pick(entry.Title, actions.Where(a=>a.Available).Select(a => (a.Label, (Action)(() => a.Execute(this)))).ToList(),false);
    }
    public void Pick(string title, List<(string Label, Action Execute)> options,bool closeOnChoose=true)
    {
        var picker = PartyMenuPicker.Build(transform.parent, title, options,closeOnChoose);
        picker.CloseAction = () => Destroy(picker.gameObject);
        Owner.Open(picker);
    }
    public void Complete(string message)
    {
        while(Owner!=null && Owner.Current!=this)Owner.Close(Owner.Current);
        result = message; Refresh(); SetFirstSelect();
    }
    internal override void SetFirstSelect()
    {
        if (rows.Count > 0) rows[Mathf.Clamp(selectedIndex,0,rows.Count-1)].Select();
        else (Tab == PartyMenuTab.Inventory ? InventoryTab : SkillsTab).Select();
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

public sealed class PartyMenuPicker : Dialog
{
    private Button first;
    private bool committed;
    public static PartyMenuPicker Build(Transform parent, string title, List<(string Label, Action Execute)> choices,bool closeOnChoose=true)
    {
        var canvas = GameUISkin.Canvas("Party picker", parent, 120);
        GameUISkin.Rect("Input shield",canvas.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=new Color(0,0,0,.12f);
        var picker = canvas.gameObject.AddComponent<PartyMenuPicker>();
        var panel = GameUISkin.Panel(canvas.transform,new Vector2(.2f,.2f),new Vector2(.7f,.78f));
        GameUISkin.Label(panel.transform,title,new Vector2(.05f,.85f),new Vector2(.95f,.98f),30);
        var viewport = GameUISkin.Rect("Choices",panel.transform,new Vector2(.05f,.2f),new Vector2(.95f,.84f));
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        picker.scrollView = viewport.gameObject.AddComponent<ScrollRect>();
        var content = GameUISkin.Rect("Rows",viewport,new Vector2(0,1),Vector2.one); content.pivot = new Vector2(.5f,1);
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childControlHeight=true; layout.childForceExpandHeight=false;
        layout.childControlWidth=true;layout.childForceExpandWidth=true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        picker.scrollView.content=content; picker.scrollView.viewport=viewport; picker.scrollView.horizontal=false;
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
        var back=GameUISkin.Button(panel.transform,"Back",new Vector2(.3f,.035f),new Vector2(.7f,.16f),picker.CloseDialog);
        if(picker.first==null)picker.first=back;
        return picker;
    }
    internal override void SetFirstSelect()=>first.Select();
}
