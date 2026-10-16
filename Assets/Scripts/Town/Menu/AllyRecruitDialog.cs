using JuicyChickenGames.Menu;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllyRecruitDialog : Dialog
{
	public TextMeshProUGUI NameText;
	public TextMeshProUGUI DescriptionText;

	public GameObject RecruitButtons;
	public Button RecruitButton;
	public TextMeshProUGUI RecruitButtonText;
	public Button CancelButton;

	public GameObject RemovelButtons;
	public Button RemoveButton;
	public Button CancelButton2;

	public AllyRecruitDialogMode AllyRecruitDialogMode;

	public FaceCamDisplay FaceCamDisplay;
	private TownAlly _ally;
    private System.Action ClosedPortrait;
    private void OnDisable() { ClosedPortrait?.Invoke(); ClosedPortrait = null; }

	internal override void SetFirstSelect()
	{
		switch (AllyRecruitDialogMode)
		{
			case AllyRecruitDialogMode.Recruit:
				CancelButton.Select();
				break;
			case AllyRecruitDialogMode.Talk:
				CancelButton2.Select();
				break;
		}
	}
	internal void Show(TownAlly ally, AllyRecruitDialogMode allyRecruitDialogMode)
	{
		this._ally = ally;
		this.AllyRecruitDialogMode = allyRecruitDialogMode;

		FaceCamDisplay.SetFollow(ally.VisualParent);
        ClosedPortrait = () => FaceCamDisplay.Unfollow(ally.VisualParent);
		UpdateUI();
	}

	private void UpdateUI()
	{
        var navigation = GetComponent<NavigationHandler>();
        if (navigation != null) navigation.ConfigureCompactPointer(this);
        // This retired decorative control still carries a migrated, empty parchment panel.
        transform.Find("CharacterBG_Board/FeatherObj")?.gameObject.SetActive(false);
		var classLabel = HeroClass.Label(_ally.PrimaryClass, _ally.SecondaryClass);
		NameText.text = GameplayText.Format(_ally.Name);
        NameText.enableAutoSizing = true;
        NameText.fontSizeMin = 32; NameText.fontSizeMax = 64;
        NameText.textWrappingMode = TextWrappingModes.NoWrap;
        NameText.alignment = TextAlignmentOptions.Center;
		DescriptionText.text = GameplayText.Format((string.IsNullOrEmpty(classLabel) ? "" : classLabel + "\n\n") + _ally.Description);
        DescriptionText.font = TMP_Settings.defaultFontAsset;
        DescriptionText.enableAutoSizing = true;
        DescriptionText.fontSizeMin = 16; DescriptionText.fontSizeMax = 22;
        DescriptionText.textWrappingMode = TextWrappingModes.Normal;
        DescriptionText.alignment = TextAlignmentOptions.TopLeft;
        var town = FindFirstObjectByType<Town>();
        var player = town.TownPlayer;
        string blockedReason = null;

		switch (AllyRecruitDialogMode)
		{
			case AllyRecruitDialogMode.Recruit:
				RecruitButtons.gameObject.SetActive(true);
				RemovelButtons.gameObject.SetActive(false);
				RecruitButtonText.text = GameplayText.Format($"Recruit for {_ally.RecruitCost} gold");
                RecruitButtonText.enableAutoSizing = true;
                RecruitButtonText.fontSizeMin = 14;
                RecruitButtonText.fontSizeMax = 24;
                RecruitButtonText.margin = new Vector4(20, 3, 20, 3);
                RecruitButtonText.textWrappingMode = TextWrappingModes.NoWrap;
                RecruitButtonText.alignment = TextAlignmentOptions.Center;
                RecruitButtonText.raycastTarget = false;
                blockedReason = player.RecruitedAllies.Count >= town.Configuration.MaxPartySize ? $"Party limit is {town.Configuration.MaxPartySize}." :
                    !town.TownAllies.Contains(_ally) ? "This ally is no longer available." :
                    player.Gold < _ally.RecruitCost ? $"Costs {_ally.RecruitCost} gold; you have {player.Gold}." : null;
                RecruitButton.interactable = true;
				break;
			case AllyRecruitDialogMode.Talk:
				RecruitButtons.gameObject.SetActive(false);
				RemovelButtons.gameObject.SetActive(true);
                blockedReason = player.RecruitedAllies.Count <= 1 ? "Keep at least one party member." :
                    !player.RecruitedAllies.Contains(_ally) ? "This ally is not in the party." :
                    Common.Instance.CampaignContext != null && _ally.Id == Common.Instance.GameSaveData.ProtagonistId ? "The protagonist stays in the party." : null;
                RemoveButton.interactable = true;
				break;
			case AllyRecruitDialogMode.Info:
				break;
		}
        if (blockedReason != null) DescriptionText.text += GameplayText.Format("\n\n" + blockedReason);
	}

	public void Remove_Clicked()
	{
        if (!FindFirstObjectByType<Town>().Services.Dismiss(_ally, out var reason))
        {
            Reject(reason);
            return;
        }
        CloseDialog();
    }

    public void Recruit_Clicked()
    {
        if (!FindFirstObjectByType<Town>().Services.Recruit(_ally, out var reason))
        {
            Reject(reason);
            return;
        }
        CloseDialog();
    }

    private void Reject(string reason)
    {
        UpdateUI();
        if (!DescriptionText.text.Contains(reason)) DescriptionText.text += GameplayText.Format("\n\n" + reason);
        GameMessages.Reject(reason);
    }

	public static void Recruit(Town town, TownAlly ally)
	{
		town.TownAllies.Remove(ally);
		town.TownPlayer.RecruitedAllies.Add(ally);
		Common.Instance.InstantiatedTownAllies.Add(ally);
		ally.transform.SetParent(Common.Instance.TownAllyParent);
		ally.RefreshEquipmentVisuals();
	}

	public static void RemoveAlly(Town town, TownAlly ally)
	{
		town.TownAllies.Add(ally);

		town.TownPlayer.RecruitedAllies.Remove(ally);
		Common.Instance.InstantiatedTownAllies.Remove(ally);

		var parent = FindFirstObjectByType<TownAllyManager>();
		ally.transform.SetParent(parent.transform);

	}

	public void Cancel_Clicked()
	{
		FaceCamDisplay.Unfollow(_ally.VisualParent);
		CloseDialog();
	}
}

public enum AllyRecruitDialogMode
{
	Recruit,
	Talk,
	Info
}
