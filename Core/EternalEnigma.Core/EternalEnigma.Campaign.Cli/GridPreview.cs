using System.Security;
using System.Text;
using EternalEnigma.Core.World;

internal static class GridPreview
{
    internal static string Svg(OverworldGrid grid)
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
        Draw(OverworldLayers.Roads, "#d4cba9"); Draw(OverworldLayers.AreaLocks, "#599ee3");
        Draw(OverworldLayers.ObstacleLocks, "#e8874d"); Draw(OverworldLayers.InteractionLocks, "#c479db");
        Draw(OverworldLayers.Towns, "#ffffff"); Draw(OverworldLayers.StoryDungeons, "#ea5d60");
        Draw(OverworldLayers.RepeatableDungeons, "#ce9e59"); Draw(OverworldLayers.FinalDungeon, "#f02b40");
        Draw(OverworldLayers.PlayerStart, "#38ff93");
        foreach (var location in grid.Locations)
            svg.Append($"<circle cx=\"{location.Value.X}.5\" cy=\"{grid.Height - location.Value.Y - 1}.5\" r=\"1.3\" fill=\"transparent\"><title>{SecurityElement.Escape(location.Key)}</title></circle>");
        return svg.Append("</svg>").ToString();
    }
}
