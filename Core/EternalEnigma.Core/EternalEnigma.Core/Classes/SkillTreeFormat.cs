using System.Globalization;
using System.Text;

namespace EternalEnigma.Core.Classes;

public sealed class SkillTreeFormatException : FormatException
{
    public SkillTreeFormatException(int line, string message)
        : base(line > 0 ? $"line {line}: {message}" : message)
    {
        Line = line;
    }

    public int Line { get; }
}

// The .skilltree text format: plain, diff-friendly and free of dependencies so the same DLL can
// run in Unity. One tree per file.
//
//   # comment (whole line only; skill names may contain '#')
//   skilltree 1
//   class warrior
//   name Warrior
//
//   tier 1
//     mastery   | Novice Training
//     gathering | Mining
//     skill     | Double Strike | 5
//   tier 2
//     single    | Initiative
//
// Entry kinds: skill (normal, rank column required, 1-5), mastery, gathering, single (rank 1).
public static class SkillTreeFormat
{
    public const int Version = 1;
    public const string Extension = ".skilltree";

    public static SkillTreeDocument Parse(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        SkillTreeDocument? document = null;
        string? classId = null;
        string name = "";
        var pending = new List<(int line, string id, int tier, SkillKind kind, int rank)>();
        bool sawHeader = false;
        int tier = 0;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNo = i + 1;
            string line = lines[i].Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (!sawHeader)
            {
                var header = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (header.Length != 2 || header[0] != "skilltree")
                    throw new SkillTreeFormatException(lineNo, "expected 'skilltree <version>' first.");

                if (!int.TryParse(header[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) || version != Version)
                    throw new SkillTreeFormatException(lineNo, $"unsupported skilltree version '{header[1]}'.");

                sawHeader = true;
                continue;
            }

            if (line.IndexOf('|') < 0)
            {
                var (keyword, value) = SplitKeyword(line);
                switch (keyword)
                {
                    case "class":
                        if (classId != null)
                            throw new SkillTreeFormatException(lineNo, "duplicate 'class'.");
                        if (value.Length == 0)
                            throw new SkillTreeFormatException(lineNo, "'class' needs an id.");
                        classId = value;
                        break;
                    case "name":
                        name = value;
                        break;
                    case "tier":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out tier) ||
                            tier < 1 || tier > SkillTreeDocument.TierCount)
                            throw new SkillTreeFormatException(lineNo, $"tier must be 1-{SkillTreeDocument.TierCount}.");
                        break;
                    default:
                        throw new SkillTreeFormatException(lineNo, $"unrecognized line '{line}'.");
                }

                continue;
            }

            if (tier == 0)
                throw new SkillTreeFormatException(lineNo, "skill listed before any 'tier'.");

            var fields = line.Split('|');
            for (int f = 0; f < fields.Length; f++)
                fields[f] = fields[f].Trim();

            if (!TryParseKind(fields[0], out var kind))
                throw new SkillTreeFormatException(lineNo, $"unknown entry kind '{fields[0]}' (use skill, mastery, gathering or single).");

            if (fields.Length < 2 || fields[1].Length == 0)
                throw new SkillTreeFormatException(lineNo, "entry needs a skill name.");

            if (fields.Length > 3)
                throw new SkillTreeFormatException(lineNo, "too many fields.");

            int rank = 1;
            if (fields.Length == 3)
            {
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out rank))
                    throw new SkillTreeFormatException(lineNo, $"rank '{fields[2]}' is not a number.");
            }
            else if (kind == SkillKind.Normal)
            {
                throw new SkillTreeFormatException(lineNo, "'skill' entries need a rank (skill | Name | 1-5).");
            }

            pending.Add((lineNo, fields[1], tier, kind, rank));
        }

        if (!sawHeader)
            throw new SkillTreeFormatException(0, "empty file; expected 'skilltree 1'.");

        if (classId == null)
            throw new SkillTreeFormatException(0, "missing 'class'.");

        document = new SkillTreeDocument(classId, name);
        foreach (var (lineNo, id, entryTier, kind, rank) in pending)
        {
            try
            {
                if (!document.Set(id, entryTier, kind, rank))
                    throw new SkillTreeFormatException(lineNo, $"duplicate skill '{id}'.");
            }
            catch (ArgumentException ex)
            {
                throw new SkillTreeFormatException(lineNo, ex.Message.Split('\n')[0].Trim());
            }
        }

        return document;
    }

    public static string Write(SkillTreeDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var sb = new StringBuilder();
        sb.Append("skilltree ").Append(Version).Append('\n');
        sb.Append("class ").Append(document.ClassId).Append('\n');
        if (document.DisplayName.Length > 0)
            sb.Append("name ").Append(document.DisplayName).Append('\n');

        for (int tier = 1; tier <= SkillTreeDocument.TierCount; tier++)
        {
            var inTier = document.InTier(tier).ToList();
            if (inTier.Count == 0)
                continue;

            sb.Append('\n').Append("tier ").Append(tier).Append('\n');
            foreach (var entry in inTier)
            {
                sb.Append("  ").Append(KindKeyword(entry.Kind).PadRight(9)).Append(" | ").Append(entry.SkillId);
                if (entry.Kind == SkillKind.Normal)
                    sb.Append(" | ").Append(entry.MaxRank);
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    public static string KindKeyword(SkillKind kind) => kind switch
    {
        SkillKind.Normal => "skill",
        SkillKind.Mastery => "mastery",
        SkillKind.Gathering => "gathering",
        SkillKind.SingleRank => "single",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static bool TryParseKind(string keyword, out SkillKind kind)
    {
        switch (keyword.ToLowerInvariant())
        {
            case "skill": kind = SkillKind.Normal; return true;
            case "mastery": kind = SkillKind.Mastery; return true;
            case "gathering": kind = SkillKind.Gathering; return true;
            case "single": kind = SkillKind.SingleRank; return true;
            default: kind = SkillKind.Normal; return false;
        }
    }

    private static (string keyword, string value) SplitKeyword(string line)
    {
        int space = line.IndexOfAny(new[] { ' ', '\t' });
        return space < 0
            ? (line, "")
            : (line.Substring(0, space), line.Substring(space + 1).Trim());
    }
}
