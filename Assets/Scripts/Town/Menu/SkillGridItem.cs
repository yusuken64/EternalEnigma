using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SkillGridItem : MonoBehaviour, ISelectHandler, IDeselectHandler
{
	public TextMeshProUGUI SkillText;
    public TextMeshProUGUI RankText;
	public Button GridButton;
	public Image ActiveImage;
	public Image SkillIcon;

	public GameObject CostObject;
	public TextMeshProUGUI CostText;

	private TogglableSkillGridItem _data;
    public TownAlly Character { get; set; }
	private float clickCooldownSeconds;

	public Action SkillToggledCallback { get; internal set; }
    public Action<SkillGridItem, BaseEventData> SelectedCallback { get; internal set; }
    public TrainerOffer Offer => _data?.Offer;
    public void OnSelect(BaseEventData eventData)
    {
        if (ActiveImage != null) ActiveImage.enabled = true;
        SelectedCallback?.Invoke(this, eventData);
    }
    public void OnDeselect(BaseEventData eventData) { if (ActiveImage != null) ActiveImage.enabled = false; }
	public BallistaPurchaseDialog BallistaPurchaseDialog { get; internal set; }

	private void Update()
	{
		clickCooldownSeconds -= Time.deltaTime;
	}

	public void ToggleOn_Clicked()
	{
		if (clickCooldownSeconds > 0)
		{
			return;
		}

		clickCooldownSeconds = 0.4f;
		var town = FindFirstObjectByType<Town>();
        if (!town.Services.CanLearn(Character, _data.Skill, out var offer, out var unavailable))
        {
            GetComponentInParent<BallistaDialog>()?.Reject(unavailable);
            return;
        }
		BallistaPurchaseDialog.Setup(offer.Skill, offer.CurrentRank + 1, offer.NextCost, offer.UsesPoints);
        BallistaPurchaseDialog.CloseAction = () => SkillToggledCallback?.Invoke();
		BallistaPurchaseDialog.PurcahseCallBack = () =>
		{
			if (!town.Services.CanLearn(Character, offer.Skill, out var current, out var unavailableNow))
			{
				BallistaPurchaseDialog.Reject(unavailableNow);
				return;
			}
            if (current.CurrentRank != offer.CurrentRank || current.NextCost != offer.NextCost || current.UsesPoints != offer.UsesPoints)
            {
                BallistaPurchaseDialog.Reject("This skill offer changed. Choose the skill again.");
                return;
            }
			if (!town.Services.Learn(Character, offer.Skill, out var reason))
			{
				BallistaPurchaseDialog.Reject(reason);
				return;
			}
            BallistaPurchaseDialog.CloseDialog();
		};
	}

	private void UpdateUI()
	{
		var offer = _data.Offer;
		SkillText.text = GameplayText.Format(_data.Skill.DisplayName);
		SkillIcon = SkillIconView.Bind(SkillIcon, SkillText, _data.Skill.Icon);
        bool maxed = offer != null ? offer.IsMaxed : _data.Active;
        bool locked = offer != null && !offer.CanLearn && !maxed;
        if (RankText == null)
        {
            var rank = new GameObject("Rank and availability", typeof(RectTransform), typeof(TextMeshProUGUI));
            rank.transform.SetParent(SkillText.transform.parent, false);
            RankText = rank.GetComponent<TextMeshProUGUI>();
            RankText.font = SkillText.font;
        }
        var layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 72;
        Fit(SkillText.rectTransform, new Vector2(.07f, .39f), new Vector2(.76f, .94f));
        SkillText.fontSizeMin = 14; SkillText.fontSizeMax = 20; SkillText.enableAutoSizing = true;
        SkillText.alignment = TextAlignmentOptions.MidlineLeft;
        SkillText.textWrappingMode = TextWrappingModes.Normal;
        SkillText.raycastTarget = false;
        Fit(RankText.rectTransform, new Vector2(.07f, .08f), new Vector2(.76f, .39f));
        RankText.fontSize = 16; RankText.enableAutoSizing = true; RankText.fontSizeMin = 13; RankText.fontSizeMax = 16;
        RankText.alignment = TextAlignmentOptions.MidlineLeft; RankText.raycastTarget = false;
        RankText.color = GameUITheme.Ink;
        RankText.margin = SkillText.margin;
        RankText.text = GameplayText.Format(offer == null ? (maxed ? "Learned" : "Available") :
            $"Rank {offer.CurrentRank} of {offer.MaxRank}" + (maxed ? " · Complete" : locked ? " · Locked" : " · Available"));
        Fit(CostText.rectTransform, new Vector2(.77f, .15f), new Vector2(.97f, .85f));
        CostText.fontSizeMin = 13; CostText.fontSizeMax = 17; CostText.enableAutoSizing = true;
        CostText.alignment = TextAlignmentOptions.Center; CostText.raycastTarget = false;
        CostText.textWrappingMode = TextWrappingModes.Normal;
        CostText.text = GameplayText.Format(maxed ? "Max rank" : offer != null && offer.UsesPoints ? $"{offer.NextCost}\nSkill Points" : $"{(offer != null ? offer.NextCost : _data.Skill.LearnCost)} gold");
        GridButton.interactable = true;
        CostObject.SetActive(true);
        ActiveImage.color = new Color(.55f, .30f, .08f, 1);
        ActiveImage.enabled = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == GridButton.gameObject;
	}

    private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

	internal void Setup(TogglableSkillGridItem data)
	{
		this._data = data;
		UpdateUI();
	}

	internal bool IsSkillActive()
	{
		return _data.Active;
	}

	internal Skill GetSkill()
	{
		return _data.Skill;
	}
}
