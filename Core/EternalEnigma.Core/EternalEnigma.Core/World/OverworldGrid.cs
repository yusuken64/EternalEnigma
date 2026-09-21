using System.Collections.ObjectModel;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.World;

public static class OverworldLayers
{
    public const string Ground = "Ground";
    public const string Walkable = "Walkable";
    public const string Roads = "Roads";
    public const string Mountains = "Mountains";
    public const string Trees = "Trees";
    public const string Water = "Water";
    public const string NavigableWater = "NavigableWater";
    public const string Bridges = "Bridges";
    public static string Landscape(OverworldBiome biome) => "Landscape/" + biome;
    public static string Biome(OverworldBiome biome) => "Biome/" + biome;
    public const string Reserved = "Reserved";
    public const string Towns = "Towns";
    public const string StoryDungeons = "StoryDungeons";
    public const string RepeatableDungeons = "RepeatableDungeons";
    public const string FinalDungeon = "FinalDungeon";
    public const string Converters = "Converters";
    public const string Landmarks = "Landmarks";
    public const string Secrets = "Secrets";
    public const string PlayerStart = "PlayerStart";
    public const string Locks = "Locks";
    public const string AreaLocks = "AreaLocks";
    public const string ObstacleLocks = "ObstacleLocks";
    public const string InteractionLocks = "InteractionLocks";
    public static string Region(string id) => "Region/" + id;
}

/// <summary>Immutable x/y mask. ToArray returns a defensive copy compatible with TWC bool[x,y].</summary>
public sealed class GridLayer
{
    private readonly bool[,] cells;
    public int Width => cells.GetLength(0);
    public int Height => cells.GetLength(1);
    public bool this[int x, int y] => cells[x, y];
    public GridLayer(bool[,] cells) { this.cells = (bool[,])(cells ?? throw new ArgumentNullException(nameof(cells))).Clone(); }
    public bool[,] ToArray() => (bool[,])cells.Clone();
}

public sealed class GridLock
{
    public string RouteId { get; }
    public IReadOnlyList<GridPoint> Cells { get; }
    public GridLock(string routeId, IEnumerable<GridPoint> cells)
    { RouteId = routeId; Cells = Array.AsReadOnly(cells.ToArray()); }
}

/// <summary>Static ground plus conditional lock footprints. Boolean render layers are not progression authority.</summary>
public sealed class OverworldGrid
{
    private readonly int[,] lockIndices;
    private readonly CampaignRoute[] lockRoutes;
    public const int GenerationVersion = 9;
    public int Width { get; }
    public int Height { get; }
    public int CampaignSeed { get; }
    public string CampaignFingerprint { get; }
    public IReadOnlyDictionary<string, GridLayer> Layers { get; }
    public IReadOnlyDictionary<string, GridPoint> Locations { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<GridPoint>> Routes { get; }
    public IReadOnlyList<GridLock> Locks { get; }
    public IReadOnlyList<CampaignRoute> Warps { get; }
    public IReadOnlyDictionary<string, OverworldBiome> RegionBiomes { get; }
    public GridPoint PlayerStart { get; }

    internal OverworldGrid(Campaign campaign, IDictionary<string, GridLayer> layers,
        IDictionary<string, GridPoint> locations, IDictionary<string, IReadOnlyList<GridPoint>> routes, IEnumerable<GridLock> locks,
        IDictionary<string, OverworldBiome>? regionBiomes = null)
    {
        CampaignSeed = campaign.Seed;
        CampaignFingerprint = Generation.CampaignFingerprint.Compute(campaign);
        RegionBiomes = new ReadOnlyDictionary<string, OverworldBiome>(new Dictionary<string, OverworldBiome>(
            regionBiomes ?? campaign.Regions.ToDictionary(r => r.Id, _ => OverworldBiome.Grassland), StringComparer.Ordinal));
        Layers = new ReadOnlyDictionary<string, GridLayer>(new Dictionary<string, GridLayer>(layers, StringComparer.Ordinal));
        Width = Layers[OverworldLayers.Ground].Width; Height = Layers[OverworldLayers.Ground].Height;
        if (Layers.Values.Any(l => l.Width != Width || l.Height != Height)) throw new ArgumentException("All grid layers must have the same dimensions.");
        Locations = new ReadOnlyDictionary<string, GridPoint>(new Dictionary<string, GridPoint>(locations, StringComparer.Ordinal));
        Routes = new ReadOnlyDictionary<string, IReadOnlyList<GridPoint>>(routes.ToDictionary(r => r.Key,
            r => (IReadOnlyList<GridPoint>)Array.AsReadOnly(r.Value.ToArray()), StringComparer.Ordinal));
        Locks = Array.AsReadOnly(locks.ToArray());
        Warps = Array.AsReadOnly(campaign.Routes.Where(r => r.IsWarp).ToArray());
        PlayerStart = Locations[campaign.StartLocationId];
        lockRoutes = Locks.Select(l => campaign.Routes.Single(r => r.Id == l.RouteId)).ToArray();
        lockIndices = new int[Width, Height];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) lockIndices[x, y] = -1;
        for (int i = 0; i < Locks.Count; i++)
        foreach (var cell in Locks[i].Cells)
        {
            if (!Contains(cell) || lockIndices[cell.X, cell.Y] >= 0) throw new ArgumentException("Lock footprints must be in bounds and cannot overlap.");
            lockIndices[cell.X, cell.Y] = i;
        }
    }

    public bool Contains(GridPoint cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
    public bool IsGround(GridPoint cell) => Contains(cell) && Layers[OverworldLayers.Ground][cell.X, cell.Y];
    public GridLock? LockAt(GridPoint cell) => Contains(cell) && lockIndices[cell.X, cell.Y] >= 0 ? Locks[lockIndices[cell.X, cell.Y]] : null;
    public bool RequiresBoat(GridPoint cell) => Contains(cell) && Layers.TryGetValue(OverworldLayers.NavigableWater, out var water) && water[cell.X, cell.Y];
    public OverworldBiome? BiomeAt(GridPoint cell)
    {
        if (!IsGround(cell)) return null;
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
            if (Layers.TryGetValue(OverworldLayers.Biome(biome), out var layer) && layer[cell.X, cell.Y]) return biome;
        return null;
    }
    public bool IsWalkable(GridPoint cell, CapabilitySet held, ISet<string>? resolvedLocks = null)
    {
        if (!IsGround(cell)) return false;
        if (RequiresBoat(cell) && !held.Contains(Capability.Boat)) return false;
        int index = lockIndices[cell.X, cell.Y];
        if (index < 0) return true;
        var route = lockRoutes[index];
        return route.CanTraverse(held, resolvedLocks ?? new HashSet<string>());
    }
    public bool CanStep(GridPoint from, GridPoint to, CapabilitySet held, ISet<string>? resolvedLocks = null) =>
        OverworldMovement.CanStep(from, to, cell => IsWalkable(cell, held, resolvedLocks));
    public IEnumerable<CampaignRoute> WarpsAt(GridPoint cell) => Warps.Where(r => Locations[r.From].Equals(cell) || Locations[r.To].Equals(cell));
    public bool CanWarp(string routeId, GridPoint from, CapabilitySet held, ISet<string> resolved, out GridPoint destination)
    {
        destination = from;
        var route = WarpsAt(from).FirstOrDefault(r => r.Id == routeId);
        if (route == null || !route.CanTraverse(held, resolved) || !IsWalkable(from, held, resolved)) return false;
        var target = Locations[Locations[route.From].Equals(from) ? route.To : route.From];
        if (!IsWalkable(target, held, resolved)) return false;
        destination = target;
        return true;
    }
    public bool TryWarp(string routeId, GridPoint from, CapabilitySet held, ISet<string> resolved, out GridPoint destination)
    {
        if (!CanWarp(routeId, from, held, resolved, out destination)) return false;
        if (Warps.First(r => r.Id == routeId).Latches) resolved.Add(routeId);
        return true;
    }
    public IEnumerable<GridPoint> WarpDestinations(GridPoint from, CapabilitySet held, ISet<string> resolved, string? excludedRoute = null)
    {
        foreach (var route in WarpsAt(from))
            if (route.Id != excludedRoute && CanWarp(route.Id, from, held, resolved, out var destination)) yield return destination;
    }
    public bool[,] CreateWalkableLayer(CapabilitySet held, ISet<string>? resolvedLocks = null)
    {
        var result = new bool[Width, Height];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) result[x, y] = IsWalkable(new GridPoint(x, y), held, resolvedLocks);
        return result;
    }
}
