using System;
using System.Linq;

public class ItemCommand : IConsoleCommand
{
    public string Name => "item";
    public string HelpText => "Usage: item <itemid>\nGives the player one instance of the specified item.";

    public void Execute(string[] args, CheatConsole console)
    {
        var player = console.FindPlayer();
        if (player == null)
        {
            console.Log("Error: No player found.");
            return;
        }

        if (args.Length < 1)
        {
            console.Log("Usage: item <itemid>");
            return;
        }

        string itemName = args[0];

        var itemDef = Common.Instance?.ItemManager?.ItemDefinitions
            .FirstOrDefault(x => x.ItemName.Equals(itemName, StringComparison.InvariantCultureIgnoreCase));

        if (itemDef == null)
        {
            console.Log($"Error: Item '{itemName}' does not exist.");
            return;
        }

        var newItem = Common.Instance.ItemManager.GetAsInventoryItem(itemDef, 1);
        player.Inventory.Add(newItem);

        console.Log($"Added '{itemName}' to inventory.");
    }
}
