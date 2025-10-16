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
		items[0].Button.Select();
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
			selectOnUp = items[^1].Button,
			selectOnDown = items[0].Button
		};
		CancelButton.navigation = cancelNav;
	}

	public void DungeonClicked(DungeonTierData data)
	{
		var overworld = FindFirstObjectByType<Overworld>();
		overworld.WriteSaveData();
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			Common.Instance.GameSaveData.OverworldSaveData.RecruitedAlliesData =
			overworld.OverworldPlayer.RecruitedAllies.Select(x => new OverworldAllyData()
			{
				AllyName = x.Name
			}).ToList();

			Common.Instance.GameSaveData.DungeonSaveData.StartFloor = data.StartFloor;
			Common.Instance.GameSaveData.DungeonSaveData.EndFloor = data.EndFloor;
			SaveSystem.SaveData(Common.Instance.GameSaveData);
			SceneManager.LoadScene("DungeonScene");
		});
	}

	public void Cancel_Clicked()
	{
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
	}
}
