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
    public InventoryItemPreview InventoryItemPreview;
    public GameObject EmptyMessage;
    public List<InventoryMenuItem> InventoryMenuItems { get; private set; }
    public FaceCamDisplay FaceCamDisplay;

    private GameObject followingObject;

    public GameObject SelectionArrow;

    public void Setup(List<InventoryItem> Items, Character character)
    {
        followingObject = character.VisualParent;
        FaceCamDisplay.SetFollow(followingObject);
        Action<InventoryMenuItem, InventoryItem> action = (view, data) =>
        {
            view.Setup(data, (data) => { return character.Equipment.IsEquipped(data); });
            Button button = view.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                ActionDialog.Setup(view, data, character);
                ActionDialog.SetNavigation();
                ActionDialog.gameObject.SetActive(true);

                MenuManager.Open(ActionDialog);

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, view.transform.position);
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    screenPoint,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                    out localPoint
                );

                RectTransform panelRect = ActionDialog.Panel.GetComponent<RectTransform>();
                panelRect.localPosition = KeepFullyOnScreen(panelRect, localPoint);

                SelectionArrow.transform.parent = ActionDialog.Panel.transform;
                ActionDialog.CloseAction = () =>
                {
                    SelectionArrow.transform.parent = transform;
                };
            });

            view.SelectCallBack = () =>
            {
                ScrollToSelected(view.gameObject);
                UpdatedItemPreview(data, character);
            };
        };
        InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, Items, action);

        if (Items.Count() == 0)
		{
            EmptyMessage.gameObject.SetActive(true);
            InventoryItemPreview.Setup(null);
        }
		else
        {
            EmptyMessage.gameObject.SetActive(false);
        }
    }

    public void SetupOverworld(List<InventoryItem> Items, OverworldCharacter character)
    {
        followingObject = character.VisualParent;
        FaceCamDisplay.SetFollow(followingObject);
        Action<InventoryMenuItem, InventoryItem> action = (view, data) =>
        {
            view.Setup(data, (data) => { return character.Equipment.IsEquipped(data); });
            Button button = view.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                //ActionDialog.Setup(view, data, character);
                //ActionDialog.SetNavigation();
                //ActionDialog.gameObject.SetActive(true);

                //MenuManager.Open(ActionDialog);

                //var newPosition = view.transform.position;
                //newPosition = KeepFullyOnScreen(ActionDialog.Panel.GetComponent<RectTransform>(), newPosition);
                //ActionDialog.Panel.transform.position = newPosition;
            });

            view.SelectCallBack = () =>
            {
                ScrollToSelected(view.gameObject);
                UpdatedItemPreviewOverworld(data, character);
            };
        };
        InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, Items, action);

        if (Items.Count() == 0)
        {
            EmptyMessage.gameObject.SetActive(true);
            InventoryItemPreview.Setup(null);
        }
        else
        {
            EmptyMessage.gameObject.SetActive(false);
        }
    }

	private void UpdatedItemPreviewOverworld(InventoryItem data, OverworldCharacter character)
    {
        InventoryItemPreview.Setup(data);

        if (data is EquipableInventoryItem equipable)
        {
            var currentStats = character.BaseStats + character.Equipment.GetEquipmentStatModification();
            var simulatedStats = character.BaseStats + character.Equipment.GetStatsIfEquipped(equipable);

            StatText.text =
    $@"Strength: {currentStats.Strength} >> {simulatedStats.Strength}
Defense:  {currentStats.Defense}  >> {simulatedStats.Defense}";
        }
        else
        {
            var currentStats = character.BaseStats + character.Equipment.GetEquipmentStatModification();
            StatText.text =
    $@"Strength: {currentStats.Strength}
Defense:  {currentStats.Defense}";
        }
    }

	private void UpdatedItemPreview(InventoryItem data, Character character)
    {
        InventoryItemPreview.Setup(data);

        if (data is EquipableInventoryItem equipable)
        {
            var currentStats = character.BaseStats + character.Equipment.GetEquipmentStatModification();
            var simulatedStats = character.BaseStats + character.Equipment.GetStatsIfEquipped(equipable);

            StatText.text =
    $@"Strength: {currentStats.Strength} >> {simulatedStats.Strength}
Defense:  {currentStats.Defense}  >> {simulatedStats.Defense}";
        }
        else
        {
            var currentStats = character.BaseStats + character.Equipment.GetEquipmentStatModification();
            StatText.text =
    $@"Strength: {currentStats.Strength}
Defense:  {currentStats.Defense}";
        }
    }

    internal void Close()
    {
        FaceCamDisplay.Unfollow(followingObject);
    }

    private Vector3 KeepFullyOnScreen(RectTransform rectTransform, Vector3 newPosition)
    {
        var canvasRect = canvas.GetComponent<RectTransform>();

        // Panel size in local canvas units
        float halfWidth = rectTransform.rect.width / 2f;
        float halfHeight = rectTransform.rect.height / 2f;

        // Canvas size
        float canvasHalfWidth = canvasRect.rect.width / 2f;
        float canvasHalfHeight = canvasRect.rect.height / 2f;

        // Clamp relative to canvas center (local position)
        float x = Mathf.Clamp(newPosition.x, -canvasHalfWidth + halfWidth, canvasHalfWidth - halfWidth);
        float y = Mathf.Clamp(newPosition.y, -canvasHalfHeight + halfHeight, canvasHalfHeight - halfHeight);

        return new Vector3(x, y, newPosition.z);
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
}
