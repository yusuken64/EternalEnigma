using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EntranceDialog : Dialog
{
	public List<DungeonTierData> DungeonTierDatas;
	public DungeonTierItem DungeonTierItemPrefab;
	public Transform Container;
	private List<DungeonTierItem> items;

	public Button CancelButton;

    public override void PrepareTown(TownInteractionContext context)
    {
        DungeonTierDatas = context.Town.Configuration.DungeonTiers;
        Setup();
        SetNavigation();
    }

	internal void Setup()
	{
		Action<DungeonTierItem, DungeonTierData> setupAction = (view, data) =>
		{
			view.Setup(data);
			view.ClickCallback = (data) =>
			{
				DungeonClicked(data);
			};
		};
		items = Container.RePopulateObjects(DungeonTierItemPrefab, DungeonTierDatas, setupAction);
	}

	internal override void SetFirstSelect()
	{
		if (items.Count > 0) items[0].Button.Select(); else CancelButton.Select();
	}

	public void SetNavigation()
	{
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];

			Navigation customNav = new Navigation
			{
				mode = Navigation.Mode.Explicit
			};

			customNav.selectOnUp = items[(i - 1 + items.Count) % items.Count].Button;

			if (i < items.Count - 1)
				customNav.selectOnDown = items[i + 1].Button;
			else
				customNav.selectOnDown = CancelButton;

			item.Button.navigation = customNav;
		}

		var cancelNav = new Navigation
		{
			mode = Navigation.Mode.Explicit,
			selectOnUp = items.Count > 0 ? items[^1].Button : CancelButton,
			selectOnDown = items.Count > 0 ? items[0].Button : CancelButton
		};
		CancelButton.navigation = cancelNav;
	}

	public void DungeonClicked(DungeonTierData data)
	{
		var town = FindFirstObjectByType<Town>();
        if (Common.Instance.CampaignContext != null) { TownMenu.ShowMessage("Leave town through the southern exit and enter a dungeon marker."); return; }
        if (!town.Services.CanEnter(data))
        {
            TownMenu.ShowMessage($"Donate {data.RequiredDonation}g total at the statue to unlock this tier.");
            return;
        }
        town.WriteSaveData();
        Common.Instance.GameSaveData.DungeonSaveData.ReturnCommitted = false;
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			Common.Instance.GameSaveData.DungeonSaveData.StartFloor = data.StartFloor;
			Common.Instance.GameSaveData.DungeonSaveData.EndFloor = data.EndFloor;
			SaveSystem.SaveData(Common.Instance.GameSaveData);
			SceneManager.LoadScene("DungeonScene");
		});
	}

	public void Cancel_Clicked()
	{
		CloseDialog();
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
	}
}
