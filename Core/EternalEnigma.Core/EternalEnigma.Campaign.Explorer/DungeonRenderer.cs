using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public static class DungeonRenderer
{
    public const string Legend = "@ you  < start  > stairs/gate  / doorway  f furniture  E required enemy  O boulder  o goal  + lit  - dark  T target/trap/tree  R stash  e enemy  $ gold  i item  ^ trap  # wall | R reset puzzle";

    public static string[] Render(DungeonRun run, int width, int height)
    {
        var dungeon = run.Current;

        // Clamp width and height to dungeon dimensions
        width = Math.Clamp(width, 1, dungeon.Width);
        height = Math.Clamp(height, 1, dungeon.Height);

        // Calculate camera position (centered on player position)
        int left = Math.Clamp(run.Position.X - width / 2, 0, dungeon.Width - width);
        int bottom = Math.Clamp(run.Position.Y - height / 2, 0, dungeon.Height - height);

        var rows = new string[height];
        for (int row = 0; row < height; row++)
        {
            var cells = new char[width];
            for (int col = 0; col < width; col++)
            {
                cells[col] = Glyph(dungeon, run, new GridPoint(left + col, bottom + height - 1 - row));
            }
            rows[row] = new string(cells);
        }

        return rows;
    }

    private static char Glyph(DungeonFloor dungeon, DungeonRun run, GridPoint p)
    {
        // Priority order as specified
        if (p.Equals(run.Position)) return '@';
        if (p.Equals(dungeon.Stairs)) return '>';
        if (p.Equals(dungeon.Start)) return '<';
        if (run.Puzzle.Boulders.Values.Contains(p)) return 'O';
        if (run.Puzzle.Enemy?.Equals(p) == true && !run.Puzzle.Complete) return 'E';
        if (dungeon.ObjectiveEnemies.Any(e => run.Objective.RemainingEnemyIds.Contains(e.Id) && !e.Invulnerable && Math.Abs(e.Position.X - p.X) <= e.Footprint / 2 && Math.Abs(e.Position.Y - p.Y) <= e.Footprint / 2)) return 'E';
        if (dungeon.RecoveryStashes.Any(s => s.Position.Equals(p))) return 'R';
        var puzzle = dungeon.PuzzleObjects.FirstOrDefault(o => o.Position.Equals(p) && o.Kind != PuzzleObjectKind.Boulder);
        if (puzzle != null) return puzzle.Kind switch {
            PuzzleObjectKind.Goal => 'o', PuzzleObjectKind.Light => run.Puzzle.Lights[puzzle.Id] ? '+' : '-',
            PuzzleObjectKind.LightTarget => puzzle.TargetState ? '1' : '0', PuzzleObjectKind.BasicSupplies => 'i', _ => 'T' };

        if (dungeon.Enemies.Any(e => e.Cell.Equals(p))) return 'e';
        if (dungeon.Gold.Any(g => g.Cell.Equals(p))) return '$';
        if (dungeon.Items.Any(i => i.Cell.Equals(p))) return 'i';
        if (dungeon.Traps.Any(t => t.Cell.Equals(p))) return '^';
        var prop=dungeon.Scenery.FirstOrDefault(s=>s.Cell.Equals(p));
        if(prop!=null) return prop.Kind==DungeonSceneryKind.Container?'C':prop.Kind==DungeonSceneryKind.Destructible?'X':'^';

        if (dungeon.TownPlan is TownPlan town)
        {
            if (town.IsDoorway(p)) return '/';
            if (town.Layers[TownLayers.Furniture].At(p)) return 'f';
            if (town.Layers[TownLayers.Trees].At(p)) return 'T';
            if (!dungeon.IsWalkable(p)) return '#';
            if (town.Layers[TownLayers.ShopFloor].At(p)) return '_';
            if (town.Layers[TownLayers.Roads].At(p)) return ':';
        }

        if (dungeon.Layers[DungeonLayers.Columns].At(p)) return 'I';
        if (dungeon.Layers[DungeonLayers.Torchlights].At(p)) return '!';

        if (dungeon.Layers[DungeonLayers.Carpet].At(p)) return ',';

        return dungeon.IsWalkable(p) ? '.' : '#';
    }
}
