using UnityEngine;

[CreateAssetMenu(fileName = "UsableItemDefinition", menuName = "Game/Item/UsableItemDefinition")]
public class UsableItemDefinition : ItemDefinition
{
	internal override InventoryItem AsInventoryItem(int? stock)
	{
		return new UsableInventoryItem(this, stock);
	}
}
