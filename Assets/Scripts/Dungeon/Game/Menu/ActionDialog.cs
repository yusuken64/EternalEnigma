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

		public void Use_Clicked()
		{
			Player playerCharacter = Game.Instance.PlayerCharacter;
			playerCharacter.SetAction(new UseInventoryItemAction(playerCharacter, _data));

			MenuManager.Instance.CloseMenu();
		}
		public void Throw_Clicked()
		{
			Player playerCharacter = Game.Instance.PlayerCharacter;
			playerCharacter.SetAction(new ThrowItemAction(playerCharacter, _data, playerCharacter.ThrownItemProjectilePrefab));

			MenuManager.Instance.CloseMenu();
		}
		public void Drop_Clicked()
		{
			Player playerCharacter = Game.Instance.PlayerCharacter;
			playerCharacter.SetAction(new DropItemAction(playerCharacter, _data, playerCharacter.TilemapPosition));

			MenuManager.Instance.CloseMenu();
		}
		public void Cancel_Clicked()
		{
			MenuManager.Close(this);
		}

		internal void Setup(InventoryMenuItem view, InventoryItem data, Player player)
		{
			this._data = data;
			this._view = view;
			ItemNameText.text = _data.ItemName;

			if (data is EquipableInventoryItem equipableInventoryItem)
			{
				if (player.Inventory.IsEquipped(equipableInventoryItem))
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