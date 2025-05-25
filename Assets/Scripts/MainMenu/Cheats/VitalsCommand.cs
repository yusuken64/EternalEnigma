public class VitalsCommand : IConsoleCommand
{
    public string Name => "vitals";

    public string HelpText => "Usage: vitals [hp <value>] [sp <value>] [hunger <value>] [level <value>]";

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
            console.Log(HelpText);
            return;
        }

        for (int i = 0; i < args.Length - 1; i += 2)
        {
            string key = args[i].ToLowerInvariant();
            if (!int.TryParse(args[i + 1], out int value))
            {
                console.Log($"Invalid value for {key}: '{args[i + 1]}'");
                continue;
            }

            switch (key)
            {
                case "hp": player.Vitals.HP = value; break;
                case "sp": player.Vitals.SP = value; break;
                case "hunger": player.Vitals.Hunger = value; break;
                case "level": player.Vitals.Level = value; break;
                default: console.Log($"Unknown vitals key: {key}"); break;
            }
        }

        player.SyncDisplayedStats();
        console.Log("Vitals updated.");
    }
}
