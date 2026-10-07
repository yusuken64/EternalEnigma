using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

    public DroppedItem DroppedItemPrefab;
    public Sprite Icon;
    public DroppedItem ResolveDroppedPrefab(IEnumerable<DroppedItem> categories) =>
        DroppedItemPrefab != null ? DroppedItemPrefab : categories?.FirstOrDefault(p=>p!=null&&p.DroppedItemVisual==DroppedItemVisual);
    public Sprite ResolveIcon(GamePresentationProfile profile=null)
    {
        if(Icon!=null)return Icon;
        profile=profile!=null?profile:GamePresentationProfile.Current;
        int index=(int)DroppedItemVisual;
        return profile!=null&&profile.ItemIcons!=null&&index>=0&&index<profile.ItemIcons.Length?profile.ItemIcons[index]:null;
    }

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
