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
    public Image SkillIcon;

	public Action PurcahseCallBack { get; internal set; }

	internal override void SetFirstSelect()
	{
		PurchaseButton.Select();
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

	internal void Setup(Skill skill, int nextRank, int cost, bool usesPoints = false)
	{
        FindFirstObjectByType<TownMenuManager>().Open(this);
        TownModalBackdrop.Ensure(this, true);

        TitleText.font = TMP_Settings.defaultFontAsset;
        TitleText.enableAutoSizing = true; TitleText.fontSizeMin = 18; TitleText.fontSizeMax = 28;
        TitleText.textWrappingMode = TextWrappingModes.Normal;
        DescriptionText.enableAutoSizing = true; DescriptionText.fontSizeMin = 16; DescriptionText.fontSizeMax = 24;
        DescriptionText.textWrappingMode = TextWrappingModes.Normal;
        string price = usesPoints ? $"{cost} Skill Point{(cost == 1 ? "" : "s")}" : $"{cost} gold";
        TitleText.text = GameplayText.Format(nextRank <= 1
            ? $"Learn {skill.DisplayName} for {price}?"
            : $"Train {skill.DisplayName} to rank {nextRank} for {price}?");
        DescriptionText.text = GameplayText.Format(skill.Description + "\n\n" + SkillPreviewText.Usage(skill));
        PurchaseButton.GetComponentInChildren<TMP_Text>().text = "Learn";
        GameUISkin.PadButtonLabel(PurchaseButton);
        GameUISkin.PadButtonLabel(CancelButton);
        SkillIcon = SkillIconView.Bind(SkillIcon, TitleText, skill.Icon);
        SetNavigation();
	}

    public void Purchase_Clicked()
    {
        if (!gameObject.activeInHierarchy) return;
        PurcahseCallBack?.Invoke();
    }

    public void Reject(string reason)
    {
        DescriptionText.text = GameplayText.Format(reason);
        GameMessages.Reject(reason);
    }

    public void Cancel_ClickeD()
    {
        CloseDialog();
    }

    private void OnDisable() => PurcahseCallBack = null;
}
