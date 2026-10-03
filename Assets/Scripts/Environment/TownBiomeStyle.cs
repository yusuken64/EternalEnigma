using EternalEnigma.Core.World;
using EternalEnigma.Core.Generation;
using UnityEngine;

[ExecuteAlways]
public sealed class TownBiomeStyle : MonoBehaviour
{
    private void OnEnable(){var creator=GetComponent<TWC.TileWorldCreator>();if(creator!=null)creator.OnBuildLayersComplete+=BiomeDecorations.Town;}
    private void OnDisable(){var creator=GetComponent<TWC.TileWorldCreator>();if(creator!=null)creator.OnBuildLayersComplete-=BiomeDecorations.Town;}
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
