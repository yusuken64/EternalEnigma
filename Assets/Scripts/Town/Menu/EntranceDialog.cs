using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
        FitLayout();
        GetComponent<NavigationHandler>()?.ConfigureCompactPointer(this);
        var campaign = Common.Instance.CampaignContext;
        if (campaign != null)
        {
            var town = FindFirstObjectByType<Town>();
            var dungeons = campaign.Campaign.Locations.Where(l => l.ParentTownId == town.Configuration.Id).ToList();
            items = Container.RePopulateObjects(DungeonTierItemPrefab, dungeons, (view, location) =>
                view.SetupCampaign(location, campaign.Completed.Contains(location.Id), () =>
                {
                    if (!Common.Instance.Travel.EnterTownDungeon(town, location.Id))
                        TownMenu.ShowMessage(!campaign.CanEnterDungeon(location.Id)
                            ? campaign.OpeningPreparationHint : "This dungeon cannot be entered right now.");
                }));
            return;
        }
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

    private void FitLayout()
    {
        var prompt = transform.Find("Prompt")?.GetComponent<TMP_Text>();
        if (prompt != null)
        {
            Fit(prompt.transform, .15f, .84f, .85f, .95f);
            prompt.enableAutoSizing = true;
            prompt.fontSizeMin = 24;
            prompt.fontSizeMax = 42;
            prompt.textWrappingMode = TextWrappingModes.NoWrap;
            prompt.margin = Vector4.zero;
            prompt.raycastTarget = false;
        }
        Fit(Container, .24f, .22f, .76f, .79f);
        var rows = Container.GetComponent<VerticalLayoutGroup>();
        if (rows != null) rows.spacing = 8;
        // Keep the authored illustration in the space beside the choices. Its
        // original fixed rectangle extends over the centered Cancel action.
        if (transform.Find("Illustration") is RectTransform illustration)
        {
            illustration.anchorMin = illustration.anchorMax = new Vector2(.12f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            illustration.sizeDelta = new Vector2(160, 160);
        }
        Fit(CancelButton.transform.parent, .30f, .055f, .70f, .155f);
        var buttons = CancelButton.transform.parent.GetComponent<HorizontalLayoutGroup>();
        if (buttons != null)
        {
            buttons.childControlWidth = buttons.childControlHeight = true;
            buttons.childForceExpandWidth = buttons.childForceExpandHeight = true;
        }
        var label = CancelButton.GetComponentInChildren<TMP_Text>();
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = 28;
    }

	internal override void SetFirstSelect()
	{
		(items?.FirstOrDefault(item => item.Button.interactable)?.Button ?? CancelButton).Select();
	}

	public void SetNavigation()
	{
        var available = items.Where(item => item.Button.interactable).Select(item => item.Button).ToList();
        available.Add(CancelButton);
        for (int i = 0; i < available.Count; i++)
            available[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = available[(i + available.Count - 1) % available.Count],
                selectOnDown = available[(i + 1) % available.Count]
            };
	}

	public void DungeonClicked(DungeonTierData data)
	{
		var town = FindFirstObjectByType<Town>();
        if (Common.Instance.CampaignContext != null) return;
        if (!town.Services.CanEnter(data))
        {
            TownMenu.ShowMessage("This dungeon tier is unavailable.");
            return;
        }
        town.WriteSaveData();
        Common.Instance.GameSaveData.DungeonSaveData.ReturnCommitted = false;
        Common.Instance.GameSaveData.DungeonSaveData.VisualSelection = DungeonVisualSelection.Resolve(null);
		Common.Instance.ScreenTransition.DoTransition(() =>
		{
			Common.Instance.GameSaveData.DungeonSaveData.StartFloor = data.StartFloor;
			Common.Instance.GameSaveData.DungeonSaveData.EndFloor = data.EndFloor;
			SaveSystem.Capture(Common.Instance);
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
