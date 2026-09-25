namespace EternalEnigma.Core.Classes;

public sealed class ClassKit
{
    public ClassKit(ClassSkillTable primary, ClassSkillTable? secondary = null)
    {
        if (primary == null)
            throw new ArgumentNullException(nameof(primary));

        if (secondary != null && string.Equals(primary.ClassId, secondary.ClassId, StringComparison.Ordinal))
            throw new ArgumentException("Primary and secondary class must differ.", nameof(secondary));

        Primary = primary;
        Secondary = secondary;
    }

    public ClassSkillTable Primary { get; }
    public ClassSkillTable? Secondary { get; }
    public bool IsCombination => Secondary != null;
}
