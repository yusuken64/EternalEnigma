using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

/// <summary>Normalized OR of ANDs. An empty conjunction represents an open route.</summary>
public sealed class Requirement
{
    public IReadOnlyList<CapabilitySet> Alternatives { get; }
    public static Requirement Open { get; } = new(CapabilitySet.Empty);
    public bool IsOpen => Alternatives.Any(a => a.Count == 0);

    public Requirement(params CapabilitySet[] alternatives)
    {
        if (alternatives == null || alternatives.Length == 0)
            throw new ArgumentException("At least one solution is required.", nameof(alternatives));
        var unique = alternatives.Distinct().ToArray();
        var normalized = unique.Where(a => !unique.Any(b => !a.Equals(b) && a.ContainsAll(b)))
            .OrderBy(a => a.GetHashCode()).ToArray();
        if (normalized.Length > 3 || normalized.Any(a => a.Count > 2))
            throw new ArgumentException("Locks support 1–3 alternatives of at most two capabilities.", nameof(alternatives));
        Alternatives = Array.AsReadOnly(normalized);
    }

    public bool IsSatisfiedBy(CapabilitySet held) => Alternatives.Any(held.ContainsAll);
    public override string ToString() => string.Join("|", Alternatives.Select(a => a.ToString()));

    /// <summary>Player-facing wording, e.g. "Boat, or Climbing Gear and Rope". ToString stays the diagnostic form.</summary>
    public string Describe(Func<Capability, string>? name = null)
    {
        name ??= c => c.DisplayName();
        return string.Join(", or ", Alternatives.Where(a => a.Count > 0).Select(a => string.Join(" and ", a.Values.Select(name))));
    }
}
