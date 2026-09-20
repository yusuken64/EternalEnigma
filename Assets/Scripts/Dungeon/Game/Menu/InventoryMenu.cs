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

    public void Setup(List<InventoryItem> items, Character character,
        Action<InventoryItem> selectItem = null, string selectionPrompt = null, bool followPortrait = true)
    {
        SetupView(items, followPortrait ? character.VisualParent : null, character.Equipment.IsEquipped,
            (view, data) =>
            {
                if (selectItem != null) { selectItem(data); return; }
                ActionDialog.Setup(view, data, character);
                ActionDialog.SetNavigation();
                MenuManager.Open(ActionDialog);
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, view.transform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(), screenPoint,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main, out var localPoint);
                var panel = ActionDialog.Panel.GetComponent<RectTransform>();
                panel.localPosition = KeepFullyOnScreen(panel, localPoint);
                SelectionArrow.transform.parent = ActionDialog.Panel.transform;
                ActionDialog.CloseAction = () => SelectionArrow.transform.parent = transform;
            },
            data =>
            {
                if (selectItem != null)
                {
                    InventoryItemPreview.Setup(data);
                    StatText.text = selectionPrompt;
                }
                else UpdateItemPreview(data, character.BaseStats, character.Equipment);
            });
        if (selectItem != null) StatText.text = selectionPrompt;
    }

    public void SetupTown(List<InventoryItem> items, TownCharacter character)
    {
        SetupView(items, character.VisualParent, character.Equipment.IsEquipped,
            (view, item) => FindFirstObjectByType<TownMenu>().OpenItemActions(this, (TownAlly)character, item),
            item => UpdateItemPreview(item, character.BaseStats, character.Equipment));
    }

    // Rendering, focus, portrait and scrolling are shared; callers provide the allowed actions.
    public void SetupView(List<InventoryItem> items, GameObject portrait, Func<InventoryItem, bool> isEquipped,
        Action<InventoryMenuItem, InventoryItem> clicked, Action<InventoryItem> preview)
    {
        if (followingObject != portrait)
        {
            if (followingObject != null) FaceCamDisplay.Unfollow(followingObject);
            followingObject = portrait;
            if (portrait != null) FaceCamDisplay.SetFollow(portrait);
        }
        InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, items, (view, item) =>
        {
            view.Setup(item, isEquipped);
            view.onClick.RemoveAllListeners();
            view.onClick.AddListener(() => clicked(view, item));
            view.SelectCallBack = eventData =>
            {
                if (eventData is PointerEventData) StopAutoScroll();
                else ScrollToSelected(view.gameObject);
                preview(item);
            };
        });
        EmptyMessage.SetActive(items.Count == 0);
        if (items.Count == 0)
        {
            InventoryItemPreview.Setup(null);
            StatText.text = "";
        }
    }

    private void UpdateItemPreview(InventoryItem data, Stats stats, Equipment equipment)
    {
        InventoryItemPreview.Setup(data);
        var current = stats + equipment.GetEquipmentStatModification();
        if (data is EquipableInventoryItem item)
        {
            var simulated = stats + equipment.GetStatsIfEquipped(item);
            StatText.text = $"Strength: {current.Strength} >> {simulated.Strength}\nDefense: {current.Defense} >> {simulated.Defense}";
        }
        else StatText.text = $"Strength: {current.Strength}\nDefense: {current.Defense}";
    }

    internal void Close()
    {
        if (followingObject != null) FaceCamDisplay.Unfollow(followingObject);
        followingObject = null;
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
