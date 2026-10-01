using EternalEnigma.Core.Classes;

namespace EternalEnigma.ConsoleExplorer;

/// <summary>One .skilltree file being browsed: the editable tree, where it saves, and its undo history.</summary>
public sealed class SkillTreeFile
{
    private const int UndoLimit = 50;
    private readonly List<SkillTreeDocument> undo = new();
    private string savedText;

    public SkillTreeFile(SkillTreeDocument document, string? path)
    {
        Document = document;
        Path = path;
        savedText = SkillTreeFormat.Write(document);
    }

    public SkillTreeDocument Document { get; private set; }
    public string? Path { get; set; }
    public bool Dirty => SkillTreeFormat.Write(Document) != savedText;
    public bool CanUndo => undo.Count > 0;

    public void MarkSaved() => savedText = SkillTreeFormat.Write(Document);

    public void Snapshot()
    {
        undo.Add(Document.Clone());
        if (undo.Count > UndoLimit)
            undo.RemoveAt(0);
    }

    public void Undo()
    {
        Document = undo[^1];
        undo.RemoveAt(undo.Count - 1);
    }

    // Replaces the tree with an imported one; the change can be undone.
    public void Replace(SkillTreeDocument document)
    {
        Snapshot();
        Document = document;
    }
}

/// <summary>
/// A set of skill trees opened in the explorer, plus the selection and editing commands. It has no
/// console dependency so the commands can be tested; <see cref="SkillTreeScreen"/> draws it.
/// </summary>
public sealed class SkillTreeBook
{
    private bool confirmDiscard;

    public SkillTreeBook(string? directory = null)
    {
        Directory = directory;
    }

    public List<SkillTreeFile> Files { get; } = new();
    public int Index { get; private set; }
    public int Selection { get; private set; }
    public string Message { get; set; } = "";
    public string? Directory { get; }

    public SkillTreeFile? Current => Files.Count == 0 ? null : Files[Math.Clamp(Index, 0, Files.Count - 1)];
    public bool AnyDirty => Files.Any(f => f.Dirty);

    /// <summary>Opens one .skilltree file, or every .skilltree in a directory (bad files are reported, not fatal).</summary>
    public static SkillTreeBook Open(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            var book = new SkillTreeBook(path);
            var problems = new List<string>();
            foreach (string file in System.IO.Directory.GetFiles(path, "*" + SkillTreeFormat.Extension).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try { book.Files.Add(Load(file)); }
                catch (SkillTreeFormatException ex) { problems.Add($"{System.IO.Path.GetFileName(file)}: {ex.Message}"); }
            }

            book.Message = problems.Count > 0 ? "Skipped " + string.Join("; ", problems)
                : book.Files.Count == 0 ? "No .skilltree files in " + path : "";
            return book;
        }

        var single = new SkillTreeBook(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
        single.Files.Add(Load(path));
        return single;
    }

    /// <summary>Finds Docs/SkillTrees by walking up from the working directory and the executable.</summary>
    public static string? FindDefaultDirectory()
    {
        foreach (string start in new[] { System.IO.Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (int depth = 0; dir != null && depth < 10; depth++, dir = dir.Parent)
            {
                string candidate = System.IO.Path.Combine(dir.FullName, "Docs", "SkillTrees");
                if (System.IO.Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public static SkillTreeFile Load(string path) =>
        new(SkillTreeFormat.Parse(File.ReadAllText(path)), path);

    /// <summary>Entries in display order: tier 1 to 3, authoring order inside a tier.</summary>
    public IReadOnlyList<ClassSkillEntry> Ordered()
    {
        var doc = Current?.Document;
        if (doc == null)
            return Array.Empty<ClassSkillEntry>();

        return Enumerable.Range(1, SkillTreeDocument.TierCount).SelectMany(doc.InTier).ToList();
    }

    public ClassSkillEntry? SelectedEntry
    {
        get
        {
            var ordered = Ordered();
            return ordered.Count == 0 ? null : ordered[Math.Clamp(Selection, 0, ordered.Count - 1)];
        }
    }

    public void MoveSelection(int delta)
    {
        confirmDiscard = false;
        Selection = Math.Clamp(Selection + delta, 0, Math.Max(0, Ordered().Count - 1));
    }

    public void SwitchTree(int delta)
    {
        confirmDiscard = false;
        if (Files.Count == 0)
            return;

        Index = (Index + delta + Files.Count) % Files.Count;
        Selection = 0;
    }

    public void ShiftTier(int delta) => EditSelected(entry =>
    {
        int tier = entry.Tier + delta;
        if (tier < 1 || tier > SkillTreeDocument.TierCount)
            throw new ArgumentException("There is no tier " + tier + ".");

        Current!.Document.MoveToTier(entry.SkillId, tier);
        return entry.SkillId;
    });

    public void AdjustRank(int delta) => EditSelected(entry =>
    {
        if (entry.Kind != SkillKind.Normal)
            throw new ArgumentException(entry.Kind + " skills have a single rank.");

        Current!.Document.SetMaxRank(entry.SkillId, Math.Clamp(entry.MaxRank + delta, 1, 5));
        return entry.SkillId;
    });

    public void CycleKind() => EditSelected(entry =>
    {
        var kinds = new[] { SkillKind.Normal, SkillKind.Mastery, SkillKind.Gathering, SkillKind.SingleRank };
        var next = kinds[(Array.IndexOf(kinds, entry.Kind) + 1) % kinds.Length];
        Current!.Document.SetKind(entry.SkillId, next);
        return entry.SkillId;
    });

    public void Remove() => EditSelected(entry =>
    {
        Current!.Document.Remove(entry.SkillId);
        Message = $"Removed {entry.SkillId}.";
        return null;
    });

    public void Rename(string newName) => EditSelected(entry =>
    {
        newName = newName.Trim();
        if (Current!.Document.Find(newName) != null)
            throw new ArgumentException($"'{newName}' is already in the tree.");

        var doc = Current.Document;
        var all = doc.Entries.ToList();
        int at = all.FindIndex(e => e.SkillId == entry.SkillId);
        // Rebuild so the renamed skill keeps its place in the order.
        var rebuilt = new SkillTreeDocument(doc.ClassId, doc.DisplayName);
        for (int i = 0; i < all.Count; i++)
        {
            var e = all[i];
            rebuilt.Set(i == at ? newName : e.SkillId, e.Tier, e.Kind, e.MaxRank);
        }

        Current.Replace(rebuilt);
        return newName;
    }, snapshot: false);

    /// <summary>Adds (or resets) a skill in the selected skill's tier, or tier 1 in an empty tree.</summary>
    public void Add(string name, SkillKind kind = SkillKind.Normal)
    {
        confirmDiscard = false;
        var file = Current;
        if (file == null)
        {
            Message = "Import a tree first (I).";
            return;
        }

        try
        {
            int tier = SelectedEntry?.Tier ?? 1;
            file.Snapshot();
            bool added = file.Document.Set(name, tier, kind, kind == SkillKind.Normal ? 5 : 1);
            Message = added ? $"Added {name.Trim()} to tier {tier}." : $"{name.Trim()} already existed; reset it.";
            SelectSkill(name.Trim());
        }
        catch (ArgumentException ex)
        {
            file.Undo();
            Message = ex.Message.Split('\n')[0];
        }
    }

    public void Undo()
    {
        confirmDiscard = false;
        var file = Current;
        if (file == null || !file.CanUndo)
        {
            Message = "Nothing to undo.";
            return;
        }

        file.Undo();
        Selection = Math.Clamp(Selection, 0, Math.Max(0, Ordered().Count - 1));
        Message = "Undid last change.";
    }

    public void Save()
    {
        confirmDiscard = false;
        var file = Current;
        if (file == null)
            return;

        file.Path ??= System.IO.Path.Combine(Directory ?? System.IO.Directory.GetCurrentDirectory(), file.Document.ClassId + SkillTreeFormat.Extension);
        File.WriteAllText(file.Path, SkillTreeFormat.Write(file.Document));
        file.MarkSaved();
        Message = "Saved " + file.Path;
    }

    /// <summary>Writes the current tree to another file without changing where it saves.</summary>
    public void Export(string path)
    {
        confirmDiscard = false;
        var file = Current;
        if (file == null || string.IsNullOrWhiteSpace(path))
            return;

        path = path.Trim().Trim('"');
        if (System.IO.Path.GetExtension(path).Length == 0)
            path += SkillTreeFormat.Extension;

        File.WriteAllText(path, SkillTreeFormat.Write(file.Document));
        Message = "Exported to " + path;
    }

    /// <summary>Loads a tree from a file; it replaces an open tree of the same class (undoable) or is added.</summary>
    public void Import(string path)
    {
        confirmDiscard = false;
        path = path.Trim().Trim('"');
        try
        {
            var imported = Load(path).Document;
            var existing = Files.FirstOrDefault(f => f.Document.ClassId == imported.ClassId);
            if (existing != null)
            {
                existing.Replace(imported);
                Index = Files.IndexOf(existing);
                Message = $"Replaced {imported.ClassId} from {path} (Z undoes, S saves).";
            }
            else
            {
                Files.Add(new SkillTreeFile(imported, null));
                Index = Files.Count - 1;
                Message = $"Imported {imported.ClassId} from {path}; S saves it to the library.";
            }

            Selection = 0;
        }
        catch (Exception ex) when (ex is SkillTreeFormatException or IOException or UnauthorizedAccessException)
        {
            Message = "Import failed: " + ex.Message;
        }
    }

    /// <summary>True when Esc should leave; otherwise warns once about unsaved changes.</summary>
    public bool TryLeave()
    {
        if (!AnyDirty || confirmDiscard)
            return true;

        confirmDiscard = true;
        Message = "Unsaved changes. S: save, Esc again: discard and leave.";
        return false;
    }

    private void SelectSkill(string skillId)
    {
        int at = Ordered().ToList().FindIndex(e => e.SkillId == skillId);
        if (at >= 0)
            Selection = at;
    }

    private void EditSelected(Func<ClassSkillEntry, string?> edit, bool snapshot = true)
    {
        confirmDiscard = false;
        var file = Current;
        var entry = SelectedEntry;
        if (file == null || entry == null)
        {
            Message = "Nothing selected.";
            return;
        }

        if (snapshot)
            file.Snapshot();

        try
        {
            string? follow = edit(entry);
            if (follow != null)
                SelectSkill(follow);
            else
                Selection = Math.Clamp(Selection, 0, Math.Max(0, Ordered().Count - 1));
        }
        catch (ArgumentException ex)
        {
            if (snapshot)
                file.Undo();
            Message = ex.Message.Split('\n')[0];
        }
    }
}
