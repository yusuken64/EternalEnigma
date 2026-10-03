using JuicyChickenGames.Menu;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BallistaDialog : Dialog
{
    public TextMeshProUGUI SkillsText;
    public SkillGridItem SkillGridSelectablePrefab;
    public Transform SkillGridContainer;
    public List<SkillGridItem> SkillGridItems { get; private set; } = new();
    public TownAlly Character { get; internal set; }
    public BallistaPurchaseDialog BallistaPurchaseDialog;
    public TrainerLayout Layout { get; private set; }
    private ClassSource source;
    private readonly string[] selectedSkills = new string[2];
    private readonly float[] listPositions = { 1, 1 };
    private readonly float[] previewPositions = { 1, 1 };
    private SkillGridItem selected;

    public override void PrepareTown(TownInteractionContext context)
    {
        Character = context.Player.ControllingTownAlly;
        Show();
    }

    internal override void SetFirstSelect()
    {
        if (Layout == null) return;
        (selected != null ? selected.GridButton : Layout.Close).Select();
    }

    internal void Show()
    {
        BallistaPurchaseDialog.gameObject.SetActive(false);
        gameObject.SetActive(true);
        EnsureLayout();
        source = ClassSource.Primary;
        for (int i = 0; i < 2; i++)
        {
            selectedSkills[i] = null;
            listPositions[i] = previewPositions[i] = 1;
        }
        Layout.PrimaryTab.GetComponentInChildren<TMP_Text>().text = Character.PrimaryClass == null
            ? "Skills" : "Primary - " + Character.PrimaryClass.DisplayName;
        bool secondary = Character.PrimaryClass != null && Character.SecondaryClass != null &&
            Character.PrimaryClass.Id != Character.SecondaryClass.Id;
        Layout.SecondaryTab.gameObject.SetActive(secondary);
        if (secondary) Layout.SecondaryTab.GetComponentInChildren<TMP_Text>().text = "Secondary - " + Character.SecondaryClass.DisplayName;
        BuildList();
    }

    private void EnsureLayout()
    {
        if (Layout != null) return;
        foreach (Transform child in transform)
            if (child != BallistaPurchaseDialog.transform) child.gameObject.SetActive(false);
        var navigation = GetComponent<NavigationHandler>();
        if (navigation != null) navigation.selectionArrow = null;
        Layout = Instantiate(Resources.Load<TrainerLayout>("UI/TrainerLayout"), transform, false);
        BallistaPurchaseDialog.transform.SetAsLastSibling();
        SkillsText = Layout.Balance;
        SkillGridContainer = Layout.ListScroll.content;
        SkillGridSelectablePrefab = Layout.SkillTemplate;
        scrollView = Layout.ListScroll;
        Layout.PrimaryTab.onClick.AddListener(() => ChangeTab(ClassSource.Primary));
        Layout.SecondaryTab.onClick.AddListener(() => ChangeTab(ClassSource.Secondary));
        Layout.Close.onClick.AddListener(Close_Clicked);
    }

    public void ChangeTab(ClassSource next)
    {
        if (next == ClassSource.Secondary && !Layout.SecondaryTab.gameObject.activeSelf) return;
        if (next == source) { SetFirstSelect(); return; }
        StopAutoScroll();
        listPositions[(int)source] = scrollView.verticalNormalizedPosition;
        previewPositions[(int)source] = Layout.PreviewScroll.verticalNormalizedPosition;
        source = next;
        BuildList();
        SetFirstSelect();
    }

    private List<TrainerOffer> Offers() => TrainerOffers.Build(Character, FindFirstObjectByType<Town>().Configuration);

    private void BuildList()
    {
        StopAutoScroll();
        selected = null;
        foreach (Transform child in SkillGridContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        SkillGridItems.Clear();
        foreach (var tier in Offers().Where(o => o.Source == source).GroupBy(o => o.Tier))
        {
            var heading = Instantiate(Layout.HeadingTemplate, SkillGridContainer, false);
            heading.gameObject.SetActive(true);
            heading.text = Character.PrimaryClass == null ? "Available skills" :
                $"Tier {tier.Key} - unlocks at level {tier.First().TierRequiredLevel}";
            foreach (var offer in tier)
            {
                var row = Instantiate(SkillGridSelectablePrefab, SkillGridContainer, false);
                row.Setup(new TogglableSkillGridItem { Skill = offer.Skill, Offer = offer, Active = offer.CurrentRank > 0 });
                row.Character = Character;
                row.BallistaPurchaseDialog = BallistaPurchaseDialog;
                row.SkillToggledCallback = Refresh;
                row.SelectedCallback = Preview;
                row.GridButton.onClick.AddListener(row.ToggleOn_Clicked);
                row.gameObject.SetActive(true);
                SkillGridItems.Add(row);
            }
        }
        selected = SkillGridItems.FirstOrDefault(i => i.GetSkill().SkillName == selectedSkills[(int)source]) ?? SkillGridItems.FirstOrDefault();
        WireNavigation();
        UpdateBalance();
        if (selected != null) Preview(selected);
        else Layout.Preview.text = "No skills in this tree.";
        StopAutoScroll();
        Canvas.ForceUpdateCanvases();
        scrollView.verticalNormalizedPosition = listPositions[(int)source];
        Layout.PreviewScroll.verticalNormalizedPosition = previewPositions[(int)source];
    }

    private void WireNavigation()
    {
        var tab = source == ClassSource.Primary ? Layout.PrimaryTab : Layout.SecondaryTab;
        var first = SkillGridItems.FirstOrDefault()?.GridButton ?? Layout.Close;
        var last = SkillGridItems.LastOrDefault()?.GridButton ?? tab;
        var other = Layout.SecondaryTab.gameObject.activeSelf ? Layout.SecondaryTab : Layout.PrimaryTab;
        Link(Layout.PrimaryTab, Layout.Close, first, other, other);
        Link(Layout.SecondaryTab, Layout.Close, first, Layout.PrimaryTab, Layout.PrimaryTab);
        for (int i = 0; i < SkillGridItems.Count; i++)
            Link(SkillGridItems[i].GridButton, i == 0 ? tab : SkillGridItems[i - 1].GridButton,
                i + 1 == SkillGridItems.Count ? Layout.Close : SkillGridItems[i + 1].GridButton, tab, Layout.PreviewControl);
        Link(Layout.Close, last, tab, selected?.GridButton ?? first, Layout.PreviewControl);
        Link(Layout.PreviewControl, tab, Layout.Close, selected?.GridButton ?? first, Layout.Close);
    }

    private static void Link(Selectable control, Selectable up, Selectable down, Selectable left, Selectable right) =>
        control.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up,
            selectOnDown = down, selectOnLeft = left, selectOnRight = right };

    private void Preview(SkillGridItem row)
    {
        bool changed = selectedSkills[(int)source] != row.GetSkill().SkillName;
        selected = row;
        selectedSkills[(int)source] = row.GetSkill().SkillName;
        var o = row.Offer;
        string status = o.IsMaxed ? "Already at maximum rank." : !o.CanLearn ? o.LockReason : "Ready to learn.";
        string requirements = o.HasClass ? $"{o.Kind} - Rank {o.CurrentRank}/{o.MaxRank}\nTier level: {o.TierRequiredLevel}" +
            (o.IsMaxed ? "" : $" - Next rank level: {o.NextRequiredLevel}\nNext rank: {o.NextCost} learning points") :
            $"Rank {o.CurrentRank}/{o.MaxRank}" + (o.IsMaxed ? "" : $" - Cost: {o.NextCost} gold");
        Layout.Preview.text = $"<b>{o.Skill.SkillName}</b>\n{requirements}\n\n{status}\n{o.Prerequisites}\n\n{o.Skill.Description}";
        if (changed) Layout.PreviewScroll.verticalNormalizedPosition = 1;
        WireNavigation();
        ScrollToSelected(row.gameObject);
    }

    public void Refresh()
    {
        var offers = Offers().ToDictionary(o => o.Skill.SkillName);
        foreach (var row in SkillGridItems)
            if (offers.TryGetValue(row.GetSkill().SkillName, out var offer))
                row.Setup(new TogglableSkillGridItem { Skill = offer.Skill, Offer = offer, Active = offer.CurrentRank > 0 });
        UpdateBalance();
        if (selected != null) Preview(selected);
    }

    private void UpdateBalance() => SkillsText.text = Character.PrimaryClass == null ? "Skill training - Gold purchases" :
        $"Skill training - {TrainerOffers.AvailablePoints(Character)} learning points available (separate from combat SP)";
    public void Close_Clicked() => CloseDialog();
    [ContextMenu("Force Select")] public void ForceSelect() => SetFirstSelect();
    internal List<string> GetActiveSkillsSave() => Character?.Skills.ToList() ?? new();
}

public class TogglableSkillGridItem
{
    public bool Active;
    public Skill Skill;
    public TrainerOffer Offer;
}
