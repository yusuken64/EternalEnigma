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
    public static char TownService(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return '*';
        string service = id.ToLowerInvariant();
        if (service.Contains("bakery")) return 'b';
        if (service.Contains("consumable")) return 'c';
        if (service.Contains("item") || service.Contains("shop")) return 'i';
        if (service.Contains("inn") || service.Contains("rest")) return 'n';
        if (service.Contains("trainer")) return 'R';
        return '*';
    }
}
