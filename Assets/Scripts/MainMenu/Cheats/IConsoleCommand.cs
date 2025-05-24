public interface IConsoleCommand
{
    string Name { get; }
    string HelpText { get; }
    void Execute(string[] args, CheatConsole console);
}
