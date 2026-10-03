using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

public enum DungeonEnvironmentKind { Interior, Outdoor }

[Serializable]
public sealed class DungeonEncounterVisualSettings
{
    public bool OverrideBiome;
    public OverworldBiome Biome;
    public DungeonEnvironmentKind Environment;
}

[Serializable]
public struct DungeonVisualSelection
{
    public OverworldBiome Biome;
    public DungeonEnvironmentKind Environment;
    public bool UseBiomePresentation;
    public bool IsLegacy => !UseBiomePresentation && Biome == OverworldBiome.Grassland && Environment == DungeonEnvironmentKind.Interior;

    // Never access Context.Location/Position/Grid: these can lazily generate the overworld.
    public static DungeonVisualSelection Resolve(CampaignContext context, DungeonEncounterVisualSettings settings = null)
    {
        var biome = OverworldBiome.Grassland;
        if (context != null)
        {
            string id = string.IsNullOrEmpty(context.State.PendingDungeon) ? context.State.LocationId : context.State.PendingDungeon;
            var location = context.Campaign.Locations.FirstOrDefault(l => l.Id == id);
            if (location != null) biome = OverworldGridGenerator.BiomeForRegion(context.Campaign, location.RegionId);
        }
        return new DungeonVisualSelection { Biome = settings?.OverrideBiome == true ? settings.Biome : biome,
            Environment = settings?.Environment ?? DungeonEnvironmentKind.Interior, UseBiomePresentation = true };
    }

}
