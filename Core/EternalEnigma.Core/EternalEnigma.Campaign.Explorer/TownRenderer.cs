using EternalEnigma.Core.World;

namespace EternalEnigma.ConsoleExplorer;

public static class TownRenderer
{
    public const string Legend = "@ you  X exit  D dungeon  0-9 buildings  v vendor  a ally  _ shop floor  + shop wall  H house  T tree  : road  \" park";

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

        // 0-9 building slot index (index >= 10 → B)
        if (visit.Plan.BuildingIndexAt(p) is int buildingIdx)
            return buildingIdx < 10 ? (char)('0' + buildingIdx) : 'B';

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

        // : Roads
        if (visit.Plan.Layers[TownLayers.Roads].At(p))
            return ':';

        // " Parks
        if (visit.Plan.Layers[TownLayers.Parks].At(p))
            return '"';

        // . otherwise
        return '.';
    }
}
