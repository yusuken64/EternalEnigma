using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CampaignSlotView : MonoBehaviour, ISelectHandler
{
    public Button Button;
    public TMP_Text Label;
    public RawImage Artwork;
    public Image[] Portraits;
    public SelectToActivateButton SelectToActivateButton;
    public GameSaveData Data { get; private set; }
    public Action ClickAction { get; internal set; }
    public Action Selected { get; internal set; }

    internal void Setup(GameSaveData save, BiomeBackgroundCatalog catalog, int index, MainMenu menu, string error = null)
    {
        Data = save;
        Artwork.texture = catalog.For(save?.Summary);
        Label.text = $"<b>Slot {index + 1}</b>\n<size=65%>" +
            (error != null ? "Unreadable save" : save == null ? "Empty - new journey" :
                save.Campaign?.Finished == true ? "Completed" : "In progress") + "</size>";
        foreach (var portrait in Portraits)
        {
            portrait.gameObject.SetActive(false);
            portrait.sprite = null;
        }
        if (save?.Summary == null) return;
        var heroes = (menu.TownConfiguration ?? TownSceneLoader.Default).AllyCatalog;
        for (int i = 0; i < Mathf.Min(save.Summary.Party.Count, Portraits.Length); i++)
        {
            var portrait = Portraits[i];
            portrait.sprite = heroes.FirstOrDefault(a => a.Id == save.Summary.Party[i].PortraitId)?.Portrait;
            portrait.gameObject.SetActive(portrait.sprite != null);
        }
    }

    public void Click() => ClickAction?.Invoke();
    public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
}
