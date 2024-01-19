using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillGridItem : MonoBehaviour
{
	public TextMeshProUGUI SkillText;
	public Button GridButton;
	public Image ActiveImage;

	private TogglableSkillGridItem _data;
	private Func<bool> _skillsFull;
	private float clickCooldownSeconds;

	public Action SkillToggledCallback { get; internal set; }

	private void Update()
	{
		clickCooldownSeconds -= Time.deltaTime;
	}

	public void ToggleOn_Clicked()
	{
		if (clickCooldownSeconds <= 0)
		{
			clickCooldownSeconds = 0.4f;
			bool skillsFull = _skillsFull();
			if (!skillsFull ||
				(skillsFull && _data.Active))
			{
				_data.Active = !_data.Active;
				UpdateUI();
				SkillToggledCallback?.Invoke();
			}
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

	internal void Setup(TogglableSkillGridItem data, Func<bool> skillsFull)
	{
		this._data = data;
		this._skillsFull = skillsFull;
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
