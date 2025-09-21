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
			//TODO prompt are you sure;
			_data.Active = !_data.Active;
			UpdateUI();
			SkillToggledCallback?.Invoke();
		}
	}

	private void UpdateUI()
	{
		SkillText.text = $"{_data.Skill.SkillName} ({_data.Skill.SPCost})";
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
