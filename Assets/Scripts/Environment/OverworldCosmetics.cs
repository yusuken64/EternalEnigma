using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

public readonly struct CosmeticPlacement
{
    public readonly int X, Y;
    public readonly string Model;
    public readonly OverworldBiome Biome;
    public readonly float Scale, Rotation, Height;
    public CosmeticPlacement(int x, int y, string model, OverworldBiome biome, float scale, float rotation, float height = 0)
    { X = x; Y = y; Model = model; Biome = biome; Scale = scale; Rotation = rotation; Height = height; }
}

public static class OverworldCosmetics
{
    public const string Layer = "Cosmetic/Biome Props";
    public const int TriangleBudget = 600000;
    public const int PropsPerChunk = 160;

    public static bool InLayer(OverworldGrid grid, string layer, int x, int y) =>
        x >= 0 && y >= 0 && x < grid.Width && y < grid.Height && grid.Layers.TryGetValue(layer, out var mask) && mask[x, y];

    public static OverworldBiome Biome(OverworldGrid grid, int x, int y)
    {
        var playable = grid.BiomeAt(new GridPoint(x, y));
        if (playable.HasValue) return playable.Value;
        foreach (OverworldBiome biome in Enum.GetValues(typeof(OverworldBiome)))
            if (InLayer(grid, OverworldLayers.Landscape(biome), x, y)) return biome;
        return OverworldBiome.Grassland;
    }

    public static bool[,] Protected(OverworldGrid grid)
    {
        var result = new bool[grid.Width, grid.Height];
        void Mark(int x, int y, int radius)
        {
            for (int yy = Math.Max(0, y - radius); yy <= Math.Min(grid.Height - 1, y + radius); yy++)
            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(grid.Width - 1, x + radius); xx++) result[xx, yy] = true;
        }
        for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
            if (InLayer(grid, OverworldLayers.Roads, x, y) || InLayer(grid, OverworldLayers.Bridges, x, y) ||
                InLayer(grid, OverworldLayers.TownFootprints, x, y)) Mark(x, y, 1);
        foreach (var cell in grid.Locations.Values) Mark(cell.X, cell.Y, 3);
        foreach (var gate in grid.Locks) foreach (var cell in gate.Cells) Mark(cell.X, cell.Y, 2);
        Mark(grid.PlayerStart.X, grid.PlayerStart.Y, 3);
        return result;
    }

    // Coordinate hash does not touch gameplay RNG, and is stable across rebuilds and sessions.
    public static uint Hash(int seed, int x, int y)
    {
        unchecked { uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); }
    }

    public static List<CosmeticPlacement> Plan(OverworldGrid grid, EnvironmentKit kit, TreeModelPicker treeModels=null)
    {
        var result = new List<CosmeticPlacement>(); var protect = Protected(grid);
        var diorama=DioramaCatalog.Load();
        treeModels=treeModels!=null?treeModels:kit.TreeModels;
        var chunks = new Dictionary<(int, int), int>(); int triangles = 0;
        for (int y = 1; y < grid.Height - 1; y++) for (int x = 1; x < grid.Width - 1; x++)
        {
            if (protect[x, y] || InLayer(grid, OverworldLayers.Mountains, x, y)) continue;
            var biome = Biome(grid, x, y);
            if (biome == OverworldBiome.Water || InLayer(grid, OverworldLayers.Water, x, y)) continue;
            uint h = Hash(grid.CampaignSeed, x, y);
            bool tree = InLayer(grid, OverworldLayers.Trees, x, y);
            if(tree && diorama!=null && diorama.TreeWalls)continue;
            bool cliff=BiomeDecorations.Directions.Any(d=>InLayer(grid,OverworldLayers.Mountains,x+d.x,y+d.y));
            uint patch=Hash(grid.CampaignSeed^13817,x/4,y/4);
            if (h % 1000 >= (tree ? 180 : cliff?180:patch%4==0?160:45)) continue;
            var chunk = (x / 32, y / 32); chunks.TryGetValue(chunk, out int count);
            if (count >= PropsPerChunk) continue;
            string model =
                biome switch {
                    OverworldBiome.Desert => h % 3 == 0 ? "Rock" : "Cactus",
                    OverworldBiome.Tundra => tree ? "Pine" : "SnowRock",
                    OverworldBiome.Marsh => tree ? "DeadTree" : "Reeds",
                    OverworldBiome.Volcanic => h % 3 == 0 ? "DeadTree" : "Basalt",
                    OverworldBiome.Forest => tree ? "Tree" : "Mushrooms",
                    OverworldBiome.Mountain => tree ? "Pine" : "Rock",
                    _ => tree ? "Tree" : "Flowers"
                };
            if(tree && treeModels!=null) model=treeModels.Pick(biome,Hash(grid.CampaignSeed ^ 15401,x,y));
            if(!tree)
            {
                string replacement=DioramaPlacement.GroundCover(biome,h,cliff);
                if(diorama?.Get(replacement)!=null)model=replacement;
            }
            int cost = kit.Triangles(model); if (triangles + cost > TriangleBudget) continue;
            triangles += cost; chunks[chunk] = count + 1;
            result.Add(new CosmeticPlacement(x, y, model, biome, diorama?.Get(model)!=null?DioramaPlacement.Variation(h):.8f + (h >> 8) % 20 / 100f, (h >> 16) % 360));
        }
        return result;
    }
}

