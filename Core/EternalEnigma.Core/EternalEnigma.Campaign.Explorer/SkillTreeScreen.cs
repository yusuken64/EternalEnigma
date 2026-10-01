using EternalEnigma.Core.Classes;

namespace EternalEnigma.ConsoleExplorer;

/// <summary>Draws a <see cref="SkillTreeBook"/> and runs the interactive skill tree view.</summary>
public static class SkillTreeScreen
{
    public static readonly string[] Keys =
    {
        "Up/Down: select | Left/Right: other tree | [ ]: move tier | +/-: max rank | K: kind",
        "A: add | N: rename | Del: remove | Z: undo | S: save | E: export | I: import | Esc: back",
    };

    /// <summary>The tree as text lines: tiers, skills with their rank span, learn levels and point totals.</summary>
    public static List<string> Render(SkillTreeBook book, int height = int.MaxValue)
    {
        var file = book.Current;
        if (file == null)
            return new List<string> { "No skill trees loaded.", "Press I to import a .skilltree file." };

        var doc = file.Document;
        var lines = new List<string>
        {
            $"SKILL TREE {book.Index + 1}/{book.Files.Count} | {doc.ClassId}" +
            (doc.DisplayName.Length > 0 ? $" ({doc.DisplayName})" : "") +
            $" | {(file.Path == null ? "(unsaved)" : System.IO.Path.GetFileName(file.Path))}{(file.Dirty ? " *modified*" : "")}",
        };

        var ordered = book.Ordered();
        var selected = book.SelectedEntry;
        int selectedLine = 0;
        for (int tier = 1; tier <= SkillTreeDocument.TierCount; tier++)
        {
            lines.Add("");
            lines.Add($"Tier {tier}  (unlocks at level {SkillLearningRules.TierUnlockLevel(tier)})");
            var inTier = ordered.Where(e => e.Tier == tier).ToList();
            if (inTier.Count == 0)
                lines.Add("    (empty)");

            foreach (var entry in inTier)
            {
                bool isSelected = selected != null && entry.SkillId == selected.SkillId;
                if (isSelected)
                    selectedLine = lines.Count;

                lines.Add((isSelected ? "  > " : "    ") + Describe(entry));
            }
        }

        var errors = doc.Validate();
        lines.Add("");
        lines.Add(errors.Count == 0 ? "Valid: one mastery per tier, no duplicates." : "Problems:");
        lines.AddRange(errors.Select(e => "  ! " + e));

        // Keep the selection visible when the terminal is short; the header stays pinned.
        int room = Math.Max(1, height);
        if (lines.Count > room)
        {
            int body = Math.Max(1, room - 1);
            int first = Math.Clamp(selectedLine - body / 2, 1, Math.Max(1, lines.Count - body));
            lines = new List<string> { lines[0] }.Concat(lines.Skip(first).Take(body)).ToList();
        }

        return lines;
    }

    public static string Describe(ClassSkillEntry entry)
    {
        string kind = SkillTreeFormat.KindKeyword(entry.Kind).PadRight(9);
        string name = entry.SkillId.Length > 28 ? entry.SkillId.Substring(0, 27) + "~" : entry.SkillId.PadRight(28);
        int firstLevel = SkillLearningRules.RequiredLevel(entry.Tier, 1);
        if (entry.Kind == SkillKind.Normal)
        {
            int lastLevel = SkillLearningRules.RequiredLevel(entry.Tier, entry.MaxRank);
            int points = Enumerable.Range(1, entry.MaxRank).Sum(SkillLearningRules.PointCost);
            return $"{kind} {name} ranks 1-{entry.MaxRank}  lv {firstLevel}-{lastLevel}  {points} pt";
        }

        bool free = entry.Kind == SkillKind.Mastery && entry.Tier == 1;
        return $"{kind} {name} 1 rank       lv {firstLevel}     {(free ? "free" : "1 pt")}";
    }

    /// <summary>Handles one key. Returns false when the user leaves the view.</summary>
    public static bool Handle(SkillTreeBook book, ConsoleKeyInfo key, Func<string, string?> prompt)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape: return !book.TryLeave();
            case ConsoleKey.UpArrow: book.MoveSelection(-1); break;
            case ConsoleKey.DownArrow: book.MoveSelection(1); break;
            case ConsoleKey.LeftArrow: book.SwitchTree(-1); break;
            case ConsoleKey.RightArrow: book.SwitchTree(1); break;
            case ConsoleKey.Oem4: book.ShiftTier(-1); break;   // [
            case ConsoleKey.Oem6: book.ShiftTier(1); break;    // ]
            case ConsoleKey.Add: case ConsoleKey.OemPlus: book.AdjustRank(1); break;
            case ConsoleKey.Subtract: case ConsoleKey.OemMinus: book.AdjustRank(-1); break;
            case ConsoleKey.K: book.CycleKind(); break;
            case ConsoleKey.Delete: case ConsoleKey.X: book.Remove(); break;
            case ConsoleKey.Z: book.Undo(); break;
            case ConsoleKey.S: Guard(book, book.Save); break;
            case ConsoleKey.A:
                if (prompt("Skill name to add: ") is { Length: > 0 } added) book.Add(added);
                break;
            case ConsoleKey.N:
                if (book.SelectedEntry != null && prompt($"Rename {book.SelectedEntry.SkillId} to: ") is { Length: > 0 } renamed)
                    book.Rename(renamed);
                break;
            case ConsoleKey.E:
                if (prompt("Export to file: ") is { Length: > 0 } exportPath) Guard(book, () => book.Export(exportPath));
                break;
            case ConsoleKey.I:
                if (prompt("Import from file: ") is { Length: > 0 } importPath) book.Import(importPath);
                break;
        }

        return true;
    }

    /// <summary>Interactive loop; needs a real terminal.</summary>
    public static void Run(SkillTreeBook book)
    {
        bool cursorVisible = !OperatingSystem.IsWindows() || Console.CursorVisible;
        try
        {
            Console.CursorVisible = false;
            Console.Clear();
            while (true)
            {
                int width = Math.Max(1, Console.WindowWidth - 1), height = Math.Max(1, Console.WindowHeight - 1);
                int reserved = Keys.Length + 2;
                var lines = Render(book, Math.Max(1, height - reserved));
                while (lines.Count < height - reserved) lines.Add("");
                lines.Add(book.Message);
                lines.Add("");
                lines.AddRange(Keys);
                Console.SetCursorPosition(0, 0);
                for (int row = 0; row < height; row++)
                {
                    string line = row < lines.Count ? lines[row] : "";
                    Console.Write(line.Length > width ? line[..width] : line.PadRight(width));
                    if (row + 1 < height) Console.WriteLine();
                }

                book.Message = "";
                if (!Handle(book, Console.ReadKey(true), text => Prompt(text, width, height)))
                    return;
            }
        }
        finally { Console.CursorVisible = cursorVisible; Console.Clear(); }
    }

    // Static preview used for --skilltree when there is no terminal.
    public static IEnumerable<string> Snapshot(SkillTreeBook book)
    {
        for (int i = 0; i < book.Files.Count; i++)
        {
            book.SwitchTree(i == 0 ? 0 : 1);
            if (i > 0) yield return "";
            foreach (string line in Render(book)) yield return line;
        }

        if (book.Files.Count == 0) yield return book.Message.Length > 0 ? book.Message : "No skill trees found.";
    }

    private static string? Prompt(string label, int width, int height)
    {
        Console.SetCursorPosition(0, Math.Max(0, height - 1));
        Console.Write(label.PadRight(width));
        Console.SetCursorPosition(label.Length, Math.Max(0, height - 1));
        Console.CursorVisible = true;
        try { return Console.ReadLine(); }
        finally { Console.CursorVisible = false; Console.Clear(); }
    }

    private static void Guard(SkillTreeBook book, Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            book.Message = "File error: " + ex.Message;
        }
    }
}
