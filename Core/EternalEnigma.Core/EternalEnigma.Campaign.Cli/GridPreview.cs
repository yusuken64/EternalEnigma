using System.Security;
using System.Text;
using EternalEnigma.Core.World;
using EternalEnigma.Core.Progression;

internal static class GridPreview
{
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
