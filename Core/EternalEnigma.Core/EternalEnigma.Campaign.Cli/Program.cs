using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

try
{
    int seed = 0, count = 1;
    bool generateGrid = false;
    string? town = null, dungeon = null;
    int floor = 1;
    int width = 256, height = 256;
    string? output = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--help")
        {
            Console.WriteLine("Campaign generator: --seed <int> --count <positive int> --output <directory> [--grid --width <int> --height <int>] [--town <locationId> | --dungeon <locationId> [--floor <n>]]\n--town and --dungeon require --output. JSON exports are diagnostic, not a save format.");
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
            case "--town": town = args[++i]; break;
            case "--dungeon": dungeon = args[++i]; break;
            case "--floor": floor = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
            default: throw new ArgumentException($"Unknown argument: {args[i]}.");
        }
    }
    if (count < 1 || (long)seed + count - 1 > int.MaxValue) throw new ArgumentException("Seed range must be nonempty and within Int32.");
    if ((town != null || dungeon != null) && output == null) throw new ArgumentException("--town and --dungeon require --output.");
    if (output != null) Directory.CreateDirectory(output);

    // Handle --town and --dungeon export modes
    if (town != null || dungeon != null)
    {
        var campaign = CampaignGenerator.Generate(seed);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());

        if (town != null)
        {
            // Town export
            int townSeed = CampaignContext.LocationSeed(seed, town, 0);
            var townPlan = TownPlanGenerator.Generate(new TownPlanOptions(townSeed));

            // Build JSON object
            var layers = new Dictionary<string, string[]>();
            foreach (var kvp in townPlan.Layers)
            {
                layers[kvp.Key] = Enumerable.Range(0, townPlan.Height)
                    .Select(y => new string(Enumerable.Range(0, townPlan.Width).Select(x => kvp.Value[x, y] ? '1' : '0').ToArray())).ToArray();
            }
            var townJson = new
            {
                seed = townSeed,
                townPlan.Width,
                townPlan.Height,
                layers,
                buildingSlots = townPlan.BuildingSlots,
                allySlots = townPlan.AllySlots,
                shopRooms = townPlan.ShopRooms,
                partySpawn = townPlan.PartySpawn,
                exit = townPlan.Exit,
                dungeonEntrance = townPlan.DungeonEntrance
            };

            File.WriteAllText(Path.Combine(output!, $"town-{townSeed}-{town}.json"), JsonSerializer.Serialize(townJson, jsonOptions));
            File.WriteAllText(Path.Combine(output!, $"town-{townSeed}-{town}.svg"), GridPreview.TownSvg(townPlan));
            Console.WriteLine($"town seed={townSeed} {townPlan.Width}x{townPlan.Height}");
        }
        else if (dungeon != null)
        {
            // Dungeon export
            var location = campaign.Locations.FirstOrDefault(l => l.Id == dungeon);
            if (location == null) throw new ArgumentException($"Unknown location: {dungeon}");

            int dungeonSeed = CampaignContext.LocationSeed(seed, dungeon, floor);
            var floorOptions = CampaignContext.DungeonFloorOptionsFor(seed, dungeon, floor, location.Tier);
            var dungeonFloor = DungeonFloorGenerator.Generate(floorOptions);

            // Build JSON object
            var layers = new Dictionary<string, string[]>();
            foreach (var kvp in dungeonFloor.Layers)
            {
                layers[kvp.Key] = Enumerable.Range(0, dungeonFloor.Height)
                    .Select(y => new string(Enumerable.Range(0, dungeonFloor.Width).Select(x => kvp.Value[x, y] ? '1' : '0').ToArray())).ToArray();
            }
            var dungeonJson = new
            {
                seed = dungeonSeed,
                dungeonFloor.Width,
                dungeonFloor.Height,
                dungeonFloor.IsThroneFloor,
                layers,
                rooms = dungeonFloor.Rooms,
                start = dungeonFloor.Start,
                stairs = dungeonFloor.Stairs,
                enemies = dungeonFloor.Enemies,
                gold = dungeonFloor.Gold,
                items = dungeonFloor.Items,
                traps = dungeonFloor.Traps
            };

            File.WriteAllText(Path.Combine(output!, $"dungeon-{dungeonSeed}-{dungeon}-{floor}.json"), JsonSerializer.Serialize(dungeonJson, jsonOptions));
            File.WriteAllText(Path.Combine(output!, $"dungeon-{dungeonSeed}-{dungeon}-{floor}.svg"), GridPreview.DungeonSvg(dungeonFloor));
            Console.WriteLine($"dungeon seed={dungeonSeed} {dungeonFloor.Width}x{dungeonFloor.Height} floor={floor} throne={dungeonFloor.IsThroneFloor}");
        }

        return 0;
    }

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
                    grid.PlayerStart, grid.Locations, grid.Routes, grid.Locks, grid.Warps, grid.RegionBiomes, grid.TownFootprints, campaign.ReturnObjectives,
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
