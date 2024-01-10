using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
	public class DynamicActionDialog : Dialog
	{
		public Transform ButtonContainer;
		public DynamicActionButton ActionButtonPrefab;

		internal Func<DynamicActionInfo, bool> selector;
		private List<DynamicActionButton> actionButtons;

		public void Setup(List<DynamicActionInfo> dynamicActionInfos)
		{
			Action<DynamicActionButton, DynamicActionInfo> setupAction = (view, data) =>
			{
				view.Setup(data);
			};
			actionButtons = ButtonContainer.RePopulateObjects(ActionButtonPrefab, dynamicActionInfos, setupAction);
		}

		internal override void SetFirstSelect()
		{
			if (actionButtons.Any())
			{
				var first = actionButtons.FirstOrDefault(x => selector(x._data));
				if (first != null)
				{
					first.Button.Select();

				}
				else
				{
					actionButtons[0].Button.Select();
				}
			}
		}

		public void SetNavigation()
		{
			for (int i = 0; i < actionButtons.Count; i++)
			{
				var item = actionButtons[i];

				Navigation customNav = new Navigation();
				customNav.mode = Navigation.Mode.Explicit;
				customNav.selectOnDown = actionButtons[(i + 1) % actionButtons.Count].Button;
				customNav.selectOnUp = actionButtons[(i - 1 + actionButtons.Count) % actionButtons.Count].Button;
				item.Button.navigation = customNav;
			}
		}
	}

	public class DynamicActionInfo
	{
		public string ActionName;
		public Action ClickAction;

		public object Data;
	}
}