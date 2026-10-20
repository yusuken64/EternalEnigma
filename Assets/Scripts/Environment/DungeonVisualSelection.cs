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
        bool book = false;
        if (context != null)
        {
            string id = string.IsNullOrEmpty(context.State.PendingDungeon) ? context.State.LocationId : context.State.PendingDungeon;
            book = id == context.BookDungeon.Id;
            var location = context.ResolveDungeonLocation(id) ?? context.Campaign.Locations.FirstOrDefault(l => l.Id == id);
            if (book) biome = context.BookDungeon.Biome;
            else if (location != null) biome = OverworldGridGenerator.BiomeForRegion(context.Campaign, location.RegionId);
        }
        return new DungeonVisualSelection { Biome = !book && settings?.OverrideBiome == true ? settings.Biome : biome,
            Environment = book ? DungeonEnvironmentKind.Outdoor : settings?.Environment ?? DungeonEnvironmentKind.Interior, UseBiomePresentation = true };
    }

}
