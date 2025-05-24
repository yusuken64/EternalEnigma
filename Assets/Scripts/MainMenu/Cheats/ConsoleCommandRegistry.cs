using System;
using System.Collections.Generic;

public class ConsoleCommandRegistry
{
    private Dictionary<string, IConsoleCommand> commands = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IConsoleCommand command)
    {
        commands[command.Name] = command;
    }

    public bool TryGetCommand(string name, out IConsoleCommand command)
    {
        return commands.TryGetValue(name, out command);
    }

    public IEnumerable<IConsoleCommand> AllCommands => commands.Values;
}
