using System.Collections.Generic;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Non-playable scenery using the overworld's biome models and placement budgets.</summary>
internal static class TownSurroundings
{
    internal const int InnerBorder = 4;

    internal static int MarginCells(float cellSize)
    {
        // Reserve enough ground for 32:9 window resizing, with room past the view edges.
        float aspect = Mathf.Max(4f, Camera.main != null ? Camera.main.aspect : (float)Screen.width / Mathf.Max(1, Screen.height));
        return Mathf.Max(InnerBorder, Mathf.CeilToInt((CameraZoom.OverworldSize * aspect + 4f) / cellSize));
    }

    internal static void Build(Transform parent, TownPlan plan, EnvironmentKit kit, TreeModelPicker trees,
        DioramaCatalog diorama, OverworldBiome biome, float size, int seed, int margin, bool paintedGround)
    {
        var root = new GameObject("Town surroundings");
        root.transform.SetParent(parent, false);
        var batch = new EnvironmentBatch(root.transform);
        var chunks = new Dictionary<(int, int), int>();
        int triangles = 0;
        trees = trees != null ? trees : kit.TreeModels;
        for (int y = -margin; y < plan.Height + margin; y++)
        for (int x = -margin; x < plan.Width + margin; x++)
        {
            // Retain the existing enclosure, trees and entrance nearest the playable town.
            if (x >= -InnerBorder && y >= -InnerBorder && x < plan.Width + InnerBorder && y < plan.Height + InnerBorder) continue;
            var position = new Vector3(x + .5f, y + .5f, 0) * size;
            bool approach = y < 0 && Mathf.Abs(x - plan.Exit.X) <= 1;
            if (!paintedGround)
                batch.Add(kit.Mesh("Paving"), approach ? kit.Road : kit.Ground(biome), position + Vector3.forward * .02f, Vector3.one * size);
            if (approach) continue;
            uint hash = OverworldCosmetics.Hash(seed, x, y);
            uint patch = OverworldCosmetics.Hash(seed ^ 13817, Mathf.FloorToInt(x / 4f), Mathf.FloorToInt(y / 4f));
            if (hash % 1000 >= (patch % 4 == 0 ? 160 : 45)) continue;
            var chunk = (Mathf.FloorToInt(x / 32f), Mathf.FloorToInt(y / 32f));
            chunks.TryGetValue(chunk, out int count);
            if (count >= OverworldCosmetics.PropsPerChunk) continue;

            bool tree = (hash >> 12) % 4 == 0;
            string model = tree ? trees?.Pick(biome, OverworldCosmetics.Hash(seed ^ 15401, x, y)) ?? "Tree" :
                biome switch
                {
                    OverworldBiome.Desert => "Cactus", OverworldBiome.Tundra => "SnowRock",
                    OverworldBiome.Marsh or OverworldBiome.Water => "Reeds", OverworldBiome.Volcanic => "Basalt",
                    OverworldBiome.Mountain => "Rock", OverworldBiome.Forest => "Mushrooms", _ => "Flowers"
                };
            if (!tree)
            {
                string cover = DioramaPlacement.GroundCover(biome, hash);
                if (diorama?.Get(cover) != null) model = cover;
            }
            int cost = kit.Triangles(model);
            if (triangles + cost > OverworldCosmetics.TriangleBudget) continue;
            triangles += cost;
            chunks[chunk] = count + 1;
            if (diorama?.Get(model) != null)
                diorama.Add(batch, model, biome, position, (hash >> 16) % 360, DioramaPlacement.Variation(hash));
            else batch.Add(kit.Mesh(model), kit.Material(biome), position, Vector3.one * size * (.8f + (hash >> 8) % 20 / 100f), (hash >> 16) % 360);
        }
        batch.Finish();
    }
}
