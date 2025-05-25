using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
    public class AllyActionDialog : Dialog
	{
		public TextMeshProUGUI ItemNameText;
		public List<Button> Buttons;
		public Transform Container;
		public Canvas Canvas;
		public GameObject Panel;

		public DynamicActionDialog DynamicActionDialog;
		private Ally _ally;

		public FaceCamDisplay FaceCamDisplay;

		public void Strategy_Clicked()
		{
			MenuManager.Open(DynamicActionDialog);

			List<DynamicActionInfo> dynamicActionInfos = new List<DynamicActionInfo>()
			{
				new DynamicActionInfo()
				{
					ActionName = "Follow",
					Data = AllyStrategy.Follow,
					ClickAction = () =>
					{
						Game.Instance.DoFloatingText("Follow", Color.white, _ally.VisualParent.transform.position);
						_ally.AllyStrategy = AllyStrategy.Follow;
						FaceCamDisplay.Unfollow(_ally.VisualParent);
						MenuManager.Close(DynamicActionDialog);
						MenuManager.Close(this);
					}
				},
				//new DynamicActionInfo()
				//{
				//	ActionName = "Passive",
				//	ClickAction = () =>
				//	{
				//		_ally.AllyStrategy = AllyStrategy.Passive;
				//		MenuManager.Close(DynamicActionDialog);
				//		MenuManager.Close(this);
				//	}
				//},
				new DynamicActionInfo()
				{
					ActionName = "Aggressive",
					Data = AllyStrategy.Aggresive,
					ClickAction = () =>
					{
						Game.Instance.DoFloatingText("Aggresive", Color.white, _ally.VisualParent.transform.position);
						_ally.AllyStrategy = AllyStrategy.Aggresive;
						FaceCamDisplay.Unfollow(_ally.VisualParent);
						MenuManager.Close(DynamicActionDialog);
						MenuManager.Close(this);
					}
				},
				new DynamicActionInfo()
				{
					ActionName = "Hold Position",
					Data = AllyStrategy.HoldPosition,
					ClickAction = () =>
					{
						Game.Instance.DoFloatingText("Hold Position", Color.white, _ally.VisualParent.transform.position);
						_ally.AllyStrategy = AllyStrategy.HoldPosition;
						FaceCamDisplay.Unfollow(_ally.VisualParent);
						MenuManager.Close(DynamicActionDialog);
						MenuManager.Close(this);
					}
				},
			};
			DynamicActionDialog.Setup(dynamicActionInfos);
			DynamicActionDialog.SetNavigation();
			DynamicActionDialog.selector = (x) => (AllyStrategy)x.Data == _ally.AllyStrategy;
			MenuManager.Instance.LateAction = () =>
			{
				DynamicActionDialog.SetFirstSelect();
			};
		}

		public void Inventory_Clicked()
		{
			MenuManager.Close(this);
			MenuManager.Instance.OpenInventoryAs(_ally);
		}

		public void Skill_Clicked()
		{
			MenuManager.Open(DynamicActionDialog);

			List<DynamicActionInfo> dynamicActionInfos = 
				_ally.Skills.Select((skill, index) => 
				{ 
					return new DynamicActionInfo()
					{
						ActionName = $"{skill.SkillName}({skill.SPCost})",
						ClickAction = () =>
						{
							//_ally.SetAction(new SkillAction(_ally, skill));
						}
					};
				}).ToList();
			DynamicActionDialog.Setup(dynamicActionInfos);
			DynamicActionDialog.SetNavigation();
			MenuManager.Instance.LateAction = () =>
			{
				DynamicActionDialog.SetFirstSelect();
			};
		}

		public void Talk_Clicked()
		{
			Game.Instance.DoFloatingText($"{_ally.Vitals.HP}HP, {_ally.Vitals.SP}SP", Color.white, _ally.VisualParent.transform.position);
			FaceCamDisplay.Unfollow(_ally.VisualParent);
			MenuManager.Close(this);
		}

		public void Cancel_Clicked()
		{
			FaceCamDisplay.Unfollow(_ally.VisualParent);
			MenuManager.Close(this);
		}

		internal void Setup(Ally ally)
		{
			this._ally = ally;
			FaceCamDisplay.SetFollow(_ally.VisualParent);
		}

		internal void Close()
		{
			FaceCamDisplay.Unfollow(_ally.VisualParent);
		}

		internal override void SetFirstSelect()
		{
			Buttons[0].Select();
		}

		public void SetNavigation()
		{
			for (int i = 0; i < Buttons.Count; i++)
			{
				var item = Buttons[i];

				Navigation customNav = new Navigation();
				customNav.mode = Navigation.Mode.Explicit;
				customNav.selectOnDown = Buttons[(i + 1) % Buttons.Count];
				customNav.selectOnUp = Buttons[(i - 1 + Buttons.Count) % Buttons.Count];
				item.navigation = customNav;
			}
		}
	}
}