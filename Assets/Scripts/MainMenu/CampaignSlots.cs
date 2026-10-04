using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class CampaignSlots : MonoBehaviour
{
    public CampaignSlotView[] Slots;
    public TMP_Text Title;
    public Button BackButton;
    private MainMenu menu;
    private bool newJourney;
    private int selected;
    private GameSaveData[] saves = new GameSaveData[SaveSystem.SlotCount];
    private string[] errors = new string[SaveSystem.SlotCount];
    private Button[] cards = new Button[SaveSystem.SlotCount];
    [SerializeField] private TMP_Text detail;
    [SerializeField] private Image progress;
    [SerializeField] private Button proceed;
    public static void Show(MainMenu menu,bool newJourney)
    {
        var view=AuthoredUI.Require<CampaignSlots>(menu.transform);
        if(view.gameObject.activeSelf)return;
        view.menu=menu;view.newJourney=newJourney;view.selected=SaveSystem.ActiveSlot;
        view.gameObject.SetActive(true);menu.NavigationHandler.gameObject.SetActive(false);view.Build();
    }
    private void Build()
    {
        Title.text=newJourney?"New Journey — choose a slot":"Campaigns";
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        for (int i=0;i<SaveSystem.SlotCount;i++)
        {
            int slot = i;
            var save = saves[i] = SaveSystem.Inspect(i,out errors[i]);
            var card=cards[i]=Slots[i].Button;
            card.onClick.RemoveAllListeners();card.onClick.AddListener(()=>Select(slot));
            Slots[i].Artwork.texture=catalog?.For(save?.Summary);
            foreach(var portrait in Slots[i].Portraits)portrait.gameObject.SetActive(false);
            string text = $"<b>Slot {i+1}</b>\n";
            if (errors[i] != null) text += "Unreadable save\n"+errors[i];
            else if (save == null) text += "Empty — begin a new journey";
            else if (save.Summary == null) { errors[i]="Unsupported save summary."; text += errors[i]; }
            else
            {
                var s = save.Summary;
                string point = save.SavePointId?.EndsWith("/home") == true ? "Home" : save.SavePointId?.EndsWith("/inn") == true ? "Inn" : "Campaign completed";
                string timestamp = DateTime.TryParse(save.SavedUtc, out var savedAt) ? savedAt.ToLocalTime().ToString("g") : save.SavedUtc;
                text += (save.Campaign.Finished ? "Completed" : "In Progress")+$"\n{s.Town} ? {s.Biome}\n{point}\n{TimeSpan.FromSeconds(save.PlaytimeSeconds).TotalHours:0.0} hours ? {save.TownSaveData.Gold:N0} gold\n{timestamp}";
                for (int j=0;j<Mathf.Min(s.Party.Count,Slots[i].Portraits.Length);j++)
                {
                    var portrait=Slots[i].Portraits[j];portrait.gameObject.SetActive(true);
                    portrait.sprite = (menu.TownConfiguration ?? TownSceneLoader.Default).AllyCatalog.FirstOrDefault(a=>a.Id==s.Party[j].PortraitId)?.Portrait;

                }
            }
            Slots[i].Label.text=text;
        }
        proceed.GetComponentInChildren<TMP_Text>().text=newJourney?"Choose hero":"Continue";
        proceed.onClick.RemoveAllListeners();proceed.onClick.AddListener(Proceed);
        BackButton.onClick.RemoveAllListeners();BackButton.onClick.AddListener(Close);
        MenuUIInputModule.Active?.PushDialog(this,transform,cards[selected].gameObject,Close);
        Select(selected);
    }
    private void Select(int slot)
    {
        selected=slot; var save=saves[slot]; var s=save?.Summary;
        proceed.interactable=newJourney || (errors[slot]==null && save?.HasCampaign==true && !save.Campaign.Finished);
        for(int i=0;i<cards.Length;i++) cards[i].GetComponent<Image>().color=i==slot ? GameUITheme.Selected : Color.white;
        detail.text = errors[slot] ?? (s==null ? "Choose an empty slot to begin." :
            $"<b>{s.Region}</b>    Regions visited: {s.RegionsVisited} / {s.RegionsTotal}\n"+
            $"Required dungeons cleared: {s.RequiredCleared} / {s.RequiredTotal}\n"+
            string.Join("\n",s.Party.Select(h=>$"<b>{h.Name}</b> · {h.PrimaryClass}{(string.IsNullOrEmpty(h.SecondaryClass) ? "" : " / " + h.SecondaryClass)} · Lv {h.Level}    HP {h.Hp:N0}/{h.MaxHp:N0}    SP {h.Sp:N0}/{h.MaxSp:N0}    Strength {h.Strength:N0}    Defense {h.Defense:N0}    EXP {h.Experience:N0} ({h.ExperienceProgress:P0})")));
        progress.rectTransform.anchorMax = new Vector2(s==null || s.RequiredTotal==0 ? 0 : (float)s.RequiredCleared/s.RequiredTotal,1);
    }
    private void Proceed()
    {
        if (newJourney)
        {
            void Pick() { SaveSystem.ActiveSlot=selected; Close(); menu.ChooseHero(); }
            if(saves[selected]!=null || errors[selected]!=null) CampaignChoice.Show("Replace this campaign? The replacement is saved after choosing a hero. Canceling hero selection keeps this save.","Replace campaign",Pick);
            else Pick();
        }
        else
        {
            SaveSystem.ActiveSlot=selected;
            Common.Instance.GameSaveData=SaveSystem.LoadData(selected);
            Close();
            try { Common.Instance.Travel.Continue(); }
            catch(Exception e) { CampaignChoice.Show("Unable to continue: "+e.Message,"OK",()=>{}); }
        }
    }
    private void Close()
    {
        MenuUIInputModule.Active?.PopDialog(this); menu.NavigationHandler.gameObject.SetActive(true); gameObject.SetActive(false);
    }
}
