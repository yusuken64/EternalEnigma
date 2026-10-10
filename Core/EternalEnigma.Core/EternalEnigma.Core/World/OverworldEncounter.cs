namespace EternalEnigma.Core.World;

/// <summary>Reserved miniature ground. Terrain remains authoritative; hosts render decorations without changing movement.</summary>
public sealed class GridLocationFootprint
{
    public string LocationId { get; }
    public GridPoint Entrance { get; }
    public GridPoint Inward { get; }
    public GridPoint Approach => new(Entrance.X - Inward.X, Entrance.Y - Inward.Y);
    public GridPoint? OtherApproach { get; }
    public int Size { get; }
    public EternalEnigma.Core.Progression.EncounterKind? EncounterKind { get; }
    public IReadOnlyList<GridPoint> Cells { get; }
    public GridLocationFootprint(string id, GridPoint entrance, GridPoint inward, int size, GridPoint? otherApproach = null,
        EternalEnigma.Core.Progression.EncounterKind? encounterKind = null, GridPoint? center = null)
    {
        LocationId = id; Entrance = entrance; Inward = inward; Size = size; OtherApproach = otherApproach; EncounterKind = encounterKind;
        var at = center ?? entrance;
        Cells = Array.AsReadOnly(Enumerable.Range(0, size * size).Select(i => new GridPoint(at.X + i % size - size / 2, at.Y + i / size - size / 2)).ToArray());
    }
}

public enum OverworldPickupKind { Weapon, SkillScroll }
public sealed class OverworldPickup
{
    public string Id { get; }
    public string RegionId { get; }
    public GridPoint Position { get; }
    public OverworldPickupKind Kind { get; }
    /// <summary>Stable selection roll; host filters and sorts its eligible existing definitions before applying modulo.</summary>
    public int Roll { get; }
    public OverworldPickup(string id, string regionId, GridPoint position, OverworldPickupKind kind, int roll)
    { Id = id; RegionId = regionId; Position = position; Kind = kind; Roll = roll; }
}
