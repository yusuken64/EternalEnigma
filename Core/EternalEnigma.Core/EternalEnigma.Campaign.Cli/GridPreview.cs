using System.Security;
using System.Text;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Progression;

internal static class GridPreview
{
    internal static string TownSvg(TownPlan town)
    {
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {town.Width * 8} {town.Height * 8}\" width=\"{town.Width * 8}\" height=\"{town.Height * 8}\"><rect width=\"100%\" height=\"100%\" fill=\"#253643\"/>");

        void Draw(string name, string color)
        {
            if (!town.Layers.ContainsKey(name)) return;
            var layer = town.Layers[name];
            svg.Append($"<g fill=\"{color}\">");
            for (int y = 0; y < town.Height; y++)
                for (int x = 0; x < town.Width; x++)
                    if (layer[x, y])
                        svg.Append($"<rect x=\"{x * 8}\" y=\"{(town.Height - y - 1) * 8}\" width=\"8\" height=\"8\"/>");
            svg.Append("</g>");
        }

        // Draw layers with distinct colors
        Draw(TownLayers.Roads, "#d4cba9");
        Draw(TownLayers.Houses, "#8b7355");
        Draw(TownLayers.Trees, "#294b36");
        Draw(TownLayers.Parks, "#75aa58");
        Draw(TownLayers.Roofs, "#c9a87c");
        Draw(TownLayers.Buildings, "#d9b968");
        Draw(TownLayers.ShopFloor, "#e8d5b7");
        Draw(TownLayers.ShopWalls, "#9b7556");
        Draw(TownLayers.Allies, "#39734b");
        Draw(TownLayers.Dungeon, "#ea5d60");
        Draw(TownLayers.Walkable, "#99bb99");

        // Draw markers for special locations
        svg.Append($"<circle cx=\"{(town.PartySpawn.X + 0.5) * 8}\" cy=\"{(town.Height - town.PartySpawn.Y - 0.5) * 8}\" r=\"3\" fill=\"#38ff93\"><title>Party Spawn</title></circle>");
        svg.Append($"<circle cx=\"{(town.Exit.X + 0.5) * 8}\" cy=\"{(town.Height - town.Exit.Y - 0.5) * 8}\" r=\"3\" fill=\"#ff6b6b\"><title>Exit</title></circle>");
        svg.Append($"<circle cx=\"{(town.DungeonEntrance.X + 0.5) * 8}\" cy=\"{(town.Height - town.DungeonEntrance.Y - 0.5) * 8}\" r=\"3\" fill=\"#ea5d60\"><title>Dungeon Entrance</title></circle>");

        // Draw building slots
        foreach (var slot in town.BuildingSlots)
            svg.Append($"<circle cx=\"{(slot.X + 0.5) * 8}\" cy=\"{(town.Height - slot.Y - 0.5) * 8}\" r=\"2\" fill=\"#d9b968\" stroke=\"#ffffff\" stroke-width=\"1\"><title>Building Slot</title></circle>");

        // Draw ally slots
        foreach (var ally in town.AllySlots)
            svg.Append($"<text x=\"{(ally.Cell.X + 0.2) * 8}\" y=\"{(town.Height - ally.Cell.Y - 0.2) * 8}\" fill=\"#ffffff\" font-size=\"6\">A</text>");

        return svg.Append("</svg>").ToString();
    }

    internal static string DungeonSvg(DungeonFloor dungeon)
    {
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {dungeon.Width * 8} {dungeon.Height * 8}\" width=\"{dungeon.Width * 8}\" height=\"{dungeon.Height * 8}\"><rect width=\"100%\" height=\"100%\" fill=\"#253643\"/>");

        void Draw(string name, string color)
        {
            if (!dungeon.Layers.ContainsKey(name)) return;
            var layer = dungeon.Layers[name];
            svg.Append($"<g fill=\"{color}\">");
            for (int y = 0; y < dungeon.Height; y++)
                for (int x = 0; x < dungeon.Width; x++)
                    if (layer[x, y])
                        svg.Append($"<rect x=\"{x * 8}\" y=\"{(dungeon.Height - y - 1) * 8}\" width=\"8\" height=\"8\"/>");
            svg.Append("</g>");
        }

        // Draw layers
        Draw(DungeonLayers.Floor, "#88a66b");
        Draw(DungeonLayers.Dungeon, "#666b60");
        Draw(DungeonLayers.Carpet, "#75aa58");
        Draw(DungeonLayers.Columns, "#8d9097");
        Draw(DungeonLayers.Torchlights, "#cadfe3");

        // Draw markers for start and stairs
        svg.Append($"<circle cx=\"{(dungeon.Start.X + 0.5) * 8}\" cy=\"{(dungeon.Height - dungeon.Start.Y - 0.5) * 8}\" r=\"3\" fill=\"#38ff93\"><title>Start</title></circle>");
        svg.Append($"<circle cx=\"{(dungeon.Stairs.X + 0.5) * 8}\" cy=\"{(dungeon.Height - dungeon.Stairs.Y - 0.5) * 8}\" r=\"3\" fill=\"#ffff00\"><title>Stairs</title></circle>");

        // Draw placements (enemies, gold, items, traps) as small circles with text labels
        foreach (var enemy in dungeon.Enemies)
            svg.Append($"<circle cx=\"{(enemy.Cell.X + 0.5) * 8}\" cy=\"{(dungeon.Height - enemy.Cell.Y - 0.5) * 8}\" r=\"2\" fill=\"#ff5555\"><title>Enemy (roll={enemy.Roll})</title></circle>");

        foreach (var gold in dungeon.Gold)
            svg.Append($"<circle cx=\"{(gold.Cell.X + 0.5) * 8}\" cy=\"{(dungeon.Height - gold.Cell.Y - 0.5) * 8}\" r=\"2\" fill=\"#ffff00\"><title>Gold (roll={gold.Roll})</title></circle>");

        foreach (var item in dungeon.Items)
            svg.Append($"<circle cx=\"{(item.Cell.X + 0.5) * 8}\" cy=\"{(dungeon.Height - item.Cell.Y - 0.5) * 8}\" r=\"2\" fill=\"#00ffff\"><title>Item (roll={item.Roll})</title></circle>");

        foreach (var trap in dungeon.Traps)
            svg.Append($"<circle cx=\"{(trap.Cell.X + 0.5) * 8}\" cy=\"{(dungeon.Height - trap.Cell.Y - 0.5) * 8}\" r=\"2\" fill=\"#ff00ff\"><title>Trap (roll={trap.Roll})</title></circle>");

        return svg.Append("</svg>").ToString();
    }

    internal static string Svg(OverworldGrid grid, Campaign campaign)
    {
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {grid.Width} {grid.Height}\" width=\"1024\" height=\"1024\"><rect width=\"100%\" height=\"100%\" fill=\"#253643\"/>");
        void Draw(string name, string color)
        {
            var layer = grid.Layers[name];
            svg.Append($"<g fill=\"{color}\">");
            for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
                if (layer[x, y]) svg.Append($"<rect x=\"{x}\" y=\"{grid.Height - y - 1}\" width=\"1\" height=\"1\"/>");
            svg.Append("</g>");
        }
        Draw(OverworldLayers.Water, "#245f85"); Draw(OverworldLayers.Ground, "#88a66b");
        string[] colors = { "#75aa58", "#d9b968", "#368ac0", "#8d9097", "#39734b", "#cadfe3", "#6b8770", "#785653" };
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) Draw(OverworldLayers.Landscape(biome), colors[(int)biome]);
        Draw(OverworldLayers.Mountains, "#666b60"); Draw(OverworldLayers.Trees, "#294b36");
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome))) Draw(OverworldLayers.Biome(biome), colors[(int)biome]);
        Draw(OverworldLayers.Roads, "#d4cba9"); Draw(OverworldLayers.AreaLocks, "#599ee3");
        Draw(OverworldLayers.ObstacleLocks, "#e8874d"); Draw(OverworldLayers.InteractionLocks, "#c479db");
        Draw(OverworldLayers.NavigableWater, "#368ac0"); Draw(OverworldLayers.Bridges, "#c49b67");
        Draw(OverworldLayers.Towns, "#ffffff"); Draw(OverworldLayers.StoryDungeons, "#ea5d60");
        Draw(OverworldLayers.RepeatableDungeons, "#ce9e59"); Draw(OverworldLayers.FinalDungeon, "#f02b40");
        Draw(OverworldLayers.PlayerStart, "#38ff93");
        foreach (var location in grid.Locations)
            svg.Append($"<circle cx=\"{location.Value.X}.5\" cy=\"{grid.Height - location.Value.Y - 1}.5\" r=\"1.3\" fill=\"transparent\"><title>{SecurityElement.Escape(location.Key)}</title></circle>");
        foreach (var route in campaign.Routes.Where(r => r.ShortcutKind != ShortcutKind.None))
        {
            if (route.IsWarp || route.IsTownExit) continue;
            var point = grid.Locks.Single(g => g.RouteId == route.Id).Cells[0];
            svg.Append($"<circle cx=\"{point.X + .5}\" cy=\"{grid.Height - point.Y - .5}\" r=\"1.5\" fill=\"{(route.ShortcutKind == ShortcutKind.FarSide || route.ShortcutKind == ShortcutKind.Keyed ? "#00ffff" : "#ffff00")}\"><title>{route.ShortcutKind}: {route.GateHint}; key at {route.KeyLocationId}; open at {route.UnlockingEndpoint}</title></circle>");
        }
        foreach (var endpoint in grid.Warps.SelectMany(r => new[] { r.From, r.To }).Distinct())
        {
            var point = grid.Locations[endpoint];
            string destinations = string.Join("; ", grid.Warps.Where(r => r.Other(endpoint) != null).Select(r => r.Other(endpoint) + " — " + r.GateHint));
            svg.Append($"<circle cx=\"{point.X + .5}\" cy=\"{grid.Height - point.Y - .5}\" r=\"2\" fill=\"#00ffff\"><title>Warp: {SecurityElement.Escape(destinations)}</title></circle>");
        }
        foreach (var region in campaign.Regions)
        {
            var point = grid.Locations[$"checkpoint-{region.ProgressionOrder}"];
            svg.Append($"<text x=\"{point.X}\" y=\"{grid.Height - point.Y - 4}\" fill=\"white\" font-size=\"4\">{region.Label}</text>");
        }
        foreach (var route in campaign.Routes.Where(r => r.KeyLocationId != null))
        {
            var point = grid.Locations[route.KeyLocationId!];
            svg.Append($"<circle cx=\"{point.X + .5}\" cy=\"{grid.Height - point.Y - .5}\" r=\"1.8\" fill=\"#ffff00\"><title>{route.KeyId}</title></circle>");
        }
        foreach (var objective in campaign.ReturnObjectives)
        foreach (string gateId in objective.GateIds)
        {
            var point = grid.Locks.Single(g => g.RouteId == gateId).Cells[0];
            svg.Append($"<circle cx=\"{point.X + .5}\" cy=\"{grid.Height - point.Y - .5}\" r=\"2\" fill=\"none\" stroke=\"{(objective.Required ? "#ff5555" : "#ffffff")}\" stroke-width=\".5\"><title>{(objective.Required ? "Required" : "Optional")} return: {objective.EnablingCapability}</title></circle>");
        }
        return svg.Append("</svg>").ToString();
    }
}
