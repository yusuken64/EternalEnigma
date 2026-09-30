using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public static class TownRenderer
{
    public const string Legend = "@ you  X exit  D dungeon  0-9 buildings  v vendor  a ally  _ shop floor  + shop wall  H house  T tree  : road  \" park";

    /// <summary>The base legend followed by the visit's service rows, if any.</summary>
    public static string[] LegendFor(TownVisit? visit)
    {
        var services = TownServiceGlyphs.LegendLines(visit);
        // A town with services labels its doors by letter instead of slot number.
        string legend = services.Length == 0 ? Legend : Legend.Replace("0-9 buildings", "letter service door");
        if (visit != null && visit.Plan.Layers.ContainsKey(TownLayers.MainRoads))
            legend = legend.Replace(": road", "= main road  : artery  - alley") + "  p prop";
        return new[] { legend }.Concat(services).ToArray();
    }

    public static string[] Render(TownVisit visit, int width, int height)
    {
        var plan = visit.Plan;

        // Clamp width/height to plan size
        width = Math.Clamp(width, 1, plan.Width);
        height = Math.Clamp(height, 1, plan.Height);

        // Calculate camera position centered on visit.Position
        int left = Math.Clamp(visit.Position.X - width / 2, 0, plan.Width - width);
        int bottom = Math.Clamp(visit.Position.Y - height / 2, 0, plan.Height - height);

        var rows = new string[height];
        for (int row = 0; row < height; row++)
        {
            var cells = new char[width];
            for (int col = 0; col < width; col++)
            {
                var p = new GridPoint(left + col, bottom + height - 1 - row);
                cells[col] = Glyph(visit, p);
            }
            rows[row] = new string(cells);
        }
        return rows;
    }

    private static bool HasDetail(TownPlan plan, string layer, GridPoint p) => plan.Layers.TryGetValue(layer, out var cells) && cells.At(p);

    private static char Glyph(TownVisit visit, GridPoint p)
    {
        // Glyph priority:
        // @ position
        if (p.Equals(visit.Position))
            return '@';

        // X Exit
        if (p.Equals(visit.Plan.Exit))
            return 'X';

        // D DungeonEntrance
        if (p.Equals(visit.Plan.DungeonEntrance))
            return 'D';

        // Service glyph for towns with services (* for the entrance/statue); otherwise 0-9 slot index (index >= 10 → B)
        if (visit.Plan.BuildingIndexAt(p) is int buildingIdx)
        {
            if (visit.SlotServices != null)
                return visit.ServiceAt(p) is { } service ? TownServiceGlyphs.Glyph(service) : TownServiceGlyphs.OtherBuilding;
            return buildingIdx < 10 ? (char)('0' + buildingIdx) : 'B';
        }

        // v a ShopRoom.VendorAnchor
        if (visit.Plan.ShopRooms.Any(room => p.Equals(room.VendorAnchor)))
            return 'v';

        // a ally slot
        if (visit.Plan.AllySlots.Any(ally => p.Equals(ally.Cell)))
            return 'a';

        // _ ShopFloor
        if (visit.Plan.Layers[TownLayers.ShopFloor].At(p))
            return '_';

        // + ShopWalls
        if (visit.Plan.Layers[TownLayers.ShopWalls].At(p))
            return '+';

        // H Houses
        if (visit.Plan.Layers[TownLayers.Houses].At(p))
            return 'H';

        // T Trees
        if (visit.Plan.Layers[TownLayers.Trees].At(p))
            return 'T';

        // = main road, - back alley (detailed towns), : other roads
        if (HasDetail(visit.Plan, TownLayers.MainRoads, p))
            return '=';
        if (HasDetail(visit.Plan, TownLayers.Alleys, p))
            return '-';
        if (visit.Plan.Layers[TownLayers.Roads].At(p))
            return ':';

        // p decoration prop
        if (HasDetail(visit.Plan, TownLayers.Props, p))
            return 'p';

        // " Parks
        if (visit.Plan.Layers[TownLayers.Parks].At(p))
            return '"';

        // . otherwise
        return '.';
    }
}
