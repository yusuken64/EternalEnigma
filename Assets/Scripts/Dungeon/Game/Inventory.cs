using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Inventory : MonoBehaviour
{
	public List<InventoryItem> InventoryItems = new();

    public int MaxItems = 10;

	public delegate void InventoryChangedEventHandler();
	public event InventoryChangedEventHandler HandleInventoryChanged;

	internal void Remove(InventoryItem item)
	{
		InventoryItems.Remove(item);
		HandleInventoryChanged?.Invoke();
	}

	internal void Add(InventoryItem inventoryItem)
	{
		InventoryItems.Add(inventoryItem);
	}

	internal int Count()
	{
		return InventoryItems.Count();
	}

	internal bool CanAdd()
	{
		return InventoryItems.Count() < MaxItems;
	}
}
