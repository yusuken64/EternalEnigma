namespace EternalEnigma.Core.Classes;

// An editable skill tree: one class's skills laid out in tiers. It is the in-memory form of a
// .skilltree file (see SkillTreeFormat) and converts to and from the immutable ClassSkillTable
// that the learning rules use. Authoring order inside a tier is preserved.
public sealed class SkillTreeDocument
{
    public const int TierCount = 3;

    private readonly List<ClassSkillEntry> entries = new();
    private string classId;

    public SkillTreeDocument(string classId, string? displayName = null)
    {
        this.classId = RequireClassId(classId);
        DisplayName = displayName ?? "";
    }

    public string ClassId
    {
        get => classId;
        set => classId = RequireClassId(value);
    }

    public string DisplayName { get; set; }
    public IReadOnlyList<ClassSkillEntry> Entries => entries;

    public static SkillTreeDocument FromTable(ClassSkillTable table, string? displayName = null)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        var document = new SkillTreeDocument(table.ClassId, displayName);
        document.entries.AddRange(table.Skills);
        return document;
    }

    public ClassSkillTable ToTable() => new ClassSkillTable(ClassId, entries);

    public SkillTreeDocument Clone()
    {
        var copy = new SkillTreeDocument(ClassId, DisplayName);
        copy.entries.AddRange(entries);
        return copy;
    }

    public ClassSkillEntry? Find(string skillId)
    {
        foreach (var entry in entries)
        {
            if (string.Equals(entry.SkillId, skillId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    public IEnumerable<ClassSkillEntry> InTier(int tier)
    {
        foreach (var entry in entries)
        {
            if (entry.Tier == tier)
                yield return entry;
        }
    }

    // Sets a skill in the tree: replaces the existing entry in place, or appends a new one.
    // Returns true when the skill was newly added.
    public bool Set(string skillId, int tier, SkillKind kind, int maxRank)
    {
        RequireSkillId(skillId);
        var entry = new ClassSkillEntry(skillId.Trim(), tier, maxRank, kind);
        int index = IndexOf(entry.SkillId);
        if (index >= 0)
        {
            entries[index] = entry;
            return false;
        }

        entries.Add(entry);
        return true;
    }

    // Unsets a skill. Returns false when it was not in the tree.
    public bool Remove(string skillId)
    {
        int index = IndexOf(skillId);
        if (index < 0)
            return false;

        entries.RemoveAt(index);
        return true;
    }

    public void MoveToTier(string skillId, int tier)
    {
        var entry = RequireEntry(skillId);
        Set(entry.SkillId, tier, entry.Kind, entry.MaxRank);
    }

    public void SetMaxRank(string skillId, int maxRank)
    {
        var entry = RequireEntry(skillId);
        Set(entry.SkillId, entry.Tier, entry.Kind, maxRank);
    }

    // Changing to a non-normal kind forces a single rank.
    public void SetKind(string skillId, SkillKind kind)
    {
        var entry = RequireEntry(skillId);
        Set(entry.SkillId, entry.Tier, kind, kind == SkillKind.Normal ? entry.MaxRank : 1);
    }

    public IReadOnlyList<string> Validate() => ClassKitValidator.Validate(ToTable());

    private ClassSkillEntry RequireEntry(string skillId) =>
        Find(skillId) ?? throw new ArgumentException($"'{skillId}' is not in the {ClassId} tree.", nameof(skillId));

    private int IndexOf(string skillId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].SkillId, skillId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static string RequireClassId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Class id is required.", nameof(value));

        if (value.IndexOfAny(new[] { '|', '\r', '\n' }) >= 0)
            throw new ArgumentException("Class id cannot contain '|' or line breaks.", nameof(value));

        return value.Trim();
    }

    private static void RequireSkillId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Skill id is required.", nameof(value));

        if (value.IndexOfAny(new[] { '|', '\r', '\n' }) >= 0)
            throw new ArgumentException("Skill id cannot contain '|' or line breaks.", nameof(value));
    }
}
