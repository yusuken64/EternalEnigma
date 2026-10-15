namespace EternalEnigma.Core.Terminal;

public static class TerminalGlyphs
{
    public static char Enemy(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 'e';
        foreach (char c in name)
            if (c >= 'A' && c <= 'Z') return char.ToLowerInvariant(c);
            else if (c >= 'a' && c <= 'z') return c;
        return 'e';
    }
    public static char TownService(string? id, string? dialogId = null)
    {
        string service = (id ?? string.Empty).ToLowerInvariant();
        if (service.Contains("bakery")) return 'b';
        if (service.Contains("consumable")) return 'c';
        if (service.Contains("item") || service.Contains("shop")) return 'i';
        if (service.Contains("inn") || service.Contains("rest")) return 'I';
        if (service.Contains("trainer")) return 'R';
        if (service.Contains("home")) return 'h';
        if (service.Contains("statue")) return 'S';
        if (service.Contains("guild")) return 'G';
        if (service.Contains("entrance")) return 'D';
        if (service.Contains("exit")) return 'X';
        return string.IsNullOrWhiteSpace(dialogId) ? '*' : TownService(dialogId);
    }
}
