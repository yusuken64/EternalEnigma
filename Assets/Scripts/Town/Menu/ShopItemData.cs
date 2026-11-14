public class ShopItemData
{
	public string ItemName;
	public int Cost;
    public int Remaining;
    public InventoryItem BagItem;
    public ItemDefinition Definition;
    public bool IsSale;

	public ShopItemData(string itemName, int cost)
	{
		this.ItemName = itemName;
		this.Cost = cost;
	}
}
