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

    public List<InventoryMenuItem> InventoryMenuItems { get; private set; }
    public FaceCamDisplay FaceCamDisplay;

    public void Setup(List<InventoryItem> Items, Character character)
    {
        //var player = Game.Instance.PlayerCharacter;
        FaceCamDisplay.SetFollow(character.VisualParent);
        Action<InventoryMenuItem, InventoryItem> action = (view, data) =>
        {
            view.Setup(data, character);
            Button button = view.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                ActionDialog.Setup(view, data, character);
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
                UpdatedItemPreview(data, character);
            };
        };
        InventoryMenuItems = MenuItemContainer.RePopulateObjects(InventoryMenuItemPrefab, Items, action);
    }

    private void UpdatedItemPreview(InventoryItem data, Character character)
    {
        InventoryItemPreview.Setup(data);

        if (data is EquipableInventoryItem equipmentItemDefinition)
        {
            var statModification = equipmentItemDefinition.GetEquipmentStatModification();
            int newStrength = character.BaseStats.Strength + statModification.Strength;
            int newDefense = character.BaseStats.Defense + statModification.Defense;

            StatText.text = $@"Strength: {character.DisplayedStats.Strength} >> {newStrength}
Defense: {character.DisplayedStats.Defense} >> {newDefense}";
        }
        else
        {
            StatText.text = $@"Strength: {character.DisplayedStats.Strength}
Defense: {character.DisplayedStats.Defense}";
        }
    }

    internal void Close()
    {
        FaceCamDisplay.Unfollow(FindObjectOfType<Player>().VisualParent);
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
