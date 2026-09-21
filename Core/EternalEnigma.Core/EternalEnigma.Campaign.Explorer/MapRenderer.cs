using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public sealed class MapRenderer
{
    private readonly ExplorerSession session;
    private readonly Dictionary<GridPoint, LocationKind> locations;
    public MapRenderer(ExplorerSession session)
    {
        this.session = session;
        locations = session.Campaign.Locations.Where(l => l.ParentTownId == null).ToDictionary(l => session.Grid.Locations[l.Id], l => l.Kind);
    }

    public string[] Render(int width, int height)
    {
        width = Math.Clamp(width, 1, session.Grid.Width);
        height = Math.Clamp(height, 1, session.Grid.Height);
        int left = Math.Clamp(session.Position.X - width / 2, 0, session.Grid.Width - width);
        int bottom = Math.Clamp(session.Position.Y - height / 2, 0, session.Grid.Height - height);
        var rows = new string[height];
        for (int row = 0; row < height; row++)
        {
            var cells = new char[width];
            for (int col = 0; col < width; col++) cells[col] = Glyph(new GridPoint(left + col, bottom + height - 1 - row));
            rows[row] = new string(cells);
        }
        return rows;
    }

    private char Glyph(GridPoint p)
    {
        if (p.Equals(session.Position)) return '@';
        if (session.Grid.WarpsAt(p).Any()) return 'O';
        var key = session.Campaign.Routes.FirstOrDefault(r => r.KeyLocationId != null && session.Grid.Locations[r.KeyLocationId].Equals(p));
        if (key != null) return session.CollectedKeys.Contains(key.KeyId!) ? 'k' : 'K';
        if (locations.TryGetValue(p, out var kind)) return kind switch
        {
            LocationKind.Town => 'T', LocationKind.StoryDungeon => 'D', LocationKind.RepeatableDungeon => 'R',
            LocationKind.FinalDungeon => 'F', LocationKind.Converter => 'C', LocationKind.Secret => '?',
            LocationKind.Landmark => '*', _ => 'o'
        };
        if (session.Grid.RequiresBoat(p)) return '~';
        if (session.Grid.LockAt(p) is { } gate)
        {
            var route = session.Campaign.Routes.Single(r => r.Id == gate.RouteId);
            return session.IsWalkable(p) ? '/' : route.ShortcutKind == ShortcutKind.Keyed ? 'S' : route.ShortcutKind == ShortcutKind.FarSide ? 'S' : route.ShortcutKind == ShortcutKind.Capability ? 'A' : '+';
        }
        if (session.Grid.IsGround(p)) return session.Grid.Layers[OverworldLayers.Roads][p.X, p.Y] ? ':' : '.';
        if (session.Grid.Layers[OverworldLayers.Water][p.X, p.Y]) return '~';
        if (session.Grid.Layers[OverworldLayers.Trees][p.X, p.Y]) return '"';
        return '#';
    }
}
