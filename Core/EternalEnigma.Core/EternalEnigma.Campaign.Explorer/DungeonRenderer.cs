using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public static class DungeonRenderer
{
    public const string Legend = "@ you  < start  > stairs  e enemy  $ gold  i item  ^ trap  , carpet  I column  ! torch  # wall";

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

        if (dungeon.Enemies.Any(e => e.Cell.Equals(p))) return 'e';
        if (dungeon.Gold.Any(g => g.Cell.Equals(p))) return '$';
        if (dungeon.Items.Any(i => i.Cell.Equals(p))) return 'i';
        if (dungeon.Traps.Any(t => t.Cell.Equals(p))) return '^';

        if (dungeon.Layers[DungeonLayers.Columns].At(p)) return 'I';
        if (dungeon.Layers[DungeonLayers.Torchlights].At(p)) return '!';

        if (dungeon.Layers[DungeonLayers.Carpet].At(p)) return ',';

        return dungeon.IsWalkable(p) ? '.' : '#';
    }
}
