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
        public Func<string> ActorRestriction;
        public void RefreshAvailability()
        {
            foreach (var button in Buttons) button.interactable = true;
            SetNavigation();
        }

        private bool ValidateAttempt(bool movingItem = false)
        {
            string restriction = ActorRestriction?.Invoke();
            if (restriction != null) { GameMessages.Reject(restriction); return false; }
            if (contextualUse != null) return true;
            if (!MenuManager.CanAttemptAction(_character, out restriction)) { GameMessages.Reject(restriction); return false; }
            if (_character == null || _character.Vitals.HP <= 0)
            { GameMessages.Reject("A downed hero cannot use items."); return false; }
            if (_data?.ItemDefinition == null || !Game.Instance.PlayerController.Inventory.InventoryItems.Contains(_data) && !_character.Equipment.IsEquipped(_data))
            { GameMessages.Reject("This item is no longer in your inventory."); return false; }
            if (movingItem && _character.Equipment.IsEquipped(_data))
            { GameMessages.Reject("Unequip this item before throwing or dropping it."); return false; }
            return true;
        }

		public void Use_Clicked()
		{
            if (!ValidateAttempt()) return;
            if (contextualUse != null) { contextualUse(); return; }
			if (_character is Ally ally)
			{
				MenuManager.Instance.UseInventoryItem(ally, _data);
			}
		}

		public void Throw_Clicked()
		{
            if (!ValidateAttempt(movingItem: true)) return;
			var droppedItem = _data.ItemDefinition.ResolveDroppedPrefab(Game.Instance.CurrentDungeon.DroppedItemPrefabs);
			var droppedItemPrefab = droppedItem != null
				? droppedItem.gameObject
				: Game.Instance.ThrownItemProjectilePrefab;
			var action = new ThrowItemAction(Game.Instance.PlayerController.Inventory, _character, _data, droppedItemPrefab)
			{
				LookAt = false
			};
			if (!action.ValidateCommand(_character, out var reason)) { GameMessages.Reject(reason); return; }

			MenuManager.Instance.CloseAllMenus();
            _character.SetAction(action);
		}

		public void Drop_Clicked()
		{
            if (!ValidateAttempt(movingItem: true)) return;
			var action = new DropItemAction(Game.Instance.PlayerController.Inventory, _data, _character.TilemapPosition);
			if (!action.ValidateCommand(_character, out var reason)) { GameMessages.Reject(reason); return; }

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
            ActorRestriction = null;
            foreach (var button in Buttons) button.gameObject.SetActive(true);
            foreach (var button in Buttons) button.interactable = true;
			this._data = data;
			this._view = view;
			this._character = character;
			ItemNameText.text = GameplayText.Format(_data.DisplayName);

			if (data is EquipableInventoryItem equipableInventoryItem)
			{
				if (character.Equipment.IsEquipped(equipableInventoryItem))
				{
					UseItemText.text = GameplayText.Format("Unequip");
				}
				else
				{
					UseItemText.text = GameplayText.Format("Equip");
				}
			}
			else
			{
				UseItemText.text = GameplayText.Format("Use");
			}
		}

        public void SetupActions(string title, string actionLabel, Action action)
        {
            _character = null;
            ActorRestriction = null;
            foreach (var button in Buttons) button.interactable = true;
            contextualUse = action;
            ItemNameText.text = GameplayText.Format(title);
            UseItemText.text = GameplayText.Format(actionLabel);
            // Dungeon prefab order: Use, Throw, Drop, Cancel.
            for (int i = 0; i < Buttons.Count; i++)
                Buttons[i].gameObject.SetActive(i == 0 || i == Buttons.Count - 1);
            SetNavigation();
        }

		internal override void SetFirstSelect()
		{
			(Buttons.FirstOrDefault(b => b.gameObject.activeSelf && b.interactable) ??
                Panel.GetComponentsInChildren<DungeonDialogClose>().Select(c=>c.GetComponent<Button>()).FirstOrDefault())?.Select();
		}

		public void SetNavigation()
		{

            Button closeButton=null;
			if(Panel.transform.parent.GetComponent<SafeAreaPanel>()!=null)
            {
                UseGameplayDock(Panel.transform);
                Buttons[^1].gameObject.SetActive(false);
                closeButton=DungeonDialogClose.ShowSingle(Panel.transform);
            }
			var active = Buttons.Where(b => b.gameObject.activeSelf && b.interactable).ToList();
            if(closeButton!=null)active.Add(closeButton);
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
