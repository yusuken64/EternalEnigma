using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Terminal;

public abstract class TerminalMapRenderer
{
    private readonly TerminalFrame frame = new();
    public TerminalFrame Frame => frame;
    public bool Render(TerminalMapSnapshot snapshot, int columns, int rows)
    {
        frame.Begin(columns, rows);
        frame.Box(0, 0, columns, rows, TerminalPalette.Wall);
        frame.Write(2, 0, snapshot.Title, TerminalPalette.White);
        int partyRows = snapshot.Party.Count == 0 ? 0 : Math.Min(2, snapshot.Party.Count);
        int logRows = Math.Min(2, snapshot.Messages.Count);
        int targetRows = snapshot.Target != null && columns < 72 ? 1 : 0;
        int mapTop = 1, mapHeight = Math.Max(0, rows - 2 - partyRows - logRows - targetRows);
        int mapWidth = Math.Max(0, columns - 2);
        int sidebar = columns >= 72 ? Math.Min(20, columns / 4) : 0;
        mapWidth -= sidebar;
        var focus = snapshot.Target?.Cell ?? snapshot.Focus;
        var viewport = new TerminalViewport(snapshot.Width, snapshot.Height,
            Math.Max(1, Math.Min(mapWidth, snapshot.Width)), Math.Max(1, Math.Min(mapHeight, snapshot.Height)), focus);
        int padX = Math.Max(0, (mapWidth - viewport.Width) / 2);
        int padY = Math.Max(0, (mapHeight - viewport.Height) / 2);
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
        if (snapshot.Target != null)
            foreach (var possible in snapshot.Target.Area)
            {
                if (possible.X < 0 || possible.Y < 0 || possible.X >= snapshot.Width || possible.Y >= snapshot.Height || snapshot.VisibilityAt(possible) == TerminalVisibility.Unseen) continue;
                int px = 1 + padX + possible.X - viewport.Left, py = mapTop + padY + viewport.Bottom + viewport.Height - 1 - possible.Y;
                if (px >= 1 && px < 1 + mapWidth && py >= mapTop && py < mapTop + mapHeight)
                    frame.Put(px, py, '?', TerminalPalette.Feature);
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
            frame.Write(1, hudY + i, $"{stat.Name} L{stat.Level} HP {stat.Hp}/{stat.MaxHp} SP {stat.Sp}/{stat.MaxSp}", TerminalPalette.Ally);
        }
        for (int i = 0; i < logRows; i++) frame.Write(1, hudY + partyRows + i, snapshot.Messages[snapshot.Messages.Count - logRows + i], TerminalPalette.White);
        if (targetRows > 0) frame.Write(1, hudY + partyRows + logRows, snapshot.Target?.Label, TerminalPalette.Selected);
        if (sidebar > 0 && snapshot.Target != null) frame.Write(columns - sidebar, 2, snapshot.Target.Label, TerminalPalette.Selected);
        return frame.Commit();
    }
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
