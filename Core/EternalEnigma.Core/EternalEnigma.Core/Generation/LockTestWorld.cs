using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public sealed class LockTestStation
{
    public CampaignRoute Route { get; }
    public string Label { get; }
    public GridPoint Approach { get; }
    public GridPoint FarSide { get; }
    public bool Horizontal { get; }
    public LockTestStation(CampaignRoute route, string label, GridPoint approach, GridPoint farSide, bool horizontal)
    { Route = route; Label = label; Approach = approach; FarSide = farSide; Horizontal = horizontal; }
}

/// <summary>Disposable gallery of every authored lock skin and named gate key, using production gate rules.</summary>
public sealed class LockTestWorld
{
    public Campaign Campaign { get; }
    public OverworldGrid Grid { get; }
    public IReadOnlyList<LockTestStation> Stations { get; }
    public CampaignContext CreateContext() => new(Campaign, Grid, CapabilitySet.From(CapabilityCatalog.All),
        Campaign.Routes.Where(route => route.KeyId != null).Select(route => route.KeyId!));

    public LockTestWorld()
    {
        var themes = new[] { LockSkinCatalog.Highlands, LockSkinCatalog.Marsh, LockSkinCatalog.Coast, LockSkinCatalog.Forest,
            LockSkinCatalog.Desert, LockSkinCatalog.Tundra, LockSkinCatalog.Ruins, LockSkinCatalog.Volcanic };
        var biomes = new[] { OverworldBiome.Grassland, OverworldBiome.Marsh, OverworldBiome.Water, OverworldBiome.Forest,
            OverworldBiome.Desert, OverworldBiome.Tundra, OverworldBiome.Mountain, OverworldBiome.Volcanic };
        var regions = themes.Select((theme, i) => new CampaignRegion("lab-" + theme, theme, 0, i, theme + " test grounds")).ToArray();
        var routes = new List<CampaignRoute>();
        var stations = new List<LockTestStation>();
        var locations = new List<CampaignLocation>();
        var positions = new Dictionary<string, GridPoint>();
        var paths = new Dictionary<string, IReadOnlyList<GridPoint>>();
        var locks = new List<GridLock>();
        var stationThemes = new List<int>();
        const int columns = 8, spacing = 10;

        void Add(string id, string label, Requirement requirement, LockForm form, string skin, string text,
            string theme = LockSkinCatalog.Highlands, string? key = null, bool warp = false, bool farSide = false,
            bool horizontal = false, string? sharedKeyId = null)
        {
            int index = stations.Count, x = 7 + index % columns * spacing, y = 6 + index / columns * spacing;
            var approach = horizontal ? new GridPoint(x - 1, y) : new GridPoint(x, y - 1);
            var exit = horizontal ? new GridPoint(x + 1, y) : new GridPoint(x, y + 1);
            string from = id + "-approach", to = id + "-exit";
            foreach (var endpoint in new[] { from, to }) locations.Add(new CampaignLocation(endpoint, "lab-" + theme, 0, LocationKind.Landmark));
            positions[from] = approach; positions[to] = exit;
            var route = new CampaignRoute(id, from, to, requirement, form,
                shortcutKind: key != null ? ShortcutKind.Keyed : farSide ? ShortcutKind.FarSide : ShortcutKind.None,
                unlockingEndpoint: farSide ? to : null, keyId: key != null ? sharedKeyId ?? "test-key-" + index : null,
                keyLocationId: key != null ? from : null, isWarp: warp, skinId: skin,
                lockText: text.Replace("{place}", "the Test Crossing").Replace("{key}", key ?? "").Replace("{where}", theme + " test grounds"),
                keyName: key);
            routes.Add(route);
            paths[id] = new[] { approach, new GridPoint(x, y), exit };
            if (!warp) locks.Add(new GridLock(id, new[] { new GridPoint(x, y) }));
            stations.Add(new LockTestStation(route, label + (warp ? " / Waystone" : horizontal ? " / East-west path" : " / North-south path"), approach, exit, horizontal));
            stationThemes.Add(Array.IndexOf(themes, theme));
            if (!warp && !horizontal)
                Add(id + "-horizontal", label, requirement, form, skin, text, theme, key, farSide: farSide, horizontal: true, sharedKeyId: route.KeyId);
        }

        foreach (var skin in LockSkinCatalog.Skins)
            Add(skin.Id, skin.Capability.DisplayName() + " / " + skin.Form + " / " + skin.Id,
                new Requirement(CapabilitySet.Of(skin.Capability)), skin.Form, skin.Id, skin.Text,
                skin.Themes.FirstOrDefault() ?? LockSkinCatalog.Highlands);
        foreach (var key in LockSkinCatalog.TownGateKeyNames)
            Add("town-key-" + stations.Count, key, Requirement.Open, LockForm.Interaction,
                LockSkinCatalog.TownGateKeySkin, LockSkinCatalog.TownGateKeyText, key: key);
        foreach (var key in LockSkinCatalog.TownAreaKeyNames)
            Add("road-key-" + stations.Count, key, Requirement.Open, LockForm.Obstacle,
                LockSkinCatalog.TownAreaKeySkin, LockSkinCatalog.TownAreaKeyText, key: key);
        foreach (var theme in themes)
            Add("waystone-key-" + theme, LockSkinCatalog.WaystoneKeyName(theme), Requirement.Open, LockForm.Interaction,
                LockSkinCatalog.WaystoneKeySkin, LockSkinCatalog.WaystoneKeyText, theme, LockSkinCatalog.WaystoneKeyName(theme), warp: true);
        Add("test-compound", "Two capabilities: Climb + Grapple", new Requirement(CapabilitySet.Of(Capability.Climb, Capability.Grapple)),
            LockForm.Obstacle, LockSkinCatalog.Skins.First(s => s.Capability == Capability.Grapple).Id,
            "A sheer wall ends in a broken ledge. The party must secure a line and scale the rock together.");
        Add("test-alternative", "Either capability: Breach or Engineering", new Requirement(CapabilitySet.Of(Capability.Breach), CapabilitySet.Of(Capability.Engineering)),
            LockForm.Obstacle, LockSkinCatalog.Skins.First(s => s.Capability == Capability.Engineering).Id,
            "A heavy barricade is held by a jammed mechanism. Its timbers are cracked.");
        Add("test-far-side", "Shortcut opened from its far side", Requirement.Open, LockForm.Obstacle, "",
            "The gate is barred from the other side.", farSide: true);

        const string start = "lock-test-start";
        locations.Add(new CampaignLocation(start, regions[0].Id, 0, LocationKind.Landmark));
        positions[start] = new GridPoint(3, 3);
        Campaign = new Campaign(42, CampaignGenerator.Version, start, start,
            CapabilityCatalog.All.Select(c => new ActivatedCapability(c, CapabilityRole.Exploratory, 0)), regions,
            locations, routes, Array.Empty<CapabilitySource>(), Array.Empty<CampaignCompanion>());
        int width = columns * spacing + 4, height = (stations.Count + columns - 1) / columns * spacing + 4;
        var masks = typeof(OverworldLayers).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToDictionary(field => (string)field.GetRawConstantValue()!, _ => new bool[width, height]);
        foreach (var biome in biomes) { masks[OverworldLayers.Biome(biome)] = new bool[width, height]; masks[OverworldLayers.Landscape(biome)] = new bool[width, height]; }
        foreach (var region in regions) masks[OverworldLayers.Region(region.Id)] = new bool[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool ground = x > 1 && y > 1 && x < width - 2 && y < height - 2;
            masks[OverworldLayers.Ground][x, y] = masks[OverworldLayers.Walkable][x, y] = ground;
            masks[OverworldLayers.Mountains][x, y] = !ground;
            masks[OverworldLayers.Reserved][x, y] = true;
            int station = Math.Min(stations.Count - 1, Math.Max(0, (y - 2) / spacing) * columns + Math.Max(0, (x - 2) / spacing));
            int theme = stationThemes[station];
            masks[OverworldLayers.Biome(biomes[theme])][x, y] = ground;
            masks[OverworldLayers.Landscape(biomes[theme])][x, y] = true;
            masks[OverworldLayers.Region(regions[theme].Id)][x, y] = ground;
            masks[OverworldLayers.Roads][x, y] = ground && ((y - 3) % spacing == 0 ||
                (stations[station].Horizontal ? (y - 6) % spacing == 0 : (x - 7) % spacing == 0));
        }
        foreach (var gate in locks)
        {
            var cell = gate.Cells[0];
            bool horizontal = stations.First(station => station.Route.Id == gate.RouteId).Horizontal;
            for (int dx = -3; dx <= 3; dx++)
                if (dx != 0)
                {
                    int x = cell.X + (horizontal ? 0 : dx), y = cell.Y + (horizontal ? dx : 0);
                    masks[OverworldLayers.Ground][x, y] = masks[OverworldLayers.Walkable][x, y] = false;
                    masks[OverworldLayers.Mountains][x, y] = true;
                }
            masks[OverworldLayers.Locks][cell.X, cell.Y] = true;
            masks[OverworldLayers.ObstacleLocks][cell.X, cell.Y] = true;
        }
        masks[OverworldLayers.PlayerStart][positions[start].X, positions[start].Y] = true;
        Grid = new OverworldGrid(Campaign, masks.ToDictionary(pair => pair.Key, pair => new GridLayer(pair.Value)),
            positions, paths, locks, regions.Select((region, i) => (region.Id, Biome: biomes[i])).ToDictionary(pair => pair.Id, pair => pair.Biome));
        Stations = stations.AsReadOnly();
    }
}
