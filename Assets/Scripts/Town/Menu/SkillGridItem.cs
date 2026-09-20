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
		if (!_data.Active)
		{
			if (CanAfford())
			{
				BallistaPurchaseDialog.Setup(_data.Skill);
				BallistaPurchaseDialog.PurcahseCallBack = () =>
				{
                    var town = FindFirstObjectByType<Town>();
                    if (!town.Services.Learn(Character, _data.Skill, out var reason))
                    {
                        TownMenu.ShowMessage(reason);
                        return;
                    }
                    _data.Active = true;
                    UpdateUI();
                    SkillToggledCallback?.Invoke();
				};
			}
			else
			{
				var messageDialog = Common.Instance.MessageDialog;
				messageDialog.PromptText.text = "Not enough gold to buy skill";
				messageDialog.gameObject.SetActive(true);

				FindFirstObjectByType<TownMenuManager>().Open(messageDialog);
			}
		}
	}

	private bool CanAfford()
	{
		var town = FindFirstObjectByType<Town>();
		return _data.Skill.LearnCost <= town.TownPlayer.Gold;
	}

	private void UpdateUI()
	{
		SkillText.text = $"{_data.Skill.SkillName}";
		if (_data.Active)
		{
			ActiveImage.color = Color.green;
			CostObject.gameObject.SetActive(false);
		}
		else
		{
			ActiveImage.color = Color.white;
			CostText.text = $"{_data.Skill.LearnCost}";
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
