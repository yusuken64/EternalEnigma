namespace EternalEnigma.Core.Progression;

public enum TownServiceKind { Bakery, Consumables, Items, Inn, Trainer }

/// <summary>One building-sized service a town offers. Trainers are split by class.</summary>
public sealed class TownService : IEquatable<TownService>
{
    public TownServiceKind Kind { get; }
    /// <summary>Class taught by a trainer; null for every other kind.</summary>
    public string? ClassId { get; }
    public string Id => Kind == TownServiceKind.Trainer ? "trainer-" + ClassId : Kind.ToString().ToLowerInvariant();
    /// <summary>Every service is used inside its building: the door leads to a walk-in room with a vendor or trainer.</summary>
    public bool HasInterior => true;

    private TownService(TownServiceKind kind, string? classId) { Kind = kind; ClassId = classId; }

    public static TownService Shop(TownServiceKind kind)
    {
        if (kind == TownServiceKind.Trainer) throw new ArgumentException("Use Trainer(classId) for trainers.", nameof(kind));
        return new TownService(kind, null);
    }
    public static TownService Trainer(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId)) throw new ArgumentException("Class id is required.", nameof(classId));
        return new TownService(TownServiceKind.Trainer, classId);
    }

    public bool Equals(TownService? other) => other != null && Kind == other.Kind && ClassId == other.ClassId;
    public override bool Equals(object? obj) => Equals(obj as TownService);
    public override int GetHashCode() => Kind.GetHashCode() * 397 ^ (ClassId?.GetHashCode() ?? 0);
    public override string ToString() => Id;
}

/// <summary>Which services each town offers. Today every town offers all of them.</summary>
public static class TownServiceCatalog
{
    /// <summary>Class ids, matching the ClassDefinition assets.</summary>
    public static readonly IReadOnlyList<string> ClassIds = Array.AsReadOnly(new[]
    {
        "warrior", "guardian", "archer", "elementalist", "healer", "bard", "occultist", "rogue", "commander", "scout"
    });

    public static readonly IReadOnlyList<TownService> All = Array.AsReadOnly(
        new[] { TownService.Shop(TownServiceKind.Bakery), TownService.Shop(TownServiceKind.Consumables), TownService.Shop(TownServiceKind.Items), TownService.Shop(TownServiceKind.Inn) }
            .Concat(ClassIds.Select(TownService.Trainer)).ToArray());

    /// <summary>Extension point for per-town variety: tier and stage are available for later rules.</summary>
    public static IReadOnlyList<TownService> ServicesFor(int tier, int stage) => All;
}
