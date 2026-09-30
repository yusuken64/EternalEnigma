using EternalEnigma.Core.Progression;

namespace EternalEnigma.ConsoleExplorer;

/// <summary>Door glyphs and legend text for town services, shared by the town and interior renderers.</summary>
public static class TownServiceGlyphs
{
    // Uppercase initials avoid the map glyphs @ X D H T v a; two classes borrow a meaningful letter instead.
    private static readonly Dictionary<string, char> ClassGlyphs = new()
    {
        ["warrior"] = 'W', ["guardian"] = 'G', ["archer"] = 'R', ["elementalist"] = 'E', ["healer"] = 'L',
        ["bard"] = 'B', ["occultist"] = 'O', ["rogue"] = 'F', ["commander"] = 'M', ["scout"] = 'S',
    };

    /// <summary>Glyph for buildings that are not services (dungeon entrance, statue) in a town with services.</summary>
    public const char OtherBuilding = '*';

    public static char Glyph(TownService service) => service.Kind switch
    {
        TownServiceKind.Bakery => 'b',
        TownServiceKind.Consumables => 'c',
        TownServiceKind.Items => 'i',
        _ => ClassGlyphs.TryGetValue(service.ClassId ?? "", out var glyph) ? glyph : '?',
    };

    public static string Name(TownService service) => service.Kind == TownServiceKind.Trainer
        ? Capitalize(service.ClassId ?? "?") + " trainer"
        : Capitalize(service.Kind.ToString());

    /// <summary>Legend rows for a visit's services; empty for a town with none.</summary>
    public static string[] LegendLines(TownVisit? visit)
    {
        var services = visit?.SlotServices?.Where(s => s != null).Select(s => s!).ToArray() ?? Array.Empty<TownService>();
        if (services.Length == 0) return Array.Empty<string>();
        string Join(IEnumerable<TownService> items) => string.Join("  ", items.Select(s => $"{Glyph(s)} {Name(s)}"));
        return new[]
        {
            Join(services.Where(s => s.Kind != TownServiceKind.Trainer)) + $"  {OtherBuilding} entrance/statue",
            Join(services.Where(s => s.Kind == TownServiceKind.Trainer)),
        };
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
}
