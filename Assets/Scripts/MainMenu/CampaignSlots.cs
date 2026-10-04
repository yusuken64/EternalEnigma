using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class CampaignSlots : MonoBehaviour
{
    private MainMenu menu;
    private bool newJourney;
    private int selected;
    private GameSaveData[] saves = new GameSaveData[SaveSystem.SlotCount];
    private string[] errors = new string[SaveSystem.SlotCount];
    private Button[] cards = new Button[SaveSystem.SlotCount];
    private TMP_Text detail;
    private Image progress;
    private Button proceed;
    public static void Show(MainMenu menu, bool newJourney)
    {
        if (FindFirstObjectByType<CampaignSlots>() != null) return;
        var canvas = GameUISkin.Canvas("Campaign slots", null, 1000);
        var view = canvas.gameObject.AddComponent<CampaignSlots>();
        view.menu = menu; view.newJourney = newJourney; view.selected = SaveSystem.ActiveSlot;
        menu.NavigationHandler.gameObject.SetActive(false); view.Build();
    }
    private void Build()
    {
        var panel = GameUISkin.Panel(transform,new Vector2(.03f,.04f),new Vector2(.97f,.96f));
        GameUISkin.Label(panel.transform,newJourney ? "New Journey — choose a slot" : "Campaigns",new Vector2(.04f,.90f),new Vector2(.96f,.97f),36);
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");
        for (int i=0;i<SaveSystem.SlotCount;i++)
        {
            int slot = i;
            var save = saves[i] = SaveSystem.Inspect(i,out errors[i]);
            float left = .025f+i*.325f;
            var card = cards[i] = GameUISkin.Button(panel.transform,"",new Vector2(left,.55f),new Vector2(left+.30f,.88f),()=>Select(slot));
            card.name = "Slot " + (i+1);
            var art = GameUISkin.Rect("Biome artwork",card.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f)).gameObject.AddComponent<RawImage>();
            art.texture = catalog?.For(save?.Summary); art.raycastTarget = false;
            var shade = GameUISkin.Rect("Information shade",card.transform,new Vector2(.015f,.025f),new Vector2(.985f,.975f)).gameObject.AddComponent<Image>();
            shade.color = new Color(0,0,0,.58f); shade.raycastTarget = false;
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
                for (int j=0;j<s.Party.Count;j++)
                {
                    var portrait = GameUISkin.Rect("Portrait",card.transform,new Vector2(.04f+j*.23f,.05f),new Vector2(.24f+j*.23f,.30f)).gameObject.AddComponent<Image>();
                    portrait.sprite = (menu.TownConfiguration ?? TownSceneLoader.Default).AllyCatalog.FirstOrDefault(a=>a.Id==s.Party[j].PortraitId)?.Portrait;
                    portrait.preserveAspect = true; portrait.raycastTarget = false;
                }
            }
            var label=GameUISkin.Label(card.transform,text,new Vector2(.04f,.31f),new Vector2(.96f,.96f),23); label.color=Color.white; label.enableAutoSizing=true; label.fontSizeMin=15;
        }
        detail = GameUISkin.Label(panel.transform,"",new Vector2(.04f,.16f),new Vector2(.96f,.52f),24);
        detail.enableAutoSizing=true; detail.fontSizeMin=17;
        var bar=GameUISkin.Rect("Required dungeon progress",panel.transform,new Vector2(.04f,.13f),new Vector2(.96f,.145f)).gameObject.AddComponent<Image>(); bar.color=new Color(.2f,.2f,.2f);
        progress=GameUISkin.Rect("Progress",bar.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>(); progress.color=new Color(.8f,.65f,.25f);
        proceed=GameUISkin.Button(panel.transform,newJourney ? "Choose hero" : "Continue",new Vector2(.57f,.03f),new Vector2(.96f,.10f),Proceed);
        GameUISkin.Button(panel.transform,"Back",new Vector2(.04f,.03f),new Vector2(.3f,.10f),Close);
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
        MenuUIInputModule.Active?.PopDialog(this); menu.NavigationHandler.gameObject.SetActive(true); Destroy(gameObject);
    }
}
