using System.Collections.Generic;
using UnityEngine;

public class CampaignStartMenu : MonoBehaviour
{
    public MainMenu MainMenu;
    public GameObject SlotViewer;
    public List<CampaignSlotView> CampaignSlotViews;

    internal void Setup(bool newJourney)
    {
        gameObject.SetActive(true);
        SlotViewer.SetActive(true);
        SlotViewer.GetComponent<CampaignSlots>().Setup(MainMenu, newJourney);
    }

    public void Back() => SlotViewer.GetComponent<CampaignSlots>().Close();
}
