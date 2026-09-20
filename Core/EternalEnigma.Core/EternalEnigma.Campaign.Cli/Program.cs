using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

try
{
    int seed = 0, count = 1;
    bool generateGrid = false;
    int width = 256, height = 256;
    string? output = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--help")
        {
            Console.WriteLine("Campaign generator: --seed <int> --count <positive int> --output <directory> [--grid --width <int> --height <int>]\nEvery campaign/grid is validated. JSON exports are diagnostic, not a save format.");
            return 0;
        }
        if (args[i] == "--grid") { generateGrid = true; continue; }
        if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {args[i]}.");
        switch (args[i])
        {
            case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--count": count = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--output": output = args[++i]; break;
            case "--width": width = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
            case "--height": height = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
            default: throw new ArgumentException($"Unknown argument: {args[i]}.");
        }
    }
    if (count < 1 || (long)seed + count - 1 > int.MaxValue) throw new ArgumentException("Seed range must be nonempty and within Int32.");
    if (output != null) Directory.CreateDirectory(output);
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    var watch = Stopwatch.StartNew();
    for (int offset = 0; offset < count; offset++)
    {
        var campaign = CampaignGenerator.Generate(seed + offset);
        string fingerprint = CampaignFingerprint.Compute(campaign);
        Console.WriteLine($"seed={campaign.Seed} version={campaign.GeneratorVersion} capabilities={campaign.Manifest.Count} regions={campaign.Regions.Count} locations={campaign.Locations.Count} sha256={fingerprint}");
        if (output != null)
            File.WriteAllText(Path.Combine(output, $"campaign-{campaign.Seed}.json"), JsonSerializer.Serialize(new { fingerprint, campaign }, options));
        if (generateGrid)
        {
            var grid = OverworldGridGenerator.Generate(campaign, new OverworldGridOptions(width, height));
            Console.WriteLine($"grid={grid.Width}x{grid.Height} layers={grid.Layers.Count} locks={grid.Locks.Count} locations={grid.Locations.Count}");
            if (output != null)
            {
                var layers = grid.Layers.ToDictionary(p => p.Key, p => Enumerable.Range(0, grid.Height)
                    .Select(y => new string(Enumerable.Range(0, grid.Width).Select(x => p.Value[x, y] ? '1' : '0').ToArray())).ToArray());
                File.WriteAllText(Path.Combine(output, $"overworld-{campaign.Seed}.json"), JsonSerializer.Serialize(new
                {
                    version = OverworldGrid.GenerationVersion, grid.CampaignFingerprint, grid.Width, grid.Height,
                    grid.PlayerStart, grid.Locations, grid.Routes, grid.Locks, grid.Warps, grid.RegionBiomes, campaign.ReturnObjectives,
                    shortcuts = campaign.Routes.Where(r => r.ShortcutKind != EternalEnigma.Core.Progression.ShortcutKind.None), layers
                }, options));
                File.WriteAllText(Path.Combine(output, $"overworld-{campaign.Seed}.svg"), GridPreview.Svg(grid, campaign));
            }
        }
    }
    Console.WriteLine($"Validated {count} campaigns{(generateGrid ? " and grids" : "")} in {watch.Elapsed.TotalSeconds:F2}s.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
