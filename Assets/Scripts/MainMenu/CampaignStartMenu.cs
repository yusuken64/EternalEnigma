using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CampaignStartMenu : MonoBehaviour
{
    public MainMenu MainMenu;
    public List<CampaignSlotView> CampaignSlotViews;

    //CampaignSlots.Show(this, true);
    private string[] errors = new string[SaveSystem.SlotCount];
    private bool _newJourney;

    internal void Setup(bool newJourney)
    {
        _newJourney = newJourney;
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");

        for (int i = 0; i < CampaignSlotViews.Count; i++)
        {
            CampaignSlotView saveSlot = CampaignSlotViews[i];
            var data = SaveSystem.Inspect(i, out errors[i]);
            saveSlot.Setup(data, catalog, i, MainMenu);
            var slotIndex = i;
            saveSlot.ClickAction = () =>
            {
                Activate(slotIndex, saveSlot);
            };
        }

        CampaignSlotViews[0].SelectToActivateButton.Select();
    }

    private void Activate(int selected, CampaignSlotView saveSlot)
    {
        if (_newJourney)
        {
            void Pick()
            {
                SaveSystem.ActiveSlot = selected;
                MainMenu.ChooseHero();
            }

            if (saveSlot.Data != null)
            {
                CampaignChoice.Show("Replace this campaign? The replacement is saved after choosing a hero. Canceling hero selection keeps this save.",
                    "Replace campaign",
                    Pick);
            }
            else
            {
                Pick();
            }
        }
        else
        {
            SaveSystem.ActiveSlot = selected;
            Common.Instance.GameSaveData = SaveSystem.LoadData(selected);

            try { Common.Instance.Travel.Continue(); }
            catch (Exception e) { CampaignChoice.Show("Unable to continue: " + e.Message, "OK", () => { }); }
        }
    }

    

    public void Back()
    {
        MainMenu.StartButton.GetComponent<Button>().Select();
        this.gameObject.SetActive(false);
    }
}
