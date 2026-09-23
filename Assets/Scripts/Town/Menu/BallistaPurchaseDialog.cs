using JuicyChickenGames.Menu;
using System;
using TMPro;
using UnityEngine.UI;

public class BallistaPurchaseDialog : Dialog
{
	public Button PurchaseButton;
	public Button CancelButton;

    public TextMeshProUGUI TitleText;
    public TextMeshProUGUI DescriptionText;

	public Action PurcahseCallBack { get; internal set; }

	internal override void SetFirstSelect()
	{
		CancelButton.Select();
	}

    public void SetNavigation()
    {
        // Create navigation settings for each button
        Navigation purchaseNav = PurchaseButton.navigation;
        Navigation cancelNav = CancelButton.navigation;

        // Use explicit mode to fully control navigation
        purchaseNav.mode = Navigation.Mode.Explicit;
        cancelNav.mode = Navigation.Mode.Explicit;

        // Link the two buttons to each other
        purchaseNav.selectOnLeft = CancelButton;
        purchaseNav.selectOnRight = CancelButton;
        purchaseNav.selectOnUp = CancelButton;
        purchaseNav.selectOnDown = CancelButton;

        cancelNav.selectOnLeft = PurchaseButton;
        cancelNav.selectOnRight = PurchaseButton;
        cancelNav.selectOnUp = PurchaseButton;
        cancelNav.selectOnDown = PurchaseButton;

        // Apply changes back to the buttons
        PurchaseButton.navigation = purchaseNav;
        CancelButton.navigation = cancelNav;
    }

	internal void Setup(Skill skill)
	{
		Setup(skill, 1, skill.LearnCost);
	}

	internal void Setup(Skill skill, int nextRank, int cost)
	{
        FindFirstObjectByType<TownMenuManager>().Open(this);

        TitleText.text = nextRank <= 1
            ? $"Learn {skill.SkillName} ({cost}g)?"
            : $"Train {skill.SkillName} to rank {nextRank} ({cost}g)?";
        DescriptionText.text = skill.Description;
        SetNavigation();
	}

    public void Purchase_Clicked()
    {
        var callback = PurcahseCallBack;
        PurcahseCallBack = null;
        CloseDialog();
        callback?.Invoke();
    }

    public void Cancel_ClickeD()
    {
        CloseDialog();
    }
}