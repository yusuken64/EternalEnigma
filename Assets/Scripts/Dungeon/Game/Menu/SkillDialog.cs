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
                    ActionName = SkillLabel(skill),
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
                            Game.Instance.DoFloatingText(reason, Color.yellow, character.transform.position);
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
                    ActionName = controllingTownAlly.GetRank(skill) > 1 ? $"{skill} R{controllingTownAlly.GetRank(skill)}" : $"{skill}",
                    ClickAction = () =>
                    {
                        var definition = Common.Instance.SkillManager.GetSkillByName(skill);
                        TownMenu.ShowMessage(definition != null
                            ? $"{definition.SkillName} ({definition.SPCost} SP), rank {Mathf.Max(1, controllingTownAlly.GetRank(skill))}\n{definition.Description}\nUse active skills in the dungeon."
                            : $"Unknown skill: {skill}");
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

		private static string SkillLabel(Skill skill)
		{
			if (!skill.UsesArrows)
				return skill.Rank > 1 ? $"{skill.SkillName} R{skill.Rank}({skill.SPCost})" : $"{skill.SkillName}({skill.SPCost})";
			string rankPrefix = skill.Rank > 1 ? $" R{skill.Rank}" : "";
			string arrows = skill.ArrowCostMode == ArrowCostMode.PerTarget ? "1/target" : skill.ArrowCost.ToString();
			return $"{skill.SkillName}{rankPrefix}({skill.SPCost} SP, {arrows} arrows)";
		}
	}
}
