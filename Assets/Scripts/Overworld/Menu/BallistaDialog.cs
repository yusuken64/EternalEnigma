using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class BallistaDialog : Dialog
{
	public TextMeshProUGUI SkillsText;

	public SkillGridItem SkillGridSelectablePrefab;
	public Transform SkillGridContainer;
	public List<SkillGridItem> SkillGridItems { get; private set; }

	internal override void SetFirstSelect()
	{
		EventSystem.current.SetSelectedGameObject(null);
		EventSystem.current.SetSelectedGameObject(SkillGridItems[0].GridButton.gameObject);
	}

	internal void Show()
	{
		this.gameObject.SetActive(true);
		_maxSkills = Common.Instance.GameSaveData.OverworldSaveData.ActiveSkillMax;
		var activeSkills = Common.Instance.GameSaveData.OverworldSaveData.ActiveSkillNames;

		List<TogglableSkillGridItem> datas = Common.Instance.SkillManager.SkillPrefabs.Select(x => new TogglableSkillGridItem()
		{
			Active = activeSkills.Contains(x.SkillName),
			Skill = x
		}).ToList();
		Action<SkillGridItem, TogglableSkillGridItem> action = (view, data) =>
		{
			view.Setup(data, skillsFull);
			view.SkillToggledCallback = HandleSkillChanged;
		};
		SkillGridItems = SkillGridContainer.RePopulateObjects(SkillGridSelectablePrefab, datas, action);
		UpdateUI();
	}

	private int _maxSkills = 2;//TODO get from playerdata

	private bool skillsFull()
	{
		var active = SkillGridItems.Count(x => x.IsSkillActive());
		return active >= _maxSkills;
	}

	private void HandleSkillChanged()
	{
		UpdateUI();
	}

	private void UpdateUI()
	{
		var active = SkillGridItems.Count(x => x.IsSkillActive());
		SkillsText.text = $"Skills ({active}/{_maxSkills})";
	}

	public void Close_Clicked()
	{
		FindAnyObjectByType<Overworld>().WriteSaveData();
		OverworldMenuManager.Close(this);
		CloseAction?.Invoke();
	}

	[ContextMenu("Force Select")]
	public void ForceSelect()
	{
		SetFirstSelect();
	}

	internal List<string> GetActiveSkillsSave()
	{
		if (SkillGridItems == null)
		{
			return new();
		}

		var active = SkillGridItems
			.Where(x => x.IsSkillActive())
			.Select(x => x.GetSkill())
			.Select(x => x.SkillName);

		return active.ToList();
	}
}

public class TogglableSkillGridItem
{
	public bool Active;
	public Skill Skill;
}