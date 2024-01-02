using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
	/// <summary>
	/// Prompts user. use, throw, drop, cancel
	/// </summary>
	public class ActionDialog : Dialog
	{
		public TextMeshProUGUI ItemNameText;
		public List<Button> Buttons;
		public Transform Container;
		public Canvas Canvas;
		public GameObject Panel;

		private InventoryItem _data;
		private InventoryMenuItem _view;

		public void Use_Clicked()
		{
			Player playerCharacter = Game.Instance.PlayerCharacter;
			playerCharacter.SetAction(new UseInventoryItemAction(playerCharacter, _data));

			MenuManager.Instance.CloseMenu();
		}
		public void Throw_Clicked()
		{
			//MenuInputHandler.Instance.CloseAllDialog();
		}
		public void Drop_Clicked()
		{
			//playerCharacter.SetAction(new DropItemAction(playerCharacter, _data));
		}
		public void Cancel_Clicked()
		{
			MenuManager.Close(this);
		}

		public void Button_Clicked()
		{
			//MenuInputHandler.Instance.CloseAllDialog();
		}

		internal void Setup(InventoryMenuItem view, InventoryItem data)
		{
			this._data = data;
			this._view = view;
			ItemNameText.text = _data.ItemName;

			Vector3 newPos = Container.transform.position;

			//var rect = Panel.GetComponent<RectTransform>();
			//newPos.y = Mathf.Clamp(view.transform.position.y, -rect.rect.height / 2f, rect.rect.height / 2f);
			//rect.anchoredPosition = newPos;
			//Container.position = newPos;
			//Container.position = KeepFullyOnScreen(Panel, newPos);
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