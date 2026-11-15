using EternalEnigma.Core.Capabilities;

namespace EternalEnigma.Core.Progression;

/// <summary>Menu relevance only. Acquisition, party membership and saved progression are never removed.</summary>
public static class CapabilityUsage
{
    public static bool HasRemainingUse(CampaignContext context, Capability capability)
    {
        if (capability == Capability.HomeKey)
            return context.HasHomeLesson && !context.State.HomeDoorUnlocked;
        // Sailing checks the capability on ordinary water, even when every route is unlocked.
        if (capability == Capability.Boat) return true;
        bool Uses(Requirement requirement) => requirement.Alternatives.Any(a => a.Contains(capability));
        // Check the whole campaign, including undiscovered and optional routes. Keep every alternative.
        if (context.Campaign.Routes.Any(route => route.ShortcutKind != ShortcutKind.Keyed &&
            route.ShortcutKind != ShortcutKind.FarSide && Uses(route.Requirement) &&
            (NeedsOpening(context, route) || !route.CanTraverse(CapabilitySet.Empty, context.Resolved)))) return true;
        if (context.Campaign.Sources.Any(source => !context.Claimed.Contains(source.Id) && Uses(source.Prerequisites))) return true;
        // Completed encounters can be entered again and still enforce their entry requirements.
        return context.Campaign.Locations.Any(location => location.Encounter != null && Uses(location.Encounter.EntryRequirement));
    }

    public static bool HasRemainingKeyUse(CampaignContext context, string keyId) =>
        context.Campaign.Routes.Any(route => route.KeyId == keyId && NeedsOpening(context, route));

    private static bool NeedsOpening(CampaignContext context, CampaignRoute route) =>
        !context.Opened.Contains(route.Id) && !context.Resolved.Contains(route.Id);
}
