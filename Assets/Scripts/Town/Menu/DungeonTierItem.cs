using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonTierItem : MonoBehaviour
{
    public TextMeshProUGUI TierText;

    public DungeonTierData DungeonTierData;
    public Action<DungeonTierData> ClickCallback;
    private Action campaignClick;
	private DungeonTierData _data;

    public Button Button;

    public void Setup(DungeonTierData data)
    {
        campaignClick = null;
        _data = data;
        TierText.text = $"Dungeon Level {data.StartFloor} - {data.EndFloor}";
        var save = Common.Instance.GameSaveData.TownSaveData;
        if (save.DonationTotal < data.RequiredDonation)
            TierText.text += $" (Donate {data.RequiredDonation}g)";
        if (save.CompletedTiers.Contains($"{Common.Instance.CurrentTownConfiguration.Id}/{data.StartFloor}-{data.EndFloor}"))
            TierText.text += " - Complete";
    }

    public void SetupCampaign(EternalEnigma.Core.Progression.CampaignLocation location, bool completed, Action onClick)
    {
        var floors = EternalEnigma.Core.Progression.CampaignContext.Floors(location.Tier);
        string kind = location.Kind == EternalEnigma.Core.Progression.LocationKind.RepeatableDungeon ? "Repeatable dungeon" : "Story dungeon";
        TierText.text = $"{kind}: Level {floors.Start} - {floors.End}" + (completed ? " - Complete" : "");
        campaignClick = onClick;
    }

    public void OnClick()
	{
        if (campaignClick != null) campaignClick(); else ClickCallback?.Invoke(_data);
    }
}
