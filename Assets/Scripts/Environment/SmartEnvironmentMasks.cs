using System;
using EternalEnigma.Core.World;

public static class SmartEnvironmentMasks
{
    public const string Mountains = "Smart/Mountains Base";
    public const string Roads = "Smart/Roads";
    public const string Walls = "Smart/Walls";
    public const string Houses = "Smart/Houses";
    public const string Coast = "Ocean/Coastline";
    public const string Summits = "Smart/Mountain Tops Noise";
    public static bool IsDerived(string name) => name == "Smart/Mountains Tier 2" || name == "Smart/Mountains Tier 3" || name == Summits;
    public static bool[,] World(OverworldGrid grid, string name)
    {
        if (name != Mountains && name != Roads && name != Walls && name != Houses && name != Coast) return null;
        var mask = new bool[grid.Width, grid.Height];
        if(name==Coast)
        {
            for(int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++)
                mask[x,y]=OverworldCosmetics.Biome(grid,x,y)==OverworldBiome.Water || OverworldCosmetics.InLayer(grid,OverworldLayers.Water,x,y);
            return mask;
        }
        if (name == Mountains || name == Roads)
            for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
                mask[x,y] = OverworldCosmetics.InLayer(grid, name == Mountains ? OverworldLayers.Mountains : OverworldLayers.Roads, x, y)
                    && !OverworldCosmetics.InLayer(grid, OverworldLayers.Water, x, y);
        else foreach (var town in grid.TownFootprints)
        {
            if (name == Walls) foreach (var p in town.Walls) mask[p.X,p.Y] = true;
            else foreach (var pair in new[] { (-1,2), (1,2), (0,3) }) { var p = town.Cell(pair.Item1,pair.Item2); mask[p.X,p.Y] = true; }
        }
        return mask;
    }
}
