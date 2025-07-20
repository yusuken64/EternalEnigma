using JuicyChickenGames.Menu;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class AllyRecruitDialog : Dialog
{
	public TextMeshProUGUI NameText;
	public TextMeshProUGUI DescriptionText;

	public Button CancelButton;
	public Button RecruitButton;
	public TextMeshProUGUI RecruitButtonText;

	public FaceCamDisplay FaceCamDisplay;

	private OverworldAlly _ally;

	internal override void SetFirstSelect()
	{
		CancelButton.Select();
	}

	internal void Show(OverworldAlly ally)
	{
		this._ally = ally;

		FaceCamDisplay.SetFollow(ally.VisualParent);

		NameText.text = ally.Name;
		DescriptionText.text = ally.Description;

		RecruitButtonText.text = "Recruit (300g)";
	}

	public void Recruit_Clicked()
	{
		var player = FindAnyObjectByType<OverworldPlayer>();
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
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	public static void Recruit(Overworld overworld, OverworldAlly ally)
	{
		overworld.OverworldAllies.Remove(ally);
		overworld.OverworldPlayer.RecruitedAllies.Add(ally);
		Common.Instance.InstantiatedOverworldAllies.Add(ally);
		ally.transform.SetParent(Common.Instance.OverworldAllyParent);
	}

	public void Cancel_Clicked()
	{
		FaceCamDisplay.Unfollow(_ally.VisualParent);
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}
}
