using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;

namespace EternalEnigma.Core.Progression;

/// <summary>One contextual name for an unlock, shared by its reward, journal and gate actions.</summary>
public static class CampaignRewardNames
{
    public static string Name(CampaignContext context, Capability capability)
    {
        var source = context.Campaign.Sources.Where(s => s.Capability == capability)
            .OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
        if (source == null) return capability.DisplayName();
        var site = context.Campaign.Locations.Single(l => l.Id == source.LocationId);
        var town = context.Campaign.Locations.Where(l => l.Kind == LocationKind.Town && l.RegionId == site.RegionId)
            .OrderBy(l => Math.Abs(l.Stage - site.Stage)).ThenBy(l => l.Id, StringComparer.Ordinal).First();
        var skin = context.Campaign.Routes
            .Where(r => LockSkinCatalog.Skins.Any(s => s.Id == r.SkinId && s.Capability == capability))
            .OrderByDescending(r => r.IsProgressionBoundary).ThenByDescending(r => r.Required)
            .ThenBy(r => r.Id, StringComparer.Ordinal).Select(r => r.SkinId).FirstOrDefault();
        string title = capability switch
        {
            Capability.Engineering => skin switch
            {
                "engineering.interaction.2" => "Bridge Engineering",
                "engineering.interaction.3" or "engineering.interaction.5" => "Hydraulic Engineering",
                _ => "Mechanical Engineering"
            },
            Capability.Climb => skin == "climb.area.5" ? "Ruins Climbing" : "Cliff Climbing",
            Capability.Grapple => "Ropemaker's Training",
            Capability.Icewalk => "Ice Crossing Lessons",
            Capability.DeepDive => "Salvage Diving",
            Capability.Breach => "Quarry Demolition",
            Capability.WaystoneStep => "Waystone Pilgrimage",
            Capability.PhaseShift => "Veilwalking Initiation",
            Capability.HazardWard => "Warding Blessing",
            Capability.Boat => "Ferry Charter",
            Capability.Icebreaker => "Icebreaker Charter",
            Capability.Airship => "Airship Charter",
            Capability.Tunneler => "Excavation Grant",
            Capability.Translation => "Interpreter's Training",
            Capability.RoyalAuthority => "Royal Grant",
            Capability.AncientAttunement => "Ancestral Attunement",
            Capability.BeastSpeech => "Beastkeeper's Training",
            Capability.GuildStanding => "Guild Charter",
            Capability.Remedy => "Herbal Medicine",
            Capability.Diplomacy => "Envoy's Writ",
            Capability.SacredRite => "Shrine Blessing",
            _ => capability.DisplayName()
        };
        return context.GetTownDisplayName(town.Id) + " " + title;
    }

    public static string Purpose(Capability capability) => capability switch
    {
        Capability.MillwrightTraining => "Operate mill sluices, waterwheels and water-powered lifts.",
        Capability.ForestersWrit => "Pass woodland wardens and use protected ranger trails.",
        Capability.FerrymansFavor => "Call on private ferrymen for their guarded crossings.",
        Capability.GravediggersCharge => "Enter guarded burial grounds and sealed crypt passages.",
        Capability.SmugglersMark => "Gain passage through hidden docks, cellars and contraband tunnels.",
        Capability.BellkeepersOffice => "Use authorized bell signals to open crossings and summon lifts.",
        Capability.FuneraryRites => "Lay restless spirits to rest and release the roads they haunt.",
        Capability.RoyalSurveyorsMaps => "Trace forgotten roads, buried entrances and surveyed crossings.",
        _ => "Use it at matching overworld obstacles and routes."
    };
}
