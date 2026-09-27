using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using UnityEngine;

public sealed class TownBiomeStyle : MonoBehaviour
{
    public EnvironmentKit Kit;
    public bool OverrideBiome;
    public OverworldBiome Biome;
    public OverworldBiome Current
    {
        get
        {
            if (OverrideBiome || !Application.isPlaying) return Biome;
            var common = FindFirstObjectByType<Common>();
            var context = common != null ? common.CampaignContext : null;
            return context?.Location == null ? Biome : OverworldGridGenerator.BiomeForRegion(context.Campaign, context.Location.RegionId);
        }
    }
}
