using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillGridItem : MonoBehaviour
{
	public TextMeshProUGUI SkillText;
	public Button GridButton;
	public Image ActiveImage;

	private TogglableSkillGridItem _data;
	private float clickCooldownSeconds;

	public Action SkillToggledCallback { get; internal set; }

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
				//TODO prompt are you sure;
				_data.Active = !_data.Active;
				var overWorld = FindFirstObjectByType<Overworld>();
				overWorld.OverworldPlayer.Gold -= _data.Skill.LearnCost;
				UpdateUI();
				SkillToggledCallback?.Invoke();
			}
			else
			{
				var messageDialog = Common.Instance.MessageDialog;
				messageDialog.PromptText.text = "Not enough gold to buy skill";
				messageDialog.gameObject.SetActive(true);

				OverworldMenuManager.Open(messageDialog);
			}
		}
	}

	private bool CanAfford()
	{
		var overWorld = FindFirstObjectByType<Overworld>();
		return _data.Skill.LearnCost <= overWorld.OverworldPlayer.Gold;
	}

	private void UpdateUI()
	{
		SkillText.text = $"{_data.Skill.SkillName} ({_data.Skill.LearnCost})";
		if (_data.Active)
		{
			ActiveImage.color = Color.green;
		}
		else
		{
			ActiveImage.color = Color.white;
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
