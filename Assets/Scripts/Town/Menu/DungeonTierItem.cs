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
        FitLabel();
        campaignClick = null;
        Button.interactable = true;
        _data = data;
        TierText.text = GameplayText.Format($"Dungeon\nFloors {data.StartFloor} - {data.EndFloor}");
        var save = Common.Instance.GameSaveData.TownSaveData;
        if (save.CompletedTiers.Contains($"{Common.Instance.CurrentTownConfiguration.Id}/{data.StartFloor}-{data.EndFloor}"))
            TierText.text += GameplayText.Format("\nComplete");
    }

    public void SetupCampaign(EternalEnigma.Core.Progression.CampaignLocation location, bool completed, Action onClick)
    {
        FitLabel();
        var floors = EternalEnigma.Core.Progression.CampaignContext.Floors(location.Tier);
        string kind = location.Kind == EternalEnigma.Core.Progression.LocationKind.RepeatableDungeon ? "Repeatable dungeon" : "Story dungeon";
        if (location.Variant == EternalEnigma.Core.World.DungeonVariant.Town) kind = "Town district";
        TierText.text = GameplayText.Format($"{kind}\nFloors {floors.Start} - {floors.End}" + (completed ? "\nComplete" : ""));
        Button.interactable = Common.Instance.CampaignContext?.CanEnterDungeon(location.Id) != false;
        if (!Button.interactable)
            TierText.text = GameplayText.Format("Town dungeon\nOpens tomorrow");
        campaignClick = onClick;
    }

    private void FitLabel()
    {
        TierText.alignment = TextAlignmentOptions.Center;
        TierText.margin = new Vector4(10, 10, 10, 10);
        TierText.enableAutoSizing = true;
        TierText.fontSizeMin = 16;
        TierText.fontSizeMax = 32;
        TierText.textWrappingMode = TextWrappingModes.Normal;
        TierText.raycastTarget = false;
    }

    public void OnClick()
	{
        if (!Button.interactable) return;
        if (campaignClick != null) campaignClick(); else ClickCallback?.Invoke(_data);
    }
}
