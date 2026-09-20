using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class ItemSaveData
{
    public string ItemName;
    public bool HasStock;
    public int Stock;

    public static ItemSaveData From(InventoryItem item) => new() {
        ItemName = item.ItemName, HasStock = item.StackStock.HasValue, Stock = item.StackStock ?? 0 };

    public InventoryItem Restore(ItemManager manager) => manager.GetAsInventoryItemByName(ItemName, HasStock ? Stock : null);
    public static List<ItemSaveData> Capture(IEnumerable<InventoryItem> items) =>
        items.Where(i => i?.ItemDefinition != null).Select(From).ToList();
}

[Serializable]
public class TownShopSaveData
{
    public string Key;
    public int RestockVersion = -1;
    public List<TownStockSaveData> Stock = new();
}

[Serializable]
public class TownStockSaveData
{
    public string ItemName;
    public int Remaining;
}
