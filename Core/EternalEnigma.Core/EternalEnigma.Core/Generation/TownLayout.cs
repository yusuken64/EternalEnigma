using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

/// <summary>
/// Sizes a town for its services and deterministically assigns each service to a building slot.
/// Slots are in raster order, so assignment is shuffled to avoid every town putting the same service in the south.
/// </summary>
public sealed class TownLayout
{
    public TownPlanOptions Options { get; }
    /// <summary>Service for each building slot, by slot index; null for a slot reserved for an authored non-service building or a house.</summary>
    public IReadOnlyList<TownService?> SlotServices { get; }

    private TownLayout(TownPlanOptions options, IReadOnlyList<TownService?> slotServices) { Options = options; SlotServices = slotServices; }

    /// <summary>
    /// Options for a campaign town. A town with no services (hand-built test campaigns) keeps the legacy 15x15 layout.
    /// </summary>
    public static TownPlanOptions OptionsFor(int campaignSeed, CampaignLocation town, int allyCount = 3) =>
        ForLocation(campaignSeed, town, allyCount)?.Options
        ?? new TownPlanOptions(CampaignContext.LocationSeed(campaignSeed, town.Id), allyCount: allyCount);

    /// <summary>The service layout for a campaign town, or null when the town has no services (legacy layout).</summary>
    public static TownLayout? ForLocation(int campaignSeed, CampaignLocation town, int allyCount = 3) =>
        town.Services.Count == 0
            ? null
            : Create(CampaignContext.LocationSeed(campaignSeed, town.Id), town.Services, CampaignContext.AuthoredTownBuildings, allyCount, CampaignContext.ResidentialTownBuildings);

    /// <param name="otherBuildings">Authored buildings that are not services (for example the dungeon entrance and statue).</param>
    /// <param name="residentialBuildings">Extra houses without a service or interior.</param>
    public static TownInteriorSpec InteriorFor(TownServiceKind kind) => kind switch {
        TownServiceKind.Inn => new(TownInteriorKind.Inn), TownServiceKind.Trainer => new(TownInteriorKind.Trainer),
        TownServiceKind.Bakery => new(TownInteriorKind.Shop, TownShopTheme.Bakery),
        TownServiceKind.Consumables => new(TownInteriorKind.Shop, TownShopTheme.Consumables),
        _ => new(TownInteriorKind.Shop, TownShopTheme.Equipment) };

    public static TownLayout Create(int seed, IReadOnlyList<TownService> services, int otherBuildings = 0, int allyCount = 3, int residentialBuildings = 0)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (otherBuildings < 0) throw new ArgumentOutOfRangeException(nameof(otherBuildings));
        if (residentialBuildings < 0) throw new ArgumentOutOfRangeException(nameof(residentialBuildings));
        int count = services.Count + otherBuildings + residentialBuildings;
        var slots = new TownService?[count];
        var order = new SeedStream(seed, 1200).Shuffle(Enumerable.Range(0, count));
        for (int i = 0; i < services.Count; i++) slots[order[i]] = services[i];
        var flags = slots.Select(s => s != null && s.HasInterior).ToArray();
        var interiors = new TownInteriorSpec[count];
        int authored = 0;
        for (int i = 0; i < count; i++)
            interiors[i] = slots[i] is TownService service ? InteriorFor(service.Kind) :
                new TownInteriorSpec(authored++ < otherBuildings ? TownInteriorKind.None : TownInteriorKind.Residential);
        int side = Math.Min(64, Math.Max(52, TownPlanOptions.SizeFor(count)));
        // Centre the entrance corridor so the town grows evenly on both sides.
        return new TownLayout(new TownPlanOptions(seed, side, side, flags, allyCount, spineX: side / 2, detailed: true, interiors: interiors), Array.AsReadOnly(slots));
    }
}
