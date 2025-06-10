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

        public GameObject SelectTargetPrompt;
        private EventSystem _eventSystem;

        public void Setup(Character character)
        {
            SelectTargetPrompt.SetActive(false);
            MenuManager.Open(this);
            var dynamicActionInfos = character.Skills.Select((skill, index) =>
            {
                return new DynamicActionInfo()
                {
                    ActionName = $"{skill.SkillName}({skill.SPCost})",
                    ClickAction = () =>
                    {
                        //Select Skill Target;
                        var player = FindFirstObjectByType<PlayerController>();
                        SelectTargetPrompt.SetActive(true);
                        var possibleTargets = Game.Instance.AllCharacters; //TODO filter by skill
                        player.InvokeTargetSelection(skill, possibleTargets, TargetSelected);
                        _eventSystem = EventSystem.current;
                        _eventSystem.enabled = false;
                    }
                };
            }).ToList();

            Action<DynamicActionButton, DynamicActionInfo> setupAction = (view, data) =>
            {
                view.Setup(data);
            };
            Buttons = ButtonContainer.RePopulateObjects(ActionButtonPrefab, dynamicActionInfos, setupAction);
        }

        private void TargetSelected(Ally caster, Skill skill, Vector3Int target)
        {
            caster.SetAction(new SkillAction(caster, skill, target));
            _eventSystem.enabled = true;
            SelectTargetPrompt.SetActive(false);
            CloseAction?.Invoke();
        }

        internal override void SetFirstSelect()
        {
            if(!Buttons.Any()) { return; }
            Buttons[0].Button.Select();
        }

        internal void Close()
        {
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