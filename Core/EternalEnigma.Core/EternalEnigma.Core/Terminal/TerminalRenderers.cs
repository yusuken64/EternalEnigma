using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Terminal;

public abstract class TerminalMapRenderer
{
    private readonly TerminalFrame frame = new();
    private TerminalViewport viewport;
    private int viewportX, viewportY;
    private bool hasViewport;
    public TerminalFrame Frame => frame;
    public bool TryMapCell(int frameColumn, int frameRow, out GridPoint cell)
    {
        cell = default;
        if (!hasViewport || frameColumn < viewportX || frameRow < viewportY ||
            frameColumn >= viewportX + viewport.Width || frameRow >= viewportY + viewport.Height) return false;
        cell = viewport.At(frameColumn - viewportX, frameRow - viewportY);
        return true;
    }
    public bool TryFrameCell(GridPoint cell, out int column, out int row)
    {
        column = row = 0;
        if (!hasViewport || cell.X < viewport.Left || cell.Y < viewport.Bottom ||
            cell.X >= viewport.Left + viewport.Width || cell.Y >= viewport.Bottom + viewport.Height) return false;
        column = viewportX + cell.X - viewport.Left;
        row = viewportY + viewport.Bottom + viewport.Height - 1 - cell.Y;
        return true;
    }
    public bool Render(TerminalMapSnapshot snapshot, int columns, int rows, GridPoint? focusOverride = null)
    {
        hasViewport = false;
        frame.Begin(columns, rows);
        frame.Box(0, 0, columns, rows, TerminalPalette.Wall);
        WriteLine(2, 0, columns - 4, snapshot.Title, TerminalPalette.White);
        int innerWidth = Math.Max(0, columns - 2);
        int available = Math.Max(0, rows - 2);
        bool hasInfo = snapshot.Target != null || snapshot.Context.Count > 0 || snapshot.Legend.Count > 0;
        int sidebar = hasInfo && columns >= 72 ? Math.Min(28, columns / 3) : 0;
        var context = Wrap(string.Join("\n", snapshot.Context), innerWidth);
        var legend = Wrap(snapshot.Legend.Count == 0 ? "" : "Legend: " + string.Join("  ", snapshot.Legend), innerWidth);
        var logs = Wrap(string.Join("\n", snapshot.Messages), innerWidth);
        // Preserve a map even on a small dock-constrained viewport. Targeting
        // instructions take precedence over history and the static legend.
        int reserved = Math.Max(0, available - Math.Min(4, available));
        int targetRows = snapshot.Target != null && sidebar == 0
            ? Math.Min(reserved, columns < 24 ? Math.Min(3, Math.Max(1, rows / 4)) : Math.Min(8, Math.Max(4, rows / 3))) : 0;
        reserved -= targetRows;
        int contextRows = sidebar == 0 ? Math.Min(context.Count, Math.Min(3, reserved)) : 0;
        reserved -= contextRows;
        int partyRows = Math.Min(snapshot.Party.Count, reserved);
        reserved -= partyRows;
        int logRows = Math.Min(logs.Count, Math.Min(4, reserved));
        reserved -= logRows;
        int legendRows = sidebar == 0 ? Math.Min(legend.Count, reserved) : 0;
        int mapTop = 1, mapHeight = Math.Max(0, available - partyRows - logRows - targetRows - contextRows - legendRows);
        int mapWidth = Math.Max(0, columns - 2);
        mapWidth -= sidebar;
        var focus = focusOverride ?? snapshot.Target?.Cell ?? snapshot.Focus;
        viewport = new TerminalViewport(snapshot.Width, snapshot.Height,
            Math.Max(1, Math.Min(mapWidth, snapshot.Width)), Math.Max(1, Math.Min(mapHeight, snapshot.Height)), focus);
        int padX = Math.Max(0, (mapWidth - viewport.Width) / 2);
        int padY = Math.Max(0, (mapHeight - viewport.Height) / 2);
        viewportX = 1 + padX; viewportY = mapTop + padY;
        hasViewport = mapWidth > 0 && mapHeight > 0;
        for (int row = 0; row < mapHeight; row++)
        for (int col = 0; col < mapWidth; col++)
        {
            if (col < padX || row < padY || col >= padX + viewport.Width || row >= padY + viewport.Height) continue;
            GridPoint point = viewport.At(col - padX, row - padY);
            if (point.X < 0 || point.Y < 0 || point.X >= snapshot.Width || point.Y >= snapshot.Height) continue;
            var visibility = snapshot.VisibilityAt(point);
            if (visibility == TerminalVisibility.Unseen) continue;
            var cell = snapshot.TerrainAt(point);
            frame.Put(col + 1, mapTop + row, cell.Glyph, visibility == TerminalVisibility.Explored ? TerminalPalette.Dim : cell.Color);
        }
        foreach (var actor in snapshot.Actors.OrderBy(a => ActorPriority(a.Kind)))
        {
            var p = actor.Cell;
            if (p.X < 0 || p.Y < 0 || p.X >= snapshot.Width || p.Y >= snapshot.Height ||
                (snapshot.VisibilityAt(p) != TerminalVisibility.Visible && !actor.VisibleInFog)) continue;
            int x = 1 + padX + p.X - viewport.Left, y = mapTop + padY + viewport.Bottom + viewport.Height - 1 - p.Y;
            if (x < 1 || y < mapTop || x >= 1 + mapWidth || y >= mapTop + mapHeight) continue;
            frame.Put(x, y, actor.Glyph, ActorColor(actor.Kind));
        }
        void Mark(IEnumerable<GridPoint> points, char glyph, TerminalColor color)
        {
            foreach (var possible in points)
            {
                if (possible.X < 0 || possible.Y < 0 || possible.X >= snapshot.Width || possible.Y >= snapshot.Height || snapshot.VisibilityAt(possible) == TerminalVisibility.Unseen) continue;
                int px = 1 + padX + possible.X - viewport.Left, py = mapTop + padY + viewport.Bottom + viewport.Height - 1 - possible.Y;
                if (px >= 1 && px < 1 + mapWidth && py >= mapTop && py < mapTop + mapHeight)
                    frame.Put(px, py, glyph, color);
            }
        }
        if (snapshot.Target != null)
        {
            Mark(snapshot.Target.ValidCells, '?', TerminalPalette.Feature);
            Mark(snapshot.Target.Area, '*', TerminalPalette.Selected);
            var direction = snapshot.Target.Direction;
            char line = direction.X == 0 ? '|' : direction.Y == 0 ? '-' : direction.X == direction.Y ? '/' : '\\';
            Mark(snapshot.Target.Path, line, TerminalPalette.Selected);
        }
        if (snapshot.Target?.Cell is GridPoint target && target.X >= 0 && target.Y >= 0 && target.X < snapshot.Width && target.Y < snapshot.Height && snapshot.VisibilityAt(target) != TerminalVisibility.Unseen)
        {
            int x = 1 + padX + target.X - viewport.Left, y = mapTop + padY + viewport.Bottom + viewport.Height - 1 - target.Y;
            if (x >= 1 && x < 1 + mapWidth && y >= mapTop && y < mapTop + mapHeight)
                frame.Put(x, y, 'X', TerminalPalette.Selected);
        }
        int hudY = 1 + mapHeight;
        for (int i = 0; i < partyRows; i++)
        {
            var stat = snapshot.Party[i];
            WriteLine(1, hudY + i, innerWidth, $"{stat.Name} L{stat.Level} HP {stat.Hp}/{stat.MaxHp} SP {stat.Sp}/{stat.MaxSp}", TerminalPalette.Ally);
        }
        int textY = hudY + partyRows;
        WriteLines(1, textY, innerWidth, contextRows, context, TerminalPalette.Ally); textY += contextRows;
        WriteLines(1, textY, innerWidth, logRows, logs, TerminalPalette.White, keepTail: true); textY += logRows;
        WriteLines(1, textY, innerWidth, legendRows, legend, TerminalPalette.Dim); textY += legendRows;
        if (targetRows > 0) WriteLines(1, textY, innerWidth, targetRows, Wrap(snapshot.Target!.Label, innerWidth), TerminalPalette.Selected, keepEnds: true);
        if (sidebar > 0)
        {
            int sideX = columns - sidebar, sideWidth = sidebar - 1;
            for (int y = 1; y < 1 + mapHeight; y++) frame.Put(sideX - 1, y, '|', TerminalPalette.Wall);
            var info = Wrap(string.Join("\n", snapshot.Context), sideWidth);
            if (snapshot.Legend.Count > 0) info.AddRange(Wrap("Legend\n" + string.Join("\n", snapshot.Legend), sideWidth));
            if (snapshot.Target != null)
            {
                var targetLines = Wrap(snapshot.Target.Label, sideWidth);
                int count = Math.Min(mapHeight, targetLines.Count);
                WriteLines(sideX, 1, sideWidth, count, targetLines, TerminalPalette.Selected, keepEnds: true);
                WriteLines(sideX, 1 + count, sideWidth, Math.Max(0, mapHeight - count), info, TerminalPalette.Feature);
            }
            else WriteLines(sideX, 1, sideWidth, mapHeight, info, TerminalPalette.Feature);
        }
        return frame.Commit();
    }
    private void WriteLine(int x, int y, int width, string text, TerminalColor color)
    {
        if (width <= 0) return;
        text = PlainText(text).Replace('\n', ' ');
        if (text.Length > width) text = width <= 3 ? new string('.', width) : text.Substring(0, width - 3) + "...";
        frame.Write(x, y, text, color);
    }
    private void WriteLines(int x, int y, int width, int height, IReadOnlyList<string> lines, TerminalColor color,
        bool keepTail = false, bool keepEnds = false)
    {
        int count = Math.Min(height, lines.Count);
        for (int i = 0; i < count; i++)
        {
            string line = lines[i];
            if (lines.Count > height)
            {
                if (keepTail) line = i == 0 ? "... " + lines[lines.Count - height] : lines[lines.Count - height + i];
                else if (keepEnds && height >= 3 && i >= 1)
                    line = i == 1 ? "..." : lines[lines.Count - height + i];
                else if (i == count - 1) line = width > 3 ? line.Substring(0, Math.Min(line.Length, width - 3)) + "..." : "...";
            }
            WriteLine(x, y + i, width, line, color);
        }
    }
    private static List<string> Wrap(string label, int width)
    {
        var lines = new List<string>();
        if (width < 1 || string.IsNullOrEmpty(label)) return lines;
        foreach (string paragraph in PlainText(label).Split('\n'))
        {
            string remaining = paragraph;
            do
            {
                int count = Math.Min(width, remaining.Length);
                if (count < remaining.Length)
                {
                    int space = remaining.LastIndexOf(' ', count - 1, count);
                    if (space > 0) count = space;
                }
                lines.Add(remaining.Substring(0, count));
                remaining = remaining.Substring(count).TrimStart();
            } while (remaining.Length > 0);
        }
        return lines;
    }
    private static string PlainText(string value) => value.Replace("\r", "").Replace('\t', ' ')
        .Replace('\u00a0', ' ').Replace('\u2013', '-').Replace('\u2014', '-')
        .Replace('\u2018', '\'').Replace('\u2019', '\'').Replace('\u201c', '"').Replace('\u201d', '"')
        .Replace("\u2026", "...").Replace('\u00b7', '|');
    private static TerminalColor ActorColor(TerminalActorKind kind) => kind switch
    {
        TerminalActorKind.Player => TerminalPalette.Player,
        TerminalActorKind.Ally or TerminalActorKind.Summon or TerminalActorKind.Downed => TerminalPalette.Ally,
        TerminalActorKind.Enemy => TerminalPalette.Enemy,
        TerminalActorKind.Item => TerminalPalette.Item,
        _ => TerminalPalette.Feature
    };
    private static int ActorPriority(TerminalActorKind kind) => kind switch
    {
        TerminalActorKind.Item or TerminalActorKind.Prop => 0,
        TerminalActorKind.Enemy or TerminalActorKind.Npc => 1,
        TerminalActorKind.Ally or TerminalActorKind.Summon or TerminalActorKind.Downed => 2,
        TerminalActorKind.Player => 3,
        _ => 0
    };
}

public sealed class DungeonTerminalRenderer : TerminalMapRenderer { }
public sealed class OverworldTerminalRenderer : TerminalMapRenderer { }
public sealed class TownTerminalRenderer : TerminalMapRenderer { }
