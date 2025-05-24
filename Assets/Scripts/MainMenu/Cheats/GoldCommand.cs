public class GoldCommand : IConsoleCommand
{
    public string Name => "gold";
    public string HelpText => "Usage: gold <amount>";

    public void Execute(string[] args, CheatConsole console)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int amount))
        {
            console.Log(HelpText);
            return;
        }

        var player = console.FindOverworldPlayer();
        if (player != null)
        {
            player.Gold += amount;
            console.Log($"Added {amount} gold to player.");
        }
        else
        {
            console.Log("No player found.");
        }
    }
}
