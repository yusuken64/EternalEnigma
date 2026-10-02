namespace EternalEnigma.Core.Progression;

public enum TownServiceKind { Bakery, Consumables, Items, Inn, Trainer }

/// <summary>One building-sized service a town offers. The single trainer opens the menu for the visiting hero's class.</summary>
public sealed class TownService : IEquatable<TownService>
{
    public TownServiceKind Kind { get; }
    public string Id => Kind.ToString().ToLowerInvariant();
    /// <summary>Every service is used inside its building: the door leads to a walk-in room with a vendor or trainer.</summary>
    public bool HasInterior => true;

    private TownService(TownServiceKind kind) { Kind = kind; }

    public static TownService Shop(TownServiceKind kind) => new TownService(kind);

    public bool Equals(TownService? other) => other != null && Kind == other.Kind;
    public override bool Equals(object? obj) => Equals(obj as TownService);
    public override int GetHashCode() => Kind.GetHashCode();
    public override string ToString() => Id;
}

/// <summary>Which services each town offers. Today every town offers all of them.</summary>
public static class TownServiceCatalog
{
    public static readonly IReadOnlyList<TownService> All = Array.AsReadOnly(new[]
    {
        TownService.Shop(TownServiceKind.Bakery), TownService.Shop(TownServiceKind.Consumables), TownService.Shop(TownServiceKind.Items),
        TownService.Shop(TownServiceKind.Inn), TownService.Shop(TownServiceKind.Trainer),
    });

    /// <summary>Extension point for per-town variety: tier and stage are available for later rules.</summary>
    public static IReadOnlyList<TownService> ServicesFor(int tier, int stage) => All;
}
