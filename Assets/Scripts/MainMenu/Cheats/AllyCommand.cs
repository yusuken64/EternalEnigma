public class AllyCommand : IConsoleCommand
{
    public string Name => "ally";

    public string HelpText => "Usage: ally";

    public void Execute(string[] args, CheatConsole console)
    {
        var ally = UnityEngine.Object.Instantiate(Game.Instance.AllyPrefab);

        ally.InitialzeVitalsFromStats();
        ally.Vitals.Level = 1;
        ally.SyncDisplayedStats();
        //ally.InitialzeModel(overworldAlly);
        Game.Instance.Allies.Add(ally);
        ally.SetPosition(Game.Instance.PlayerController.TilemapPosition);
    }
}