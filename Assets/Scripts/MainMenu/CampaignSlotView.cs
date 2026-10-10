using EternalEnigma.Core.Progression;
using System;
using System.Drawing;
using TMPro;
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

    internal void Setup(GameSaveData data, BiomeBackgroundCatalog catalog)
    {
        Common common = Common.Instance;        
        common.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), data.Campaign);
        CampaignSaveSummary summary = CampaignSaving.BuildSummary(common, data);

        var texture = catalog.For(summary);
        Artwork.texture = texture;
    }
}
