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
    }

    public void OnClick()
	{
        ClickCallback?.Invoke(_data);
    }
}
