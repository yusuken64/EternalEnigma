using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Generation stage 9: gives every capability gate and key a fiction. It runs after topology is final, on its own
/// seed stream, so it can never change which routes exist. Presentation renders what it stores and never chooses.
/// </summary>
internal static class LockNarrator
{
    public const uint Stage = 9;
    public const uint RegionStage = 10;

    /// <summary>Names each region from its theme's pool. Own seed stream, so names never disturb lock selection.</summary>
    public static List<CampaignRegion> NameRegions(int seed, IReadOnlyList<CampaignRegion> regions)
    {
        var random = new SeedStream(seed, RegionStage);
        return regions.Select(r =>
        {
            var pool = LockSkinCatalog.RegionNames(r.Theme);
            return new CampaignRegion(r.Id, r.Theme, r.Tier, r.ProgressionOrder, pool[random.Range(pool.Count)]);
        }).ToList();
    }

    public static List<CampaignRoute> Apply(int seed, IReadOnlyList<CampaignRoute> routes, IReadOnlyList<CampaignLocation> locations,
        IReadOnlyList<CampaignRegion> regions, IReadOnlyList<ActivatedCapability> manifest)
    {
        var random = new SeedStream(seed, Stage);
        var critical = CapabilitySet.From(manifest.Where(c => c.Role == CapabilityRole.Critical).Select(c => c.Id));
        var locationRegion = locations.ToDictionary(l => l.Id, l => l.RegionId, StringComparer.Ordinal);
        var themeOf = regions.ToDictionary(r => r.Id, r => r.Theme, StringComparer.Ordinal);
        string ThemeAt(string locationId) => themeOf[locationRegion[locationId]];

        var usedSkins = new HashSet<string>(StringComparer.Ordinal);
        var usedPlaces = new HashSet<string>(StringComparer.Ordinal);
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);

        T Pick<T>(IReadOnlyList<T> pool, Func<T, string> id, HashSet<string> used)
        {
            // Prefer something not yet seen in this campaign; repeat only when the pool is exhausted.
            var fresh = pool.Where(item => !used.Contains(id(item))).ToArray();
            var choice = (fresh.Length > 0 ? fresh : pool.ToArray())[random.Range(fresh.Length > 0 ? fresh.Length : pool.Count)];
            used.Add(id(choice));
            return choice;
        }
        string Place(string theme) => Pick(LockSkinCatalog.Places(theme), p => p, usedPlaces);
        string KeyName(IReadOnlyList<string> pool) => Pick(pool, k => k, usedKeys);

        var result = new List<CampaignRoute>(routes.Count);
        foreach (var route in routes)
        {
            if (route.ShortcutKind == ShortcutKind.Keyed)
            {
                if (route.IsTownExit)
                {
                    string key = KeyName(LockSkinCatalog.TownGateKeyNames);
                    result.Add(route.WithFiction(LockSkinCatalog.TownGateKeySkin, LockSkinCatalog.TownGateKeyText.Replace("{key}", key), key));
                }
                else if (route.IsStarterExit)
                {
                    string key = KeyName(LockSkinCatalog.TownAreaKeyNames);
                    result.Add(route.WithFiction(LockSkinCatalog.TownAreaKeySkin,
                        LockSkinCatalog.TownAreaKeyText.Replace("{key}", key).Replace("{place}", Place(ThemeAt(route.From))), key));
                }
                else
                {
                    // A later biome's relic wakes a waystone in the hub.
                    var destination = regions.Single(r => r.Id == locationRegion[route.To]);
                    string key = LockSkinCatalog.WaystoneKeyName(destination.Theme);
                    usedKeys.Add(key);
                    result.Add(route.WithFiction(LockSkinCatalog.WaystoneKeySkin,
                        LockSkinCatalog.WaystoneKeyText.Replace("{key}", key).Replace("{place}", Place(ThemeAt(route.From)))
                            .Replace("{where}", destination.DisplayName), key));
                }
            }
            else if (route.ShortcutKind == ShortcutKind.None && !route.Requirement.IsOpen)
            {
                string theme = ThemeAt(route.From);
                var skin = Pick(Candidates(route, theme, critical), s => s.Id, usedSkins);
                result.Add(route.WithFiction(skin.Id, skin.Text.Replace("{place}", Place(theme))));
            }
            else result.Add(route);
        }
        return result;
    }

    /// <summary>
    /// A lock's fiction describes one way through it. Prefer a capability whose natural form matches the route's,
    /// and among those the critical one, so the gate reads as the progression obstacle it is.
    /// </summary>
    private static IReadOnlyList<LockSkin> Candidates(CampaignRoute route, string theme, CapabilitySet critical)
    {
        var options = route.Requirement.Alternatives.SelectMany(a => a.Values).Distinct().ToArray();
        var sameForm = options.Where(c => CampaignGenerator.Form(c) == route.Form).ToArray();
        var ranked = (sameForm.Length > 0 ? sameForm : options).OrderBy(c => critical.Contains(c) ? 0 : 1).ThenBy(c => c);
        foreach (var capability in ranked)
        {
            var pool = LockSkinCatalog.For(capability, route.Form, theme);
            if (pool.Count > 0) return pool;
        }
        foreach (var capability in ranked)
        {
            var pool = LockSkinCatalog.For(capability, theme);
            if (pool.Count > 0) return pool;
        }
        throw new InvalidOperationException($"No lock skin fits {route.Id} ({route.Form}, {theme}).");
    }
}
