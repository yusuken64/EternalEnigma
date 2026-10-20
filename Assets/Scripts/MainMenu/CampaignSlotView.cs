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
        FitArtwork();
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

    private void OnEnable() => FitArtwork();
    private void OnRectTransformDimensionsChange() => FitArtwork();
    private void FitArtwork()
    {
        if (Artwork == null || Artwork.texture == null) return;
        var size = Artwork.rectTransform.rect.size;
        if (size.x <= 0 || size.y <= 0) return;
        float sourceAspect = (float)Artwork.texture.width / Artwork.texture.height;
        float cardAspect = size.x / size.y;
        // Fill the card with a centred crop, retaining the building's proportions.
        float width = Mathf.Min(1, cardAspect / sourceAspect);
        float height = Mathf.Min(1, sourceAspect / cardAspect);
        Artwork.uvRect = new Rect((1 - width) * .5f, (1 - height) * .5f, width, height);
    }
}
