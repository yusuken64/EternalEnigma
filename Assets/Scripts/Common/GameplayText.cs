using System;
using System.Text.RegularExpressions;

/// <summary>Presentation-only wording. Never use display text as an item, skill or save identifier.</summary>
public static class GameplayText
{
    private static readonly Regex Markup = new(@"(<[^>]+>)");
    private static readonly Regex DebugTag = new(@"\[(?:Cast|Preview|Debug|Info|Warning|Error)\]\s*", RegexOptions.IgnoreCase);
    private static readonly Regex Separators = new(@"[\[\]()_]");
    private static readonly Regex Spaces = new(@"[^\S\r\n]+");
    private static readonly Regex CamelWords = new(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

    public static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        value = value.Replace("(Clone)", "").Trim();
        if (value.StartsWith("Enemy_", StringComparison.Ordinal)) value = value.Substring(6);
        if (value.StartsWith("Demo Scroll: ", StringComparison.Ordinal)) value = value.Substring(13) + " Scroll";
        else if (value.StartsWith("Demo: ", StringComparison.Ordinal)) value = value.Substring(6);
        if (value.StartsWith("Command: ", StringComparison.Ordinal)) value = value.Substring(9) + " Order";
        if (value.EndsWith(" (Off-hand)", StringComparison.Ordinal)) value = "Off-hand " + value.Substring(0, value.Length - 11);
        value = value switch
        {
            "Bow02" => "Hunter's Bow", "Bow03" => "Longbow", "Bow04" => "Composite Bow", "Bow05" => "Royal Bow",
            "Potion 40HP" => "Lesser Healing Potion", "Potion 60HP" => "Healing Potion", "Potion 80HP" => "Greater Healing Potion", "Potion 100HP" => "Superior Healing Potion",
            "Demo Training Sword" => "Training Sword", "Sp Up" => "SP Up", "Hunger Up" => "Fullness Up",
            "Dot" => "Damage over Time", "Hot" => "Regeneration", "MainHand" => "Main hand", "OffHand" => "Off hand",
            "TwoHand" => "Two-handed weapon", "BowAndArrow" => "bows and arrows", "SwordAndShiled" => "swords and shields",
            "HPMax" => "Maximum HP", "SPMax" => "Maximum SP", "HungerMax" => "Maximum fullness", _ => value
        };
        return Format(CamelWords.Replace(value, " ")).Trim();
    }

    public static string Plain(string value) => Markup.Replace(Format(value), "");

    public static string Format(string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        value = InputPrompts.Resolve(value);
        var parts = Markup.Split(value);
        for (int i = 0; i < parts.Length; i += 2)
        {
            string plain = DebugTag.Replace(parts[i], "").Replace("(Clone)", "");
            plain = Regex.Replace(plain, @"\bHPMax\b", "maximum HP");
            plain = Regex.Replace(plain, @"\bSPMax\b", "maximum SP");
            plain = Regex.Replace(plain, @"\bHungerMax\b", "maximum fullness");
            plain = Regex.Replace(plain, @"\b(\d+)[gG]\b", "$1 gold");
            plain = Regex.Replace(plain, @"\((\d+) (turns?)\)", "for $1 $2");
            plain = Separators.Replace(plain, " ");
            plain = Regex.Replace(plain, @"(?<!\d):\s*", " ");
            plain = Spaces.Replace(plain, " ");
            plain = Regex.Replace(plain, @" +([,.!?])", "$1");
            plain = Regex.Replace(plain, @" *\r?\n *", "\n");
            parts[i] = plain;
        }
        return string.Concat(parts);
    }
}
