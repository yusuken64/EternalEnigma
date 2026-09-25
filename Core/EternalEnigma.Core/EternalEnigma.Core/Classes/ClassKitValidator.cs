namespace EternalEnigma.Core.Classes;

public static class ClassKitValidator
{
    public static IReadOnlyList<string> Validate(ClassSkillTable table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        var errors = new List<string>();

        // Check for duplicates
        var seenSkills = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in table.Skills)
        {
            if (!seenSkills.Add(skill.SkillId))
            {
                duplicates.Add(skill.SkillId);
            }
        }
        foreach (var id in duplicates)
        {
            errors.Add($"{table.ClassId}: duplicate skill '{id}'.");
        }

        // Check for mastery tiers
        for (int tier = 1; tier <= 3; tier++)
        {
            int count = 0;
            foreach (var skill in table.Skills)
            {
                if (skill.Kind == SkillKind.Mastery && skill.Tier == tier)
                {
                    count++;
                }
            }

            if (count == 0)
                errors.Add($"{table.ClassId}: missing tier {tier} mastery.");
            else if (count > 1)
                errors.Add($"{table.ClassId}: more than one tier {tier} mastery.");
        }

        return errors.AsReadOnly();
    }

    public static IReadOnlyList<string> Validate(ClassKit kit)
    {
        if (kit == null)
            throw new ArgumentNullException(nameof(kit));

        var errors = new List<string>();

        // Add errors from primary table
        errors.AddRange(Validate(kit.Primary));

        // Add errors from secondary table if present
        if (kit.Secondary != null)
        {
            errors.AddRange(Validate(kit.Secondary));
        }

        return errors.AsReadOnly();
    }
}
