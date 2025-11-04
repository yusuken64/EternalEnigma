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
            var dynamicActionInfos = character.Skills.Select((skill, index) =>
            {
                return new DynamicActionInfo()
                {
                    ActionName = $"{skill.SkillName}({skill.SPCost})",
                    ClickAction = () =>
                    {
                        if (character.CanCast(skill, out string reason))
                        {
                            //MenuManager.Open(MenuManager.Instance.TargetDialog);
                            MenuManager.Instance.OpenTargetingMenu(character, skill);
                        }
						else
						{
                            //TODO warn based on reason
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


        internal void SetupOverworld(OverworldAlly controllingOverworldAlly)
        {
            var dynamicActionInfos = controllingOverworldAlly.Skills.Select((skill, index) =>
            {
                return new DynamicActionInfo()
                {
                    ActionName = $"{skill}",
                    ClickAction = () =>
                    {
                        //do nothing on overworld
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
	}
}