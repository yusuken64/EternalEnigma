using System;
using System.Collections.Generic;
using System.Linq;
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
        private Action contextualUse;
        public Func<bool> ValidateActor;
        private bool CanAct => ValidateActor == null || ValidateActor();
        public void RefreshAvailability()
        {
            Buttons[0].interactable = CanAct && new UseInventoryItemAction(Game.Instance.PlayerController.Inventory, _character, _data).CanBegin(_character);
            Buttons[1].interactable = Buttons[2].interactable = CanAct && !_character.Equipment.IsEquipped(_data);
            SetNavigation();
        }

		public void Use_Clicked()
		{
            if (!CanAct) return;
            if (contextualUse != null) { contextualUse(); return; }
			if (_character is Ally ally)
			{
				MenuManager.Instance.UseInventoryItem(ally, _data);
			}
		}

		public void Throw_Clicked()
		{
            if (!CanAct || _character.Equipment.IsEquipped(_data)) return;
			var droppedItem = _data.ItemDefinition.ResolveDroppedPrefab(Game.Instance.CurrentDungeon.DroppedItemPrefabs);
			var droppedItemPrefab = droppedItem != null
				? droppedItem.gameObject
				: Game.Instance.ThrownItemProjectilePrefab;
			var action = new ThrowItemAction(Game.Instance.PlayerController.Inventory, _character, _data, droppedItemPrefab)
			{
				LookAt = false
			};

			MenuManager.Instance.CloseAllMenus();
            _character.SetAction(action);
		}

		public void Drop_Clicked()
		{
            if (!CanAct || _character.Equipment.IsEquipped(_data)) return;
			var action = new DropItemAction(Game.Instance.PlayerController.Inventory, _data, _character.TilemapPosition);

			MenuManager.Instance.CloseAllMenus();
            _character.SetAction(action);
		}
		public void Cancel_Clicked()
		{
			CloseDialog();
		}

		internal void Setup(InventoryMenuItem view, InventoryItem data, Character character)
		{
            contextualUse = null;
            ValidateActor = null;
            foreach (var button in Buttons) button.gameObject.SetActive(true);
            foreach (var button in Buttons) button.interactable = true;
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

        public void SetupActions(string title, string actionLabel, Action action)
        {
            _character = null;
            ValidateActor = null;
            foreach (var button in Buttons) button.interactable = true;
            contextualUse = action;
            ItemNameText.text = title;
            UseItemText.text = actionLabel;
            // Dungeon prefab order: Use, Throw, Drop, Cancel.
            for (int i = 0; i < Buttons.Count; i++)
                Buttons[i].gameObject.SetActive(i == 0 || i == Buttons.Count - 1);
            SetNavigation();
        }

		internal override void SetFirstSelect()
		{
			Buttons.FirstOrDefault(b => b.gameObject.activeSelf && b.interactable)?.Select();
		}

		public void SetNavigation()
		{
			if(Panel.transform.parent.GetComponent<SafeAreaPanel>()!=null)
            {
                UseGameplayDock(Panel.transform);
                Buttons[^1].gameObject.SetActive(false);
                foreach(var close in Panel.GetComponentsInChildren<DungeonDialogClose>(true))close.gameObject.SetActive(false);
            }
			var active = Buttons.Where(b => b.gameObject.activeSelf && b.interactable).ToList();
            for (int i = 0; i < active.Count; i++)
			{
				var item = active[i];

				Navigation customNav = new Navigation();
				customNav.mode = Navigation.Mode.Explicit;
				customNav.selectOnDown = active[(i + 1) % active.Count];
				customNav.selectOnUp = active[(i - 1 + active.Count) % active.Count];
				item.navigation = customNav;
			}
		}
	}
}
