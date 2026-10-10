using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CampaignStartMenu : MonoBehaviour
{
    private Transform SlotsContainer;
    public List<CampaignSlotView> CampaignSlotViews;

    //CampaignSlots.Show(this, true);
    private string[] errors = new string[SaveSystem.SlotCount];
    internal void Setup()
    {
        var catalog = Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");

        for (int i = 0; i < CampaignSlotViews.Count; i++)
        {
            CampaignSlotView saveSlot = CampaignSlotViews[i];
            var data = SaveSystem.Inspect(i, out errors[i]);
            saveSlot.Setup(data, catalog);
        }

        CampaignSlotViews[0].SelectToActivateButton.Select();
        //TODO setup nav
    }
}
