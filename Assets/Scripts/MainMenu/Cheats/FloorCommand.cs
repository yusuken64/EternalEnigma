using UnityEngine.SceneManagement;

public class FloorCommand : IConsoleCommand
{
    public string Name => "floor";

    public string HelpText => "Usage: floor <floorIndex>\nLoads the dungeon scene starting at the specified floor (default is 0).";

    public void Execute(string[] args, CheatConsole console)
    {
        int startingFloor = 0;

        if (args.Length >= 1 && !int.TryParse(args[0], out startingFloor))
        {
            console.Log($"Invalid floor argument '{args[0]}', defaulting to floor 0.");
            startingFloor = 0;
        }

        Common.Instance.GameSaveData.StartingFloor = startingFloor;
        console.Log($"Starting dungeon at floor {startingFloor}...");
        SceneManager.LoadScene("DungeonScene");
    }
}
