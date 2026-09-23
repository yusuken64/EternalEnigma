using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillGridItem : MonoBehaviour
{
	public TextMeshProUGUI SkillText;
	public Button GridButton;
	public Image ActiveImage;

	public GameObject CostObject;
	public TextMeshProUGUI CostText;

	private TogglableSkillGridItem _data;
    public TownAlly Character { get; set; }
	private float clickCooldownSeconds;

	public Action SkillToggledCallback { get; internal set; }
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
		var offer = _data.Offer;
		if (offer == null || offer.IsMaxed) return;
		if (!offer.CanLearn)
		{
			TownMenu.ShowMessage(string.IsNullOrEmpty(offer.LockReason) ? "This skill cannot be learned." : offer.LockReason);
			return;
		}
		if (!CanAfford())
		{
			var messageDialog = Common.Instance.MessageDialog;
			messageDialog.PromptText.text = "Not enough gold to buy skill";
			messageDialog.gameObject.SetActive(true);
			FindFirstObjectByType<TownMenuManager>().Open(messageDialog);
			return;
		}
		BallistaPurchaseDialog.Setup(offer.Skill, offer.CurrentRank + 1, offer.NextCost);
		BallistaPurchaseDialog.PurcahseCallBack = () =>
		{
			var town = FindFirstObjectByType<Town>();
			if (!town.Services.Learn(Character, offer.Skill, out var reason))
			{
				TownMenu.ShowMessage(reason);
				return;
			}
			SkillToggledCallback?.Invoke();
		};
	}

	private bool CanAfford()
	{
		var town = FindFirstObjectByType<Town>();
		return _data.Offer != null ? _data.Offer.NextCost <= town.TownPlayer.Gold : _data.Skill.LearnCost <= town.TownPlayer.Gold;
	}

	private void UpdateUI()
	{
		var offer = _data.Offer;
		SkillText.text = offer != null ? offer.Label : $"{_data.Skill.SkillName}";
		bool maxed = offer != null ? offer.IsMaxed : _data.Active;
		if (maxed)
		{
			ActiveImage.color = Color.green;
			CostObject.gameObject.SetActive(false);
		}
		else
		{
			bool locked = offer != null && !offer.CanLearn;
			ActiveImage.color = locked ? Color.gray : (_data.Active ? Color.cyan : Color.white);
			CostText.text = $"{(offer != null ? offer.NextCost : _data.Skill.LearnCost)}";
			CostObject.gameObject.SetActive(true);
		}
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
