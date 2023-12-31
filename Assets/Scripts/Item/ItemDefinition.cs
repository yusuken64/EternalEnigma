using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Game/Item/ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
	public string ItemName;
	public ItemEffectDefinition ItemEffectDefinition;

	public bool HasStacks;
	public int StackStartMin; //randomize value between stackcstartmin and stackstarmax on pickup
	public int StackStartMax;
	public int StackMax;
}
