namespace EternalEnigma.Core.Classes;

public sealed class ClassSkillTable
{
    public ClassSkillTable(string classId, IEnumerable<ClassSkillEntry> skills)
    {
        if (string.IsNullOrWhiteSpace(classId))
            throw new ArgumentException("Class id is required.", nameof(classId));

        if (skills == null)
            throw new ArgumentNullException(nameof(skills));

        var copy = skills.ToArray();
        for (int i = 0; i < copy.Length; i++)
        {
            if (copy[i] == null)
                throw new ArgumentException("Skill entries cannot be null.", nameof(skills));
        }

        ClassId = classId;
        Skills = Array.AsReadOnly(copy);
    }

    public string ClassId { get; }
    public IReadOnlyList<ClassSkillEntry> Skills { get; }

    public ClassSkillEntry? Find(string skillId)
    {
        if (skillId == null)
            return null;

        foreach (var skill in Skills)
        {
            if (string.Equals(skill.SkillId, skillId, StringComparison.Ordinal))
                return skill;
        }

        return null;
    }

    public ClassSkillEntry? MasteryForTier(int tier)
    {
        foreach (var skill in Skills)
        {
            if (skill.Kind == SkillKind.Mastery && skill.Tier == tier)
                return skill;
        }

        return null;
    }
}
