using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignSlots : MonoBehaviour
{
    public CampaignSlotView[] Slots;
    public TMP_Text Title;
    public Button BackButton;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Image progress;
    private MainMenu menu;
    private bool newJourney;
    private int selected;
    private readonly string[] errors = new string[SaveSystem.SlotCount];

    public static void Show(MainMenu menu, bool newJourney) => menu.CampaignStartMenu.Setup(newJourney);

    internal void Setup(MainMenu owner, bool creating)
    {
        menu = owner;
        newJourney = creating;
        selected = Mathf.Clamp(SaveSystem.ActiveSlot, 0, Slots.Length - 1);
        if (Title != null) Title.text = creating ? "New journey - choose a slot" : "Continue a campaign";
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        for (int i = 0; i < Slots.Length; i++)
        {
            int slot = i;
            var view = Slots[i];
            var save = SaveSystem.Inspect(i, out errors[i]);
            view.Setup(save, catalog, i, menu, errors[i]);
            view.Selected = () => Select(slot);
            view.ClickAction = () => Activate(slot);
            // Replace serialized listeners too: old prefab callbacks could target
            // a different CampaignSlotView from the one configured by the browser.
            view.Button.onClick = new Button.ButtonClickedEvent();
            view.Button.onClick.AddListener(view.Click);
            view.Button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = Slots[(i + Slots.Length - 1) % Slots.Length].Button,
                selectOnRight = Slots[(i + 1) % Slots.Length].Button,
                selectOnUp = BackButton, selectOnDown = BackButton
            };
        }
        BackButton.onClick = new Button.ButtonClickedEvent();
        BackButton.onClick.AddListener(Close);
        Select(selected);
        MenuUIInputModule.Active?.PushDialog(this, transform, Slots[selected].Button.gameObject, Close);
        Slots[selected].Button.Select();
    }

    private void Select(int slot)
    {
        selected = slot;
        for (int i = 0; i < Slots.Length; i++)
            Slots[i].Label.color = i == slot ? new Color(1f, .86f, .45f) : Color.white;
        BackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = Slots[slot].Button, selectOnDown = Slots[slot].Button };
        var save = Slots[slot].Data;
        var summary = save?.Summary;
        if (errors[slot] != null) detail.text = "<b>Unreadable save</b>\n" + errors[slot] +
            (newJourney ? "\nChoose this slot to replace it. Canceling keeps the existing data." : "\nThis campaign cannot be continued.");
        else if (save == null) detail.text = newJourney
            ? "<b>Empty slot</b>\nChoose this slot to begin a new journey."
            : "<b>Empty slot</b>\nNo campaign to continue here.";
        else if (summary == null) detail.text = "<b>Campaign</b>\nSave details are unavailable.";
        else
        {
            string point = save.SavePointId?.EndsWith("/home") == true ? "Home" :
                save.SavePointId?.EndsWith("/inn") == true ? "Inn" : "Campaign";
            string timestamp = DateTime.TryParse(save.SavedUtc, out var savedAt) ? savedAt.ToLocalTime().ToString("g") : save.SavedUtc;
            detail.text = $"<b>{summary.Town} - {summary.Biome}</b>\n" +
                $"{point} | {TimeSpan.FromSeconds(save.PlaytimeSeconds).TotalHours:0.0} hours | {save.TownSaveData.Gold:N0} gold\n" +
                $"Saved: {timestamp}\n{summary.Region} | Dungeons cleared: {summary.RequiredCleared}/{summary.RequiredTotal}\n" +
                string.Join("\n", summary.Party.Select(hero => $"{hero.Name} - {hero.PrimaryClass}" +
                    (string.IsNullOrEmpty(hero.SecondaryClass) ? "" : " / " + hero.SecondaryClass) + $" - Lv {hero.Level}"));
        }
        progress.rectTransform.anchorMax = new Vector2(summary == null || summary.RequiredTotal == 0 ? 0 :
            Mathf.Clamp01((float)summary.RequiredCleared / summary.RequiredTotal), 1);
    }

    private void Activate(int slot)
    {
        Select(slot);
        if (newJourney)
        {
            void Pick() { SaveSystem.ActiveSlot = slot; Close(); menu.ChooseHero(); }
            if (Slots[slot].Data != null || errors[slot] != null)
                CampaignChoice.Show("Replace this campaign? The replacement is saved after choosing a hero. Canceling hero selection keeps this save.", "Replace campaign", Pick);
            else Pick();
        }
        else
        {
            if (errors[slot] != null || Slots[slot].Data?.HasCampaign != true) return;
            SaveSystem.ActiveSlot = slot;
            try
            {
                Common.Instance.GameSaveData = SaveSystem.LoadData(slot);
                Close();
                Common.Instance.Travel.Continue();
            }
            catch (Exception e) { CampaignChoice.Show("Unable to continue: " + e.Message, "OK", () => { }); }
        }
    }

    internal void Close()
    {
        MenuUIInputModule.Active?.PopDialog(this);
        gameObject.SetActive(false);
        menu.CampaignStartMenu.gameObject.SetActive(false);
        menu.StartButton.GetComponent<Button>().Select();
    }
}
