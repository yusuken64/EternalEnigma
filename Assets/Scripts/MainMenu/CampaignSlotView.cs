using EternalEnigma.Core.Progression;
using System;
using System.Drawing;
using System.Linq;
using TMPro;
using TWC.OdinSerializer.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements.Experimental;

public sealed class CampaignSlotView : MonoBehaviour
{
    public Button Button;
    public TMP_Text Label;
    public RawImage Artwork;
    public UnityEngine.UI.Image[] Portraits;

    public SelectToActivateButton SelectToActivateButton;

    public GameSaveData Data { get; private set; }
    public Action ClickAction { get; internal set; }

    internal void Setup(GameSaveData save,
        BiomeBackgroundCatalog catalog,
        int i,
        MainMenu menu)
    {
        Data = save;
        var texture = catalog.For(save?.Summary);
        Artwork.texture = texture;

        string text = $"<b>Slot {i + 1}</b>\n";
        if (save == null)
        {
            text += "Empty — begin a new journey";
        }
        else
        {
            string point = save.SavePointId?.EndsWith("/home") == true ? "home" : save.SavePointId?.EndsWith("/inn") == true ? "inn" : "campaign completed";
            string timestamp = DateTime.TryParse(save.SavedUtc, out var savedata) ? savedata.ToLocalTime().ToString("g") : save.SavedUtc;
            text += (save.Campaign.Finished ? "completed" : "in progress") + $"\n{save.Summary.Town} ? {save.Summary.Biome}\n{point}\n{TimeSpan.FromSeconds(save.PlaytimeSeconds).TotalHours:0.0} hours ? {save.TownSaveData.Gold:n0} gold\n{timestamp}";

            Portraits.ForEach(x => x.gameObject.SetActive(false));
            for (int j = 0; j < Mathf.Min(save.Summary.Party.Count, Portraits.Length); j++)
            {
                var portrait = Portraits[j];
                portrait.gameObject.SetActive(true);
                portrait.sprite = (menu.TownConfiguration ?? TownSceneLoader.Default).AllyCatalog.FirstOrDefault(a => a.Id == save.Summary.Party[j].PortraitId)?.Portrait;
            }
        }
    }

    public void Click()
    {
        ClickAction?.Invoke();
    }
}
