using System;
using System.Linq;

public class ItemsCommand : IConsoleCommand
{
    public string Name => "items";
    public string HelpText => "Usage: items [filter]\nLists all item IDs containing the optional filter string.";

    public void Execute(string[] args, CheatConsole console)
    {
        var itemDefs = Common.Instance?.ItemManager?.ItemDefinitions;
        if (itemDefs == null)
        {
            console.Log("Error: Could not access item definitions.");
            return;
        }

        var allItems = itemDefs.AsEnumerable();

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            string filter = args[0].Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;
            allItems = allItems.Where(x => x.ItemName?.IndexOf(filter, comparison) >= 0);
        }

        var matchingItems = allItems.ToList();
        if (matchingItems.Count == 0)
        {
            console.Log("No items found.");
            return;
        }

        foreach (var item in matchingItems)
        {
            console.Log(item.ItemName);
        }
    }
}
