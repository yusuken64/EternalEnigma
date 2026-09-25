using EternalEnigma.ConsoleExplorer;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;

int seed = 42;
bool snapshot = false;
string? townId = null;
string? dungeonId = null;
int? floor = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--help" or "-h")
    {
        Console.WriteLine("Campaign Explorer [--seed <integer>] [--snapshot] [--town <id>] [--dungeon <id> [--floor <n>]]\nOn start, choose a view: W: world, T: town, D: dungeon.\nArrows/WASD: move; Q/E/Z/C: diagonals; Enter: enter town/dungeon; R: claim rewards; P: party; T: travel; N: no-clip; Esc: quit (or leave town/dungeon).\nRewards simulate encounter completion. Progress is not saved. --snapshot prints a static preview; --town/--dungeon print a static preview of that location and imply --snapshot.\nNo-clip is a debug flight mode that ignores gates, water and terrain; it stands in for the future airship.");
        return 0;
    }
    if (args[i] == "--snapshot") { snapshot = true; continue; }
    if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[++i], out seed)) continue;
    if (args[i] == "--town" && i + 1 < args.Length) { townId = args[++i]; snapshot = true; continue; }
    if (args[i] == "--dungeon" && i + 1 < args.Length) { dungeonId = args[++i]; snapshot = true; continue; }
    if (args[i] == "--floor" && i + 1 < args.Length && int.TryParse(args[++i], out int f)) { floor = f; continue; }
    Console.Error.WriteLine("Invalid arguments. Use --help.");
    return 1;
}
if (!snapshot && (Console.IsInputRedirected || Console.IsOutputRedirected))
{
    Console.Error.WriteLine("Interactive exploration requires a terminal. Run with --snapshot for a static preview.");
    return 1;
}
try
{
    var campaign = CampaignGenerator.Generate(seed);
    var session = new ExplorerSession(campaign, OverworldGridGenerator.Generate(campaign));
    var renderer = new MapRenderer(session);
    if (townId != null)
    {
        var location = campaign.Locations.FirstOrDefault(l => l.Id == townId && l.Kind == LocationKind.Town);
        if (location == null) { Console.Error.WriteLine("Unknown town: " + townId); return 1; }
        var visit = TownVisit.Create(seed, townId);
        Console.WriteLine($"Campaign seed {seed} | Town {townId}");
        foreach (string row in TownRenderer.Render(visit, 79, 25)) Console.WriteLine(row);
        Console.WriteLine(TownRenderer.Legend);
        return 0;
    }
    if (dungeonId != null)
    {
        var location = campaign.Locations.FirstOrDefault(l => l.Id == dungeonId &&
            l.Kind is LocationKind.StoryDungeon or LocationKind.RepeatableDungeon or LocationKind.FinalDungeon);
        if (location == null) { Console.Error.WriteLine("Unknown dungeon: " + dungeonId); return 1; }
        var run = new DungeonRun(seed, location);
        int targetFloor = floor ?? run.Floor;
        if (targetFloor < run.Floors.Start || targetFloor > run.Floors.End)
        { Console.Error.WriteLine($"Floor {targetFloor} is outside the range {run.Floors.Start}-{run.Floors.End} for {dungeonId}."); return 1; }
        while (run.Floor < targetFloor) run.Descend();
        Console.WriteLine($"Campaign seed {seed} | Dungeon {dungeonId} floor {run.Floor}/{run.Floors.End}");
        foreach (string row in DungeonRenderer.Render(run, 79, 25)) Console.WriteLine(row);
        Console.WriteLine(DungeonRenderer.Legend);
        return 0;
    }
    if (snapshot)
    {
        Console.WriteLine($"Campaign seed {seed} | {session.Position} | {session.Location?.Id}");
        foreach (string row in renderer.Render(79, 25)) Console.WriteLine(row);
        Console.WriteLine("@ you  T town  D dungeon  R repeatable  F final  + gate  : road  # mountain");
        return 0;
    }
    var view = ChooseStartView();
    if (view == StartView.Quit) return 0;
    if (view != StartView.World)
    {
        var target = campaign.Locations.FirstOrDefault(l => l.ParentTownId == null && Matches(l.Kind, view));
        if (target != null) { session.JumpTo(target.Id); session.EnterLocation(); }
    }
    Run(session, renderer);
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
{
    Console.Error.WriteLine("Explorer failed: " + ex.Message);
    return 1;
}

static bool Matches(LocationKind kind, StartView view) => view == StartView.Town
    ? kind == LocationKind.Town
    : kind is LocationKind.StoryDungeon or LocationKind.RepeatableDungeon or LocationKind.FinalDungeon;

static StartView ChooseStartView()
{
    Console.CursorVisible = false;
    Console.Clear();
    Console.WriteLine("ETERNAL ENIGMA");
    Console.WriteLine();
    Console.WriteLine("What would you like to view?");
    Console.WriteLine("  W: World (overworld)");
    Console.WriteLine("  T: Town");
    Console.WriteLine("  D: Dungeon");
    Console.WriteLine();
    Console.WriteLine("Press a key, or Esc to quit.");
    while (true)
    {
        switch (Console.ReadKey(true).Key)
        {
            case ConsoleKey.W: return StartView.World;
            case ConsoleKey.T: return StartView.Town;
            case ConsoleKey.D: return StartView.Dungeon;
            case ConsoleKey.Escape: return StartView.Quit;
        }
    }
}

static void Run(ExplorerSession session, MapRenderer renderer)
{
    string? menu = null;
    int selection = 0;
    bool cursorVisible = !OperatingSystem.IsWindows() || Console.CursorVisible;
    try
    {
        Console.CursorVisible = false;
        Console.Clear();
        int previousWidth = 0, previousHeight = 0;
        while (true)
        {
            int width = Math.Max(1, Console.WindowWidth - 1), height = Math.Max(1, Console.WindowHeight - 1);
            if (width != previousWidth || height != previousHeight) Console.Clear();
            previousWidth = width; previousHeight = height;
            var options = menu == "Party" ? session.Roster.Select(c => $"{(session.IsActive(c.Id) ? "[x]" : "[ ]")} {c.Capability} ({c.Id})").ToArray()
                : menu == "Travel" ? session.VisitedTowns.ToArray() : menu == "Warp" ? session.WarpsHere.Select(session.WarpLabel).ToArray() : Array.Empty<string>();
            selection = Math.Clamp(selection, 0, Math.Max(0, options.Length - 1));
            string header = session.InInterior
                ? $"ETERNAL ENIGMA | seed {session.Campaign.Seed} | {session.InteriorPosition} | {session.Location?.Id} (inside)"
                : session.View == ExplorerView.Town
                    ? $"ETERNAL ENIGMA | seed {session.Campaign.Seed} | Town {session.Town!.TownId}"
                    : session.View == ExplorerView.Dungeon
                        ? $"ETERNAL ENIGMA | seed {session.Campaign.Seed} | Dungeon {session.Dungeon!.Location.Id} floor {session.Dungeon.Floor}/{session.Dungeon.Floors.End}"
                        : $"ETERNAL ENIGMA | seed {session.Campaign.Seed} | {session.Position} | {session.Location?.Id ?? "Overworld"}";
            var lines = new List<string> { header + (session.NoClip ? " | NO-CLIP" : "") };
            int mapHeight = Math.Max(1, height - 10);
            if (menu == null)
            {
                if (session.InInterior) lines.AddRange(renderer.Render(width, mapHeight));
                else if (session.View == ExplorerView.Town) lines.AddRange(TownRenderer.Render(session.Town!, width, mapHeight));
                else if (session.View == ExplorerView.Dungeon) lines.AddRange(DungeonRenderer.Render(session.Dungeon!, width, mapHeight));
                else lines.AddRange(renderer.Render(width, mapHeight));
            }
            else
            {
                lines.Add(menu + " | Up/Down: select | Enter: apply | Esc: close");
                int first = Math.Max(0, selection - Math.Max(1, mapHeight - 2) / 2);
                for (int j = first; j < options.Length && lines.Count < mapHeight + 1; j++) lines.Add((j == selection ? "> " : "  ") + options[j]);
                if (options.Length == 0) lines.Add(menu == "Warp" ? "Stand on a cyan warp gate (O)." : "No options available yet.");
            }
            while (lines.Count < mapHeight + 1) lines.Add("");
            lines.Add("Held: " + (session.Held.Count == 0 ? "none" : session.Held.ToString()));
            lines.Add(session.Message);
            lines.Add(session.RequiredReturn);
            lines.Add("Keys: " + string.Join(", ", session.CollectedKeys));
            lines.Add(session.InInterior || session.View != ExplorerView.Overworld
                ? "Move: arrows/WASD | Diagonal: QEZC/numpad | Esc: return to overworld"
                : "Move: arrows/WASD | Diagonal: QEZC/numpad | Esc: quit");
            if (session.InInterior)
            {
                lines.Add("Enter: interact | I/Esc: leave | N: no-clip (debug)");
                lines.Add(session.Town != null
                    ? "@ you  + door  = shop  # wall  A ally  \" tree  , park  : road  X exit  D dungeon door"
                    : "@ you  < entrance  > stairs  # wall  e enemy  ^ trap  $ gold  i item  I pillar  * torch");
            }
            else if (session.View == ExplorerView.Town)
            {
                lines.Add("Enter: enter dungeon/leave town | R: claim rewards | Esc: leave town | N: no-clip (debug)");
                lines.Add(TownRenderer.Legend);
            }
            else if (session.View == ExplorerView.Dungeon)
            {
                lines.Add("Enter: descend/finish | R: claim rewards | Esc: leave dungeon | N: no-clip (debug)");
                lines.Add(DungeonRenderer.Legend);
            }
            else
            {
                lines.Add("Enter: enter town/dungeon | R: claim rewards | V: warp | P: party (town) | T: fast travel | N: no-clip (debug)");
                lines.Add("@ you  T town  D dungeon  R repeatable  F final  C converter");
                lines.Add("+ gate  O warp  K key  / open  : road  . ground  # rock  ~ water");
            }
            lines.Add("Rewards simulate encounters. No combat or saving.");
            if (height < 12 || width < 40) lines = new List<string> { "Enlarge terminal to at least 41x13.", "Esc: quit" };
            Console.SetCursorPosition(0, 0);
            for (int row = 0; row < height; row++)
            {
                string line = row < lines.Count ? lines[row] : "";
                Console.Write(line.Length > width ? line[..width] : line.PadRight(width));
                if (row + 1 < height) Console.WriteLine();
            }
            var key = Console.ReadKey(true).Key;
            if (menu != null)
            {
                if (key == ConsoleKey.Escape) menu = null;
                else if (key == ConsoleKey.UpArrow) selection = Math.Max(0, selection - 1);
                else if (key == ConsoleKey.DownArrow) selection = Math.Min(Math.Max(0, options.Length - 1), selection + 1);
                else if (key == ConsoleKey.Enter && options.Length > 0)
                {
                    if (menu == "Party") session.ToggleCompanion(session.Roster[selection].Id);
                    else if (menu == "Warp") { session.Warp(session.WarpsHere[selection].Id); menu = null; }
                    else { session.FastTravel(session.VisitedTowns[selection]); menu = null; }
                }
                continue;
            }
            switch (key)
            {
                case ConsoleKey.Escape:
                    if (session.InInterior) session.LeaveLocation();
                    else if (session.View != ExplorerView.Overworld) session.Leave();
                    else return;
                    break;
                case ConsoleKey.P: if (!session.InInterior && session.View == ExplorerView.Overworld) { menu = "Party"; selection = 0; } break;
                case ConsoleKey.T: if (!session.InInterior && session.View == ExplorerView.Overworld) { menu = "Travel"; selection = 0; } break;
                case ConsoleKey.V: if (!session.InInterior && session.View == ExplorerView.Overworld) { menu = "Warp"; selection = 0; } break;
                case ConsoleKey.N: session.ToggleNoClip(); break;
                case ConsoleKey.I: if (session.InInterior) session.LeaveLocation(); else session.EnterLocation(); break;
                case ConsoleKey.R: session.ClaimRewards(); break;
                case ConsoleKey.Enter:
                    if (session.InInterior) session.InteriorInteract();
                    else if (!session.Enter()) session.ClaimRewards();
                    break;
                case ConsoleKey.W: case ConsoleKey.UpArrow: case ConsoleKey.NumPad8: session.Move(0, 1); break;
                case ConsoleKey.S: case ConsoleKey.DownArrow: case ConsoleKey.NumPad2: session.Move(0, -1); break;
                case ConsoleKey.A: case ConsoleKey.LeftArrow: case ConsoleKey.NumPad4: session.Move(-1, 0); break;
                case ConsoleKey.D: case ConsoleKey.RightArrow: case ConsoleKey.NumPad6: session.Move(1, 0); break;
                case ConsoleKey.Q: case ConsoleKey.NumPad7: session.Move(-1, 1); break;
                case ConsoleKey.E: case ConsoleKey.NumPad9: session.Move(1, 1); break;
                case ConsoleKey.Z: case ConsoleKey.NumPad1: session.Move(-1, -1); break;
                case ConsoleKey.C: case ConsoleKey.NumPad3: session.Move(1, -1); break;
            }
        }
    }
    finally { Console.CursorVisible = cursorVisible; Console.Clear(); }
}

enum StartView { World, Town, Dungeon, Quit }
