namespace EternalEnigma.Core.Capabilities;

// Values are stable identifiers used in campaign fingerprints. Never reorder.
public enum Capability
{
    Climb, Grapple, Icewalk, DeepDive, Breach, WaystoneStep, PhaseShift, HazardWard,
    Boat, Icebreaker, Airship, Tunneler,
    Engineering, Translation, RoyalAuthority, AncientAttunement, BeastSpeech,
    GuildStanding, Remedy, Diplomacy, SacredRite
}

public enum CapabilityKind { Personal, Vehicle, Utility }
public enum CapabilityRole { Critical, Exploratory }

public static class CapabilityCatalog
{
    public static IReadOnlyList<Capability> All { get; } =
        Array.AsReadOnly((Capability[])Enum.GetValues(typeof(Capability)));

    public static CapabilityKind Kind(this Capability capability)
    {
        if (!Enum.IsDefined(typeof(Capability), capability)) throw new ArgumentOutOfRangeException(nameof(capability));
        return capability <= Capability.HazardWard ? CapabilityKind.Personal :
            capability <= Capability.Tunneler ? CapabilityKind.Vehicle : CapabilityKind.Utility;
    }

    public static bool HasAreaForm(this Capability capability) => capability.Kind() == CapabilityKind.Vehicle ||
        capability == Capability.Climb || capability == Capability.Icewalk || capability == Capability.DeepDive ||
        capability == Capability.PhaseShift || capability == Capability.HazardWard;

    public static bool IsNarrative(this Capability capability) =>
        capability == Capability.Remedy || capability == Capability.Diplomacy || capability == Capability.SacredRite;
}

public readonly struct CapabilitySet : IEquatable<CapabilitySet>
{
    private readonly uint bits;
    private CapabilitySet(uint bits) { this.bits = bits; }
    public static CapabilitySet Empty => default;
    public static CapabilitySet Of(params Capability[] capabilities) => From(capabilities);
    public static CapabilitySet From(IEnumerable<Capability> capabilities)
    {
        if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
        uint value = 0;
        foreach (var capability in capabilities)
        {
            _ = capability.Kind();
            value |= 1u << (int)capability;
        }
        return new CapabilitySet(value);
    }
    public bool Contains(Capability capability) { _ = capability.Kind(); return (bits & (1u << (int)capability)) != 0; }
    public bool ContainsAll(CapabilitySet required) => (bits & required.bits) == required.bits;
    public CapabilitySet Union(CapabilitySet other) => new(bits | other.bits);
    public IEnumerable<Capability> Values => CapabilityCatalog.All.Where(Contains);
    public int Count => Values.Count();
    public bool Equals(CapabilitySet other) => bits == other.bits;
    public override bool Equals(object? obj) => obj is CapabilitySet other && Equals(other);
    public override int GetHashCode() => (int)bits;
    public override string ToString() => string.Join("+", Values);
}

public sealed class ActivatedCapability
{
    public Capability Id { get; }
    public CapabilityRole Role { get; }
    public int Tier { get; }
    public ActivatedCapability(Capability id, CapabilityRole role, int tier)
    { _ = id.Kind(); Id = id; Role = role; Tier = tier; }
}
