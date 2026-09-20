using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonTierItem : MonoBehaviour
{
    public TextMeshProUGUI TierText;

    public DungeonTierData DungeonTierData;
    public Action<DungeonTierData> ClickCallback;
	private DungeonTierData _data;

    public Button Button;

    public void Setup(DungeonTierData data)
    {
        _data = data;
        TierText.text = $"Dungeon Level {data.StartFloor} - {data.EndFloor}";
        var save = Common.Instance.GameSaveData.TownSaveData;
        if (save.DonationTotal < data.RequiredDonation)
            TierText.text += $" (Donate {data.RequiredDonation}g)";
        if (save.CompletedTiers.Contains($"{Common.Instance.CurrentTownConfiguration.Id}/{data.StartFloor}-{data.EndFloor}"))
            TierText.text += " - Complete";
    }

    public void OnClick()
	{
        ClickCallback?.Invoke(_data);
    }
}
