using JuicyChickenGames.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TownMenu : MonoBehaviour
{
    public List<TownDialogBinding> BuildingDialogs = new();
    public AllyRecruitDialog AllyRecruitDialog;
    public InventoryMenu InventoryMenu;
    public SkillDialog SkillDialog;
    public ActionDialog ItemActionDialog;
    private readonly Dictionary<Dialog, Dialog> customDialogs = new();

    private void Start()
    {
        foreach (var binding in BuildingDialogs) binding.Dialog.gameObject.SetActive(false);
        AllyRecruitDialog.gameObject.SetActive(false);
        InventoryMenu.gameObject.SetActive(false);
        SkillDialog.gameObject.SetActive(false);
        if (ItemActionDialog == null)
        {
            // Reuse the dungeon's action-menu prefab with context-specific actions.
            ItemActionDialog = Instantiate(Resources.Load<ActionDialog>("TownItemActions"), transform);
        }
        ItemActionDialog.gameObject.SetActive(false);
    }

    public void ValidateBindings(TownConfiguration configuration)
    {
        if (BuildingDialogs.Any(b => b == null || b.Dialog == null) ||
            BuildingDialogs.Select(b => b.Id).Distinct().Count() != BuildingDialogs.Count)
            throw new InvalidOperationException("Town has missing or duplicate dialog bindings.");
        foreach (var building in configuration.Buildings)
            if (building.DialogPrefab == null && !BuildingDialogs.Any(b => b.Id == building.DialogId))
                throw new InvalidOperationException($"No town dialog binding for '{building.DialogId}'.");
    }

    public void OpenBuilding(TownBuildingDefinition building, TownPlayer player, TownAction reverse)
    {
        Dialog dialog;
        if (building.DialogPrefab != null)
        {
            if (!customDialogs.TryGetValue(building.DialogPrefab, out dialog))
            {
                dialog = Instantiate(building.DialogPrefab, transform);
                dialog.gameObject.SetActive(false);
                customDialogs.Add(building.DialogPrefab, dialog);
            }
        }
        else dialog = BuildingDialogs.First(b => b.Id == building.DialogId).Dialog;
        dialog.PrepareTown(new TownInteractionContext(FindFirstObjectByType<Town>(), building));
        dialog.CloseAction = () => player.SetAction(reverse);
        FindFirstObjectByType<TownMenuManager>().Open(dialog);
    }

    public static void ShowMessage(string text)
    {
        var dialog = Common.Instance.MessageDialog;
        dialog.PromptText.text = text;
        FindFirstObjectByType<TownMenuManager>().Open(dialog);
    }

    public void OpenItemActions(InventoryMenu inventory, TownAlly character, InventoryItem item)
    {
        if (item is not EquipableInventoryItem)
        {
            ShowMessage($"{item.ItemName}\n{item.ItemDefinition.Description}\nUse this item in the dungeon.");
            return;
        }
        var town = FindFirstObjectByType<Town>();
        ItemActionDialog.SetupActions(item.ItemName, character.Equipment.IsEquipped(item) ? "Unequip" : "Equip", () =>
        {
            town.Services.ToggleEquipment(character, item);
            ItemActionDialog.CloseDialog();
            inventory.SetupTown(character.Equipment.GetEquippedItems().Cast<InventoryItem>().Concat(town.TownPlayer.Inventory).ToList(), character);
            inventory.SetNavigation();
            inventory.SetFirstSelect();
        });
        FindFirstObjectByType<TownMenuManager>().Open(ItemActionDialog);
    }
}

[Serializable]
public class TownDialogBinding
{
    public string Id;
    public Dialog Dialog;
}
