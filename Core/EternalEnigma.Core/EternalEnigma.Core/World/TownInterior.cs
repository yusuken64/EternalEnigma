namespace EternalEnigma.Core.World;

public enum TownInteriorKind { None, Residential, Shop, Inn, Trainer }
public enum TownShopTheme { General, Bakery, Consumables, Equipment }

public sealed class TownInteriorSpec : IEquatable<TownInteriorSpec>
{
    public TownInteriorKind Kind { get; }
    public TownShopTheme Theme { get; }
    public bool HasVendor => Kind is TownInteriorKind.Shop or TownInteriorKind.Inn or TownInteriorKind.Trainer;
    public TownInteriorSpec(TownInteriorKind kind, TownShopTheme theme = TownShopTheme.General)
    {
        if (!Enum.IsDefined(typeof(TownInteriorKind), kind) || !Enum.IsDefined(typeof(TownShopTheme), theme))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind; Theme = theme;
    }
    public bool Equals(TownInteriorSpec? other) => other != null && Kind == other.Kind && Theme == other.Theme;
    public override bool Equals(object? obj) => Equals(obj as TownInteriorSpec);
    public override int GetHashCode() => (int)Kind * 397 ^ (int)Theme;
}

public sealed class TownPropPlacement
{
    public string Asset { get; }
    public GridPoint Cell { get; }
    public int QuarterTurns { get; }
    public float Elevation { get; }
    public bool Blocks { get; }
    public TownPropPlacement(string asset, GridPoint cell, int quarterTurns = 0, float elevation = 0, bool blocks = true)
    { Asset = asset; Cell = cell; QuarterTurns = quarterTurns; Elevation = elevation; Blocks = blocks; }
}

public sealed class TownInterior
{
    public GridPoint Door { get; }
    public TownInteriorSpec Spec { get; }
    public int Arrangement { get; }
    public bool Mirrored { get; }
    public IReadOnlyList<TownPropPlacement> Props { get; }
    public IReadOnlyList<GridPoint> Occupied { get; }
    public IReadOnlyList<GridPoint> Carpet { get; }
    public IReadOnlyList<GridPoint> Counters { get; }
    public IReadOnlyList<GridPoint> Reserved { get; }
    internal TownInterior(GridPoint door, TownInteriorSpec spec, int arrangement, bool mirrored,
        IEnumerable<TownPropPlacement> props, IEnumerable<GridPoint> occupied, IEnumerable<GridPoint> carpet,
        IEnumerable<GridPoint> counters, IEnumerable<GridPoint> reserved)
    {
        Door = door; Spec = spec; Arrangement = arrangement; Mirrored = mirrored;
        Props = Array.AsReadOnly(props.ToArray()); Occupied = Array.AsReadOnly(occupied.ToArray());
        Carpet = Array.AsReadOnly(carpet.ToArray()); Counters = Array.AsReadOnly(counters.ToArray());
        Reserved = Array.AsReadOnly(reserved.ToArray());
    }
}
