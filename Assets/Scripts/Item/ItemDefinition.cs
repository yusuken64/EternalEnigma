using System;
using UnityEngine;

public abstract class ItemDefinition : ScriptableObject
{
	public string ItemName;
	public ItemEffectDefinition ItemEffectDefinition;

	public int StackStartMin = 1; //randomize value between stackcstartmin and stackstarmax on pickup
	public int StackStartMax = 1;
	public int StackMax;

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
