using JuicyChickenGames.Menu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryMenu : Dialog
{
	public Transform MenuItemContainer;
	public InventoryMenuItem InventoryMenuItemPrefab;
	public ActionDialog ActionDialog;
	public Canvas canvas;

	public TextMeshProUGUI StatText;

	public List<InventoryMenuItem> InventoryMenuItems { get; private set; }

	public void Setup(List<InventoryItem> Items)
	{
		Action<InventoryMenuItem, InventoryItem> action = (view, data) =>
		{
			view.Setup(data);
			Button button = view.GetComponent<Button>();
			button.onClick.AddListener(() =>
			{
				ActionDialog.Setup(view, data);
				ActionDialog.SetNavigation();
				ActionDialog.gameObject.SetActive(true);

				MenuManager.Open(ActionDialog);

				var newPosition = view.transform.position;
				newPosition = KeepFullyOnScreen(ActionDialog.Panel.GetComponent<RectTransform>(), newPosition);
				ActionDialog.Panel.transform.position = newPosition;
			});

			view.SelectCallBack = () =>
			{
				ScrollToSelected(view.gameObject);
			};
		};
		InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, Items, action);

		var player = Game.Instance.PlayerCharacter;
		StatText.text = $@"Strength: {player.DisplayedStats.Strength}
Defense: {player.DisplayedStats.Defense}";
	}

	private Vector3 KeepFullyOnScreen(RectTransform rectTransform, Vector3 newPosition)
	{
		var canvasRectTransform = canvas.GetComponent<RectTransform>();
		float halfWidth = rectTransform.sizeDelta.x / 2f;
		float halfHeight = rectTransform.sizeDelta.y / 2f;
		var x = Mathf.Clamp(newPosition.x, halfWidth, canvasRectTransform.sizeDelta.x - halfWidth);
		var y = Mathf.Clamp(newPosition.y, halfHeight, canvasRectTransform.sizeDelta.y - halfHeight);

		return new Vector3(x, y, 0);
	}

	public void HandleInventoryItemSelected(BaseEventData eventData)
	{
		Debug.Log(this.gameObject.name + " was selected");
	}

	public void SetNavigation()
	{
		for (int i = 0; i < InventoryMenuItems.Count; i++)
		{
			InventoryMenuItem item = InventoryMenuItems[i];

			Navigation customNav = new Navigation();
			customNav.mode = Navigation.Mode.Explicit;
			customNav.selectOnDown = InventoryMenuItems[(i + 1) % InventoryMenuItems.Count];
			customNav.selectOnUp = InventoryMenuItems[(i - 1 + InventoryMenuItems.Count) % InventoryMenuItems.Count];
			item.navigation = customNav;
		}
	}

	internal override void SetFirstSelect()
	{
		if (InventoryMenuItems.Count > 0)
		{
			InventoryMenuItems[0].Select();
		}
	}

	public void ScrollToSelected(GameObject selectedItem)
	{
		//Debug.Log($"Scoll Item into view {selectedItem.GetComponent<InventoryMenuItem>().ItemText.text}", selectedItem);

		RectTransform contentRectTransform = scrollView.content.GetComponent<RectTransform>();
		RectTransform selectedRectTransform = selectedItem.GetComponent<RectTransform>();

		Canvas.ForceUpdateCanvases();

		contentRectTransform.anchoredPosition =
				(Vector2)scrollView.transform.InverseTransformPoint(contentRectTransform.position)
				- (Vector2)scrollView.transform.InverseTransformPoint(selectedRectTransform.position);
	}
}
