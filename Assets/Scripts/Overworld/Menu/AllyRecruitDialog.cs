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
	private OverworldAlly _ally;

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
	internal void Show(OverworldAlly ally, AllyRecruitDialogMode allyRecruitDialogMode)
	{
		this._ally = ally;
		this.AllyRecruitDialogMode = allyRecruitDialogMode;

		FaceCamDisplay.SetFollow(ally.VisualParent);
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
				RecruitButtonText.text = "Recruit (300g)";
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
		var player = FindAnyObjectByType<OverworldPlayer>();
		if (player.RecruitedAllies.Count >= 1)
		{
			var overworld = FindAnyObjectByType<Overworld>();
			RemoveAlly(overworld, _ally);
		}

		FaceCamDisplay.Unfollow(_ally.VisualParent);
		FindFirstObjectByType<OverworldMenuManager>().Close(this);
		CloseAction?.Invoke();
	}

	public void Recruit_Clicked()
	{
		var player = FindAnyObjectByType<OverworldPlayer>();
		if (player.RecruitedAllies.Count >= 4)
		{
			var messageDialog = Common.Instance.MessageDialog;
			messageDialog.PromptText.text = "Max Part Size is 4";
			messageDialog.gameObject.SetActive(true);

			FindFirstObjectByType<OverworldMenuManager>().Open(messageDialog);
			return;
		}

		var overworld = FindAnyObjectByType<Overworld>();
		List<OverworldAlly> overworldAllies = overworld.OverworldAllies;
		var allyCost = 300;

		if (player.Gold >= allyCost &&
			overworldAllies.Contains(_ally))
		{
			player.Gold -= allyCost;
			Recruit(overworld, _ally);
		}

		FaceCamDisplay.Unfollow(_ally.VisualParent);
		FindFirstObjectByType<OverworldMenuManager>().Close(this);
		CloseAction?.Invoke();
	}

	public static void Recruit(Overworld overworld, OverworldAlly ally)
	{
		overworld.OverworldAllies.Remove(ally);
		overworld.OverworldPlayer.RecruitedAllies.Add(ally);
		Common.Instance.InstantiatedOverworldAllies.Add(ally);
		ally.transform.SetParent(Common.Instance.OverworldAllyParent);
	}

	public static void RemoveAlly(Overworld overworld, OverworldAlly ally)
	{
		overworld.OverworldAllies.Add(ally);

		overworld.OverworldPlayer.RecruitedAllies.Remove(ally);
		Common.Instance.InstantiatedOverworldAllies.Remove(ally);

		var parent = FindObjectOfType<OverworldAllyManager>();
		ally.transform.SetParent(parent.transform);

	}

	public void Cancel_Clicked()
	{
		FaceCamDisplay.Unfollow(_ally.VisualParent);
		FindFirstObjectByType<OverworldMenuManager>().Close(this);
		CloseAction?.Invoke();
	}
}

public enum AllyRecruitDialogMode
{
	Recruit,
	Talk,
	Info
}