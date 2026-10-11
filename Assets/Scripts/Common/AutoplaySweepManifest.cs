using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEngine;

// JsonUtility does not serialize the immutable Core options' properties. Persist every
// input explicitly, then create a fresh immutable options object for each generation.
[Serializable]
public sealed class AutoplayFloorCase
{
    public int Index, Seed, Tier, LayoutVersion, Width, Height, EnemyCount, GoldCount, ItemCount, TrapCount, GatheringCount, Difficulty;
    public string Id, Profile;
    public OverworldBiome Biome;
    public DungeonEnvironmentKind Environment;
    public DungeonFloorRole Role;
    public DungeonVariant Variant;
    public bool Throne;
    public DungeonFloorOptions Options() => new(Seed, Width, Height, Throne, EnemyCount, GoldCount, ItemCount,
        TrapCount, GatheringCount, LayoutVersion, Biome, Tier, Role, variant: Variant);
    public DungeonVisualSelection Visuals() => new() { Biome = Biome, Environment = Environment, UseBiomePresentation = true };

    public static AutoplayFloorCase Create(int index, int seed, OverworldBiome biome, DungeonEnvironmentKind environment,
        int tier, DungeonFloorRole role, int version = DungeonLayoutProfile.CurrentVersion, DungeonVariant variant = DungeonVariant.Standard)
    {
        var o = DungeonLayoutProfile.Options(seed, biome, tier, role, version, variant: variant);
        string profile = $"v{version}/{biome}/{environment}/{role}/tier-{tier}/{variant}";
        var result = new AutoplayFloorCase { Index = index, Seed = seed, Profile = profile, Biome = o.Biome,
            Environment = environment, Tier = o.Tier, Role = o.Role, Variant = o.Variant, LayoutVersion = o.LayoutVersion, Throne = o.IsThroneFloor,
            Width = o.Width, Height = o.Height, EnemyCount = o.EnemyCount, GoldCount = o.GoldCount, ItemCount = o.ItemCount,
            TrapCount = o.TrapCount, GatheringCount = o.GatheringCount, Difficulty = 2 + tier * 7 };
        result.Id = Hash128.Compute(JsonUtility.ToJson(result)).ToString();
        return result;
    }
}

[Serializable]
public sealed class AutoplaySweepManifest
{
    public const int CurrentSchema = 2;
    public int Schema = CurrentSchema, StartingSeed;
    public string Id, CreatedUtc, BuildId, ContentId;
    public List<AutoplayFloorCase> Cases = new();

    public static string CurrentBuildId => Application.isEditor ? typeof(Game).Assembly.ManifestModule.ModuleVersionId.ToString() : Application.buildGUID;
    public static string CurrentContentId()
    {
#if UNITY_EDITOR
        // Dependency hashes include imported meshes, textures, prefabs, scenes and settings.
        var paths = UnityEditor.AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
            !UnityEditor.AssetDatabase.IsValidFolder(p)).OrderBy(p => p, StringComparer.Ordinal);
        string assets = string.Join("\n", paths.Select(p => p + ":" + UnityEditor.AssetDatabase.GetAssetDependencyHash(p)));
        string settings = string.Join("\n", Directory.GetFiles("ProjectSettings").OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => p + ":" + File.ReadAllText(p)));
        return Hash128.Compute(assets + settings + Application.unityVersion + Application.version).ToString();
#else
        return Application.buildGUID;
#endif
    }

    public static AutoplaySweepManifest Create(AutoplayOptions options, DungeonThemeCatalog catalog, string build, string content)
    {
        if (!options.Validate(out var error)) throw new ArgumentException(error);
        if (catalog == null || catalog.Themes.Length == 0) throw new InvalidOperationException("Dungeon theme catalog is empty.");
        var profiles = (from theme in catalog.Themes
                        from tier in Enumerable.Range(0, 5)
                        from role in (DungeonFloorRole[])Enum.GetValues(typeof(DungeonFloorRole))
                        select AutoplayFloorCase.Create(0, options.Seed, theme.Biome, theme.Environment, tier, role)).ToList();
        // Legacy layout remains supported by existing saves and starter dungeons.
        foreach (var biome in catalog.Themes.Select(t => t.Biome).Distinct())
        foreach (int tier in Enumerable.Range(0, 5))
        foreach (DungeonFloorRole role in Enum.GetValues(typeof(DungeonFloorRole)))
            profiles.Add(AutoplayFloorCase.Create(0, options.Seed, biome, DungeonEnvironmentKind.Outdoor, tier, role, variant: DungeonVariant.Town));
        foreach (DungeonFloorRole role in Enum.GetValues(typeof(DungeonFloorRole)))
            profiles.Add(AutoplayFloorCase.Create(0, options.Seed, OverworldBiome.Grassland, DungeonEnvironmentKind.Interior, 0, role, 0));
        profiles = profiles.OrderBy(p => p.Profile, StringComparer.Ordinal).ToList();
        if (profiles.Select(p => p.Profile).Distinct().Count() != profiles.Count) throw new InvalidOperationException("Duplicate supported profile.");
        var manifest = new AutoplaySweepManifest { StartingSeed = options.Seed, BuildId = build, ContentId = content, CreatedUtc = DateTime.UtcNow.ToString("O") };
        for (int i = 0; i < options.RunCount; i++)
        {
            var p = profiles[i % profiles.Count];
            // The role is encoded in the profile even for legacy options (whose Core role is Regular).
            var role = (DungeonFloorRole)Enum.Parse(typeof(DungeonFloorRole), p.Profile.Split('/')[3]);
            manifest.Cases.Add(AutoplayFloorCase.Create(i, options.SeedForRun(i), p.Biome, p.Environment, p.Tier, role, p.LayoutVersion, p.Variant));
        }
        manifest.Id = manifest.Identity();
        return manifest;
    }

    private string Identity() => Hash128.Compute(string.Join("\n", Cases.Select(c => JsonUtility.ToJson(c)))).ToString();
    public void Validate()
    {
        if (Schema != CurrentSchema || Cases == null || Cases.Count == 0 || Id != Identity()) throw new InvalidDataException("Invalid or modified floor manifest.");
        for (int i = 0; i < Cases.Count; i++)
        {
            if (Cases[i].Index != i || (long)StartingSeed + i != Cases[i].Seed) throw new InvalidDataException("Manifest case order/seed mismatch.");
            _ = Cases[i].Options();
        }
        if (Cases.Select(c => c.Id).Distinct().Count() != Cases.Count) throw new InvalidDataException("Duplicate manifest cases.");
    }

    public static bool CanResume(AutoplayReport report, AutoplayFloorCase floor, string manifest, string build, string content) =>
        report != null && report.ArtifactsComplete && report.ValidationOnly && !report.EligibleForBalance &&
        report.Options?.Mode == AutoplayMode.FloorLootSweep && report.CaseId == floor.Id && report.ManifestId == manifest &&
        report.BuildId == build && report.ContentId == content && report.Outcome is "Passed" or "Failed";

    public static List<(AutoplayReport Report, string Directory)> ReadAttempts(string directory)
    {
        var attempts = new List<(AutoplayReport Report, string Directory)>();
        foreach (var path in Directory.EnumerateFiles(directory, "report.json", SearchOption.AllDirectories))
        {
            AutoplayReport report;
            try { report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(path)); }
            catch (ArgumentException) { continue; }
            if (report != null) attempts.Add((report, Path.GetDirectoryName(path)));
        }
        return attempts.OrderBy(a => a.Report.StartedUtc, StringComparer.Ordinal).ToList();
    }

    internal static void WriteJson(string path, object value)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
        // Windows indexers/virus scanners can briefly hold the destination without
        // delete sharing. Retain atomic replacement, with a bounded retry for that
        // transient lock; persistent reporting errors still stop the batch.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return;
            }
            catch (IOException) when (attempt < 4 && File.Exists(temporary))
            { System.Threading.Thread.Sleep(20 << attempt); }
        }
    }

    public HashSet<string> FinalizedCases(string directory, string build, string content)
    {
        var result = new HashSet<string>();
        var cases = Cases.ToDictionary(c => c.Id);
        foreach (var attempt in ReadAttempts(directory))
        {
            var report = attempt.Report;
            if (report?.CaseId == null || !cases.TryGetValue(report.CaseId, out var floor) || !CanResume(report, floor, Id, build, content)) continue;
            var folder = attempt.Directory;
            if (new[] { "save.json", "actions.jsonl", "issues.jsonl", "final.png" }.All(f => File.Exists(Path.Combine(folder, f))) &&
                new FileInfo(Path.Combine(folder, "final.png")).Length > 0 &&
                (report.Outcome != "Passed" || new[] { "entrance.png", "gameplay-final.png" }
                    .All(f => File.Exists(Path.Combine(folder, f)) && new FileInfo(Path.Combine(folder, f)).Length > 0))) result.Add(floor.Id);
        }
        return result;
    }
}
