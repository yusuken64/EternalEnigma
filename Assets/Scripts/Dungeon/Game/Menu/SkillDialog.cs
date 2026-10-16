using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
	public class SkillDialog : Dialog
    {
        public Transform ButtonContainer;
        public DynamicActionButton ActionButtonPrefab;

        public List<DynamicActionButton> Buttons;

        public void Setup(Character character)
        {
            var dynamicActionInfos = character.Skills.Where(skill => skill != null && skill.ActivationType == ActivationType.Active).Select((skill, index) =>
            {
                return new DynamicActionInfo()
                {
                    ActionName = SkillLabel(skill, character) + (skill.InitialCastTime(character) > 0 ? $"  Charges for {skill.InitialCastTime(character)} actions" : ""),
                    Icon = skill.Icon,
                    ClickAction = () =>
                    {
                        if (character.CanCast(skill, out string reason))
                        {
                            if (skill.Targeting == SkillTargeting.InventoryItem)
                                MenuManager.Instance.OpenInventoryTargetingMenu(character, skill);
                            else if (skill.RequiresTargetSelection)
                                MenuManager.Instance.OpenTargetingMenu(character, skill);
                            else
                            {
                                MenuManager.Instance.CloseAllMenus();
                                character.SetAction(new SkillAction(character, skill, character));
                            }
                        }
						else
						{
                            GameMessages.Post(reason, true);
						}
                    }
                };
            }).ToList();

            //dynamicActionInfos.Add(new DynamicActionInfo()
            //{
            //    ActionName = "Cancel",
            //    ClickAction = () => {
            //        Close();
            //    },
            //    Data = null
            //});

            Action<DynamicActionButton, DynamicActionInfo> setupAction = (view, data) =>
            {
                view.Setup(data);
            };
            Buttons = ButtonContainer.RePopulateObjects(ActionButtonPrefab, dynamicActionInfos, setupAction);
        }


        internal void SetupTown(TownAlly controllingTownAlly)
        {
            var dynamicActionInfos = controllingTownAlly.Skills.Select((skill, index) =>
            {
                return new DynamicActionInfo()
                {
                    ActionName = GameplayText.Name(skill) + (controllingTownAlly.GetRank(skill) > 1 ? $"  Rank {controllingTownAlly.GetRank(skill)}" : ""),
                    Icon = Common.Instance.SkillManager.GetSkillByName(skill)?.Icon,
                    ClickAction = () =>
                    {
                        var definition = Common.Instance.SkillManager.GetSkillByName(skill);
                        TownMenu.ShowMessage(definition != null
                            ? $"{definition.DisplayName}\nRank {Mathf.Max(1, controllingTownAlly.GetRank(skill))}, costs {definition.CostText()}\n{definition.Description}\nUse active skills in the dungeon."
                            : "This skill is currently unavailable.");
                    }
                };
            }).ToList();

            Action<DynamicActionButton, DynamicActionInfo> setupAction = (view, data) =>
            {
                view.Setup(data);
            };
            Buttons = ButtonContainer.RePopulateObjects(ActionButtonPrefab, dynamicActionInfos, setupAction);
        }

        internal override void SetFirstSelect()
        {
            if(!Buttons.Any()) { return; }
            Buttons[0].Button.Select();
        }

        internal void Close()
        {
            MenuManager.Instance.TargetDialog.CancelTargetSelection();
        }

        public void SetNavigation()
        {
            for (int i = 0; i < Buttons.Count; i++)
            {
                var item = Buttons[i].Button;

                Navigation customNav = new Navigation();
                customNav.mode = Navigation.Mode.Explicit;
                customNav.selectOnDown = Buttons[(i + 1) % Buttons.Count].Button;
                customNav.selectOnUp = Buttons[(i - 1 + Buttons.Count) % Buttons.Count].Button;
                item.navigation = customNav;
            }
        }

		private static string SkillLabel(Skill skill, Character caster)
		{
			string rank = skill.Rank > 1 ? $"  Rank {skill.Rank}" : "";
			string arrows = !skill.UsesArrows ? "" : skill.ArrowCostMode == ArrowCostMode.PerTarget
                ? ", 1 arrow per target" : $", {skill.ArrowCost} arrows";
			return $"{skill.DisplayName}{rank}  {skill.CostText(caster)}{arrows}";
		}
	}
}
