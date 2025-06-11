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

		public TextMeshProUGUI UseItemText;

		private InventoryItem _data;
		private InventoryMenuItem _view;
        private Character _character;

		public void Use_Clicked()
		{
			if (_character is Ally ally)
			{
				ally.SetAction(new UseInventoryItemAction(Game.Instance.PlayerController.Inventory, ally, _data));
			}

			MenuManager.Instance.CloseMenu();
		}

		public void Throw_Clicked()
		{
			_character.SetAction(new ThrowItemAction(_character, _data, Game.Instance.ThrownItemProjectilePrefab));
			MenuManager.Instance.CloseMenu();
		}

		public void Drop_Clicked()
		{
			_character.SetAction(new DropItemAction(Game.Instance.PlayerController.Inventory, _data, _character.TilemapPosition));
			MenuManager.Instance.CloseMenu();
		}
		public void Cancel_Clicked()
		{
			MenuManager.Close(this);
		}

		internal void Setup(InventoryMenuItem view, InventoryItem data, Character character)
		{
			this._data = data;
			this._view = view;
			this._character = character;
			ItemNameText.text = _data.ItemName;

			if (data is EquipableInventoryItem equipableInventoryItem)
			{
				if (character.Equipment.IsEquipped(equipableInventoryItem))
				{
					UseItemText.text = "Unequip";
				}
				else
				{
					UseItemText.text = "Equip";
				}
			}
			else
			{
				UseItemText.text = "Use";
			}
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