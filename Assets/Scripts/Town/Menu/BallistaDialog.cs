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
	public TownAlly Character { get; internal set; }

	public BallistaPurchaseDialog BallistaPurchaseDialog;

    public override void PrepareTown(TownInteractionContext context)
    {
        Character = context.Player.ControllingTownAlly;
        Show();
    }

	internal override void SetFirstSelect()
	{
		EventSystem.current.SetSelectedGameObject(null);
		if (SkillGridItems.Count > 0) SkillGridItems[0].GridButton.Select();
	}

	internal void Show()
	{
		BallistaPurchaseDialog.gameObject.SetActive(false);
		this.gameObject.SetActive(true);
		var activeSkills = Character.Skills;

		List<TogglableSkillGridItem> datas = FindFirstObjectByType<Town>().Configuration.LearnableSkills
			.Select(x => new TogglableSkillGridItem()
			{
				Active = activeSkills.Contains(x.SkillName),
				Skill = x
			}).ToList();
		Action<SkillGridItem, TogglableSkillGridItem> action = (view, data) =>
		{
			view.Setup(data);
            view.Character = Character;
			view.SkillToggledCallback = HandleSkillChanged;
			view.BallistaPurchaseDialog = BallistaPurchaseDialog;
		};
		SkillGridItems = SkillGridContainer.RePopulateObjects(SkillGridSelectablePrefab, datas, action);
		UpdateUI();
	}

	private void HandleSkillChanged()
	{
		UpdateUI();
	}

	private void UpdateUI()
	{
		var active = SkillGridItems.Count(x => x.IsSkillActive());
		var classLabel = Character != null ? HeroClass.Label(Character.PrimaryClass, Character.SecondaryClass) : "";
		SkillsText.text = string.IsNullOrEmpty(classLabel) ? $"Skills ({active})" : $"{classLabel} - Skills ({active})";
	}

	public void Close_Clicked()
	{

		CloseDialog();
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
