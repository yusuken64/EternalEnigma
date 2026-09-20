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
		NameText.text = _ally.Name;
		DescriptionText.text = _ally.Description;

		switch (AllyRecruitDialogMode)
		{
			case AllyRecruitDialogMode.Recruit:
				RecruitButtons.gameObject.SetActive(true);
				RemovelButtons.gameObject.SetActive(false);
				RecruitButtonText.text = $"Recruit ({_ally.RecruitCost}g)";
				break;
			case AllyRecruitDialogMode.Talk:
				RecruitButtons.gameObject.SetActive(false);
				RemovelButtons.gameObject.SetActive(true);
				break;
			case AllyRecruitDialogMode.Info:
				break;
		}
	}

	public void Remove_Clicked()
	{
        if (!FindFirstObjectByType<Town>().Services.Dismiss(_ally, out var reason))
        {
            TownMenu.ShowMessage(reason);
            return;
        }
        CloseDialog();
    }

    public void Recruit_Clicked()
    {
        if (!FindFirstObjectByType<Town>().Services.Recruit(_ally, out var reason))
        {
            TownMenu.ShowMessage(reason);
            return;
        }
        CloseDialog();
    }

	public static void Recruit(Town town, TownAlly ally)
	{
		town.TownAllies.Remove(ally);
		town.TownPlayer.RecruitedAllies.Add(ally);
		Common.Instance.InstantiatedTownAllies.Add(ally);
		ally.transform.SetParent(Common.Instance.TownAllyParent);
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