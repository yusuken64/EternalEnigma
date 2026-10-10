using System.Collections.Generic;
using System.Linq;

/// <summary>Run-local escrow retains the original objects, including upgrades and stack identity.</summary>
public sealed class PrisonEscrow
{
    readonly List<InventoryItem> bag = new();
    readonly Dictionary<Ally, List<EquipableInventoryItem>> equipment = new();
    public bool Confiscated { get; private set; }
    public bool Recovered { get; private set; }
    public readonly List<InventoryItem> Overflow = new();

    public void Confiscate(Game game)
    {
        if (Confiscated) return;
        Confiscated = true;
        bag.AddRange(game.PlayerController.Inventory.InventoryItems);
        game.PlayerController.Inventory.Clear();
        foreach (var ally in PartyRules.PartyMembers(game))
        {
            var items = ally.Equipment.GetEquippedItems().ToList();
            equipment.Add(ally, items);
            foreach (var item in items) ally.Equipment.UnEquip(item);
            ally.InvalidateCachedStats();
        }
    }

    public bool Recover(Game game)
    {
        if (!Confiscated || Recovered) return false;
        // Collect temporary items before replacing equipment. Commit once before notifying UI.
        var temporary = game.PlayerController.Inventory.InventoryItems.ToList();
        foreach (var entry in equipment)
            temporary.AddRange(entry.Key.Equipment.GetEquippedItems());
        Recovered = true;
        game.PlayerController.Inventory.Clear();
        foreach (var item in bag) game.PlayerController.Inventory.Add(item);
        foreach (var entry in equipment)
        {
            var owner = entry.Key;
            foreach (var item in owner.Equipment.GetEquippedItems().ToArray()) owner.Equipment.UnEquip(item);
            foreach (var item in entry.Value) owner.Equipment.Equip(item);
            owner.InvalidateCachedStats();
        }
        foreach (var item in temporary.Distinct())
            if (game.PlayerController.Inventory.CanAdd()) game.PlayerController.Inventory.Add(item);
            else Overflow.Add(item);
        GameMessages.Post("Recovered the party's possessions. Excess escape supplies remain in the stash.");
        return true;
    }

    internal void TakeOverflow(Game game)
    {
        while (Overflow.Count > 0 && game.PlayerController.Inventory.CanAdd())
        { game.PlayerController.Inventory.Add(Overflow[0]); Overflow.RemoveAt(0); }
    }
}
