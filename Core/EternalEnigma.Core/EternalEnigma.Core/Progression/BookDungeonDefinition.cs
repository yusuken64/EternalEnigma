using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Progression;

/// <summary>Optional content derived separately from the immutable campaign graph and its fingerprint.</summary>
public sealed class BookDungeonDefinition
{
    public const int CurrentVersion = 1;
    public string Id => Location.Id;
    public string Title => "The Other Country";
    public string ParentTownId => Location.ParentTownId!;
    public string RequiredCompletionId => "story-0";
    public OverworldBiome Biome { get; }
    public CampaignLocation Location { get; }

    internal BookDungeonDefinition(Campaign campaign)
    {
        var town = campaign.Locations.Single(l => l.Id == campaign.StartLocationId);
        var hostBiome = OverworldGridGenerator.BiomeForRegion(campaign, town.RegionId);
        // Keep the v1 ordering explicit: future biome enum additions must not reshuffle existing books.
        var candidates = new[] { OverworldBiome.Grassland, OverworldBiome.Desert, OverworldBiome.Water,
            OverworldBiome.Mountain, OverworldBiome.Forest, OverworldBiome.Tundra, OverworldBiome.Marsh,
            OverworldBiome.Volcanic }.Where(b => b != hostBiome).ToArray();
        var random = new SeedStream(CampaignContext.LocationSeed(campaign.Seed, "book-0"), 2100);
        Biome = candidates[random.Range(candidates.Length)];
        Location = new CampaignLocation("book-0", town.RegionId, 0, LocationKind.RepeatableDungeon,
            parentTownId: town.Id);
    }
}
