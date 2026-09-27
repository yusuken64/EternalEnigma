using UnityEngine;

/// <summary>The original item animated by a transformation trap; ownership transfers exactly once.</summary>
public sealed class TrapCarriedItem : MonoBehaviour
{
    public InventoryItem Item;
    internal bool TryDrop(bool onDeath = false)
    {
        if (Item == null) return false;
        var dungeon = Game.Instance.CurrentDungeon;
        var cell = GetComponent<Character>().TilemapPosition;
        if (!DungeonPlacement.TryDrop(dungeon, cell, Item, out _))
        {
            if (!onDeath) return false;
            // No free floor cell: retain the recoverable item at the death cell rather than destroy it.
            dungeon.SetDroppedItem(cell, Item.ItemDefinition, Item.StackStock).InventoryItem = Item;
        }
        Item = null; return true;
    }
}
