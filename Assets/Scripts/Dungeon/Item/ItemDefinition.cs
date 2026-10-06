using System;
using UnityEngine;

public abstract class ItemDefinition : ScriptableObject
{
	public string ItemName;
	public string Description;
	public bool ShopOnly;
	public bool IsProgressionItem;
	public int MinFloor = 1;
	public int MaxFloor = int.MaxValue;
	public ItemEffectDefinition ItemEffectDefinition;

	public bool ApplyToThrownTarget;
	public bool IsFood;
	public bool ProtectedFromTraps;
	public ItemDefinition SpoiledFood;
	public ItemDefinition CharredFood;

	public int StackStartMin = 1; //randomize value between stackcstartmin and stackstarmax on pickup
	public int StackStartMax = 1;
	public int StackMax;

	public DroppedItemVisual DroppedItemVisual;

	abstract internal InventoryItem AsInventoryItem(int? stock);

	internal int? InitializeStack(int? stock)
	{
		if (stock == null &&
			StackMax > 0)
		{
			stock = UnityEngine.Random.Range(StackStartMin, StackMax);
		}

		return stock;
	}
}
