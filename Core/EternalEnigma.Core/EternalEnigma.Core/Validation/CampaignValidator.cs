using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.Validation;

public sealed class CampaignValidationResult
{
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
    public ExplorationResult? GuaranteedCriticalPath { get; }
    internal CampaignValidationResult(IEnumerable<string> errors, ExplorationResult? path = null)
    { Errors = Array.AsReadOnly(errors.ToArray()); GuaranteedCriticalPath = path; }
}

public static class CampaignValidator
{
    public static CampaignValidationResult Validate(Campaign campaign)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));
        var errors = new List<string>();
        void Check(bool condition, string message) { if (!condition) errors.Add(message); }
        bool Unique(IEnumerable<string> ids)
        { var values = ids.ToArray(); return values.All(id => !string.IsNullOrWhiteSpace(id)) && values.Distinct(StringComparer.Ordinal).Count() == values.Length; }

        Check(Unique(campaign.Locations.Select(l => l.Id)), "locations.ids: Missing or duplicate location IDs.");
        Check(Unique(campaign.Routes.Select(r => r.Id)), "routes.ids: Missing or duplicate route IDs.");
        Check(Unique(campaign.Sources.Select(s => s.Id)), "sources.ids: Missing or duplicate source IDs.");
        Check(Unique(campaign.Regions.Select(r => r.Id)), "regions.ids: Missing or duplicate region IDs.");
        Check(Unique(campaign.Companions.Select(c => c.Id)), "companions.ids: Missing or duplicate companion IDs.");
        Check(campaign.Manifest.Select(c => c.Id).Distinct().Count() == campaign.Manifest.Count, "manifest.ids: Duplicate capability.");
        if (errors.Count > 0) return new CampaignValidationResult(errors);
        var locations = campaign.Locations.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var regions = campaign.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var manifest = campaign.Manifest.ToDictionary(c => c.Id);
        var companions = campaign.Companions.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var active = CapabilitySet.From(manifest.Keys);
        var critical = CapabilitySet.From(campaign.Manifest.Where(c => c.Role == CapabilityRole.Critical).Select(c => c.Id));
        bool ActiveRequirement(Requirement requirement) => requirement.Alternatives.All(active.ContainsAll);
        Check(locations.TryGetValue(campaign.StartLocationId, out var start) && start.Kind == LocationKind.Town && start.Tier == 0,
            "start: Start must be a tier-zero town.");
        Check(locations.TryGetValue(campaign.FinalLocationId, out var final) && final.Kind == LocationKind.FinalDungeon && final.Tier == 4,
            "final: Final location must be a tier-four final dungeon.");
        foreach (var location in campaign.Locations)
            Check(regions.TryGetValue(location.RegionId, out var region) && location.Tier >= 0 && location.Tier < 5,
                $"location.region: {location.Id} has an invalid region/tier.");
        foreach (var route in campaign.Routes)
        {
            Check(locations.ContainsKey(route.From) && locations.ContainsKey(route.To) && route.From != route.To, $"route.endpoint: {route.Id} has invalid endpoints.");
            Check(ActiveRequirement(route.Requirement), $"route.manifest: {route.Id} requires an inactive capability.");
            Check(Enum.IsDefined(typeof(LockForm), route.Form) && (route.Form == LockForm.None) == route.Requirement.IsOpen,
                $"route.form: {route.Id} has an inconsistent lock form.");
            Check(Enum.IsDefined(typeof(ShortcutKind), route.ShortcutKind) &&
                (route.ShortcutKind == ShortcutKind.FarSide ? route.Requirement.IsOpen && route.UnlockingEndpoint == route.To : route.UnlockingEndpoint == null),
                $"shortcut.metadata: {route.Id} has invalid unlocking metadata.");
            Check(route.ShortcutKind == ShortcutKind.Keyed
                ? route.Requirement.IsOpen && !string.IsNullOrWhiteSpace(route.KeyId) && route.KeyLocationId != null && locations.ContainsKey(route.KeyLocationId)
                : route.KeyId == null && route.KeyLocationId == null, $"shortcut.key: {route.Id} has invalid key metadata.");
            if (route.ShortcutKind == ShortcutKind.Capability) Check(!route.Requirement.IsOpen && route.Form == LockForm.Area, $"shortcut.capability: {route.Id} must remain conditional.");
            if (route.Required) Check(route.Requirement.Alternatives.Any(critical.ContainsAll), $"route.critical: {route.Id} has no fully critical solution.");
        }
        foreach (var companion in campaign.Companions)
            Check(active.Contains(companion.Capability) && companion.Capability.Kind() == CapabilityKind.Personal, $"companion.capability: {companion.Id} has an invalid capability.");
        foreach (var source in campaign.Sources)
        {
            Check(locations.ContainsKey(source.LocationId), $"source.location: {source.Id} has an unknown location.");
            Check(active.Contains(source.Capability) && ActiveRequirement(source.Prerequisites), $"source.manifest: {source.Id} uses an inactive capability.");
            if (source.Capability.Kind() == CapabilityKind.Personal)
                Check(source.CompanionId != null && companions.TryGetValue(source.CompanionId, out var companion) && companion.Capability == source.Capability,
                    $"source.companion: {source.Id} needs a matching companion provider.");
            else Check(source.CompanionId == null, $"source.companion: {source.Id} assigns a companion to a permanent capability.");
        }
        if (errors.Count > 0) return new CampaignValidationResult(errors);

        // Stage cuts declare intended progression independently of the existence of a completion path.
        // Every crossing must imply the intended DNF, including long shortcuts crossing several cuts.
        var returnGates = new HashSet<string>(campaign.ReturnObjectives.SelectMany(o => o.GateIds));
        var boundaries = campaign.Routes.Where(r => r.IsProgressionBoundary).ToArray();
        Check(boundaries.Length >= 5 && boundaries.Length <= 7, "boundary.count: Expected 5–7 progression boundaries.");
        Check(start!.Stage == 0 && final!.Stage == boundaries.Length && campaign.Locations.All(l => l.Stage >= 0 && l.Stage <= boundaries.Length),
            "boundary.stage: Invalid start, final or location stage.");
        for (int stage = 0; stage < boundaries.Length; stage++)
        {
            var intended = boundaries.Where(r => locations[r.From].Stage == stage && locations[r.To].Stage == stage + 1 && r.Required).ToArray();
            Check(intended.Length == 1, $"boundary.definition: Stage {stage} needs exactly one declared boundary.");
            if (intended.Length != 1) continue;
            Check(!intended[0].Requirement.IsOpen, $"boundary.open: Stage {stage} must be gated.");
            foreach (var crossing in campaign.Routes.Where(r =>
                r.ShortcutKind != ShortcutKind.Keyed && !returnGates.Contains(r.Id) && Math.Min(locations[r.From].Stage, locations[r.To].Stage) <= stage &&
                Math.Max(locations[r.From].Stage, locations[r.To].Stage) > stage))
                Check(crossing.Requirement.Alternatives.All(intended[0].Requirement.IsSatisfiedBy),
                    $"boundary.bypass: {crossing.Id} bypasses the intended requirement at stage {stage}.");
        }

        var personal = manifest.Keys.Where(c => c.Kind() == CapabilityKind.Personal).ToArray();
        var vehicles = manifest.Keys.Where(c => c.Kind() == CapabilityKind.Vehicle).ToArray();
        var utilities = manifest.Keys.Where(c => c.Kind() == CapabilityKind.Utility).ToArray();
        Check(personal.Length >= 3 && personal.Length <= 4 && personal.Any(c => c.HasAreaForm()), "manifest.personal: Activate 3–4 personal capabilities including an area form.");
        Check(vehicles.Length >= 2 && vehicles.Length <= 3 && vehicles.Any(c => c == Capability.Boat || c == Capability.Icebreaker), "manifest.vehicle: Activate 2–3 vehicles including water traversal.");
        Check(utilities.Length >= 4 && utilities.Length <= 5 && utilities.Contains(Capability.Engineering) && utilities.Any(c => c.IsNarrative()), "manifest.utility: Activate Engineering and a narrative capability among 4–5 utilities.");
        Check(critical.Count >= 5 && critical.Count <= 7 && manifest.Count - critical.Count >= 4 && manifest.Count - critical.Count <= 6,
            "manifest.roles: Expected 5–7 critical and 4–6 exploratory capabilities.");
        Check(critical.Contains(Capability.Engineering), "manifest.engineering: Engineering must be critical.");
        Check(personal.Count(critical.Contains) >= 1 && personal.Count(critical.Contains) <= 2, "manifest.personalRoles: Require 1–2 critical personal capabilities.");
        Check(vehicles.Any(critical.Contains) && (vehicles.Length != 3 || vehicles.Any(c => !critical.Contains(c))), "manifest.vehicleRoles: Invalid critical/exploratory vehicle split.");
        Check(campaign.Regions.Count >= 6 && campaign.Regions.Count <= 8 && campaign.Regions.Select(r => r.Theme).Distinct(StringComparer.Ordinal).Count() == campaign.Regions.Count,
            "regions: Expected 6–8 regions with unique themes.");
        Check(campaign.Locations.Count(l => l.Kind == LocationKind.Town) == 6, "towns: Expected six towns.");
        Check(campaign.Locations.Count(l => l.Kind == LocationKind.StoryDungeon) == 4 && campaign.Locations.Count(l => l.Kind == LocationKind.FinalDungeon) == 1,
            "dungeons.story: Expected four story dungeons and one final dungeon.");
        foreach (var entry in campaign.Manifest)
        {
            Check(entry.Tier >= 0 && entry.Tier < 5 && Enum.IsDefined(typeof(CapabilityRole), entry.Role), $"manifest.tierRole: Invalid tier or role for {entry.Id}.");
            var providers = campaign.Sources.Where(s => s.Capability == entry.Id).ToArray();
            Check(providers.Length >= 2 && providers.Any(s => s.Guaranteed), $"source.providers: {entry.Id} requires two providers and a guaranteed source.");
            Check(providers.All(s => locations[s.LocationId].Tier >= entry.Tier) && providers.Any(s => locations[s.LocationId].Tier == entry.Tier),
                $"source.tier: Providers of {entry.Id} violate tier ordering.");
            var uses = campaign.Routes.Where(r => r.Requirement.Alternatives.Any(a => a.Contains(entry.Id))).ToArray();
            Check(uses.Length >= 2 && uses.Any(r => r.Requirement.Alternatives.Count == 1 && r.Requirement.Alternatives[0].Equals(CapabilitySet.Of(entry.Id))),
                $"capability.payoff: {entry.Id} needs two uses and a sole-solution payoff.");
            if (entry.Role == CapabilityRole.Exploratory)
                Check(uses.Any(r => r.Required), $"capability.alternate: {entry.Id} needs a required-route alternate.");
        }
        foreach (var source in campaign.Sources)
        {
            var site = locations[source.LocationId];
            Check(site.Kind != LocationKind.RepeatableDungeon, $"repeatable.source: {source.Id} is a progression source inside a regenerating dungeon.");
            if (source.Capability.Kind() == CapabilityKind.Vehicle)
                Check(site.Kind == LocationKind.Converter && source.Prerequisites.Alternatives.All(a => a.Contains(Capability.Engineering)),
                    $"converter: {source.Id} must be an Engineering-gated converter.");
        }
        if (errors.Count > 0) return new CampaignValidationResult(errors);

        if (campaign.GeneratorVersion >= 3)
        {
            Check(campaign.ReturnObjectives.Count(o => o.Required) == 1 && campaign.ReturnObjectives.Count(o => !o.Required) == 3,
                "return.count: Expected one required and three optional return objectives.");
            var shortcuts = campaign.Routes.Where(r => r.ShortcutKind != ShortcutKind.None).ToArray();
            if (campaign.GeneratorVersion >= 4)
            {
                Check(campaign.Regions.Count == 6 && campaign.Regions.Select(r => r.ProgressionOrder).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, 6)),
                    "biomes.spine: Expected exactly six ordered biomes A-F.");
                Check(shortcuts.Length == 3 && shortcuts.All(r => r.ShortcutKind == ShortcutKind.Keyed) && shortcuts.Select(r => r.KeyId).Distinct().Count() == 3,
                    "shortcut.count: Expected three distinct later-zone keys.");
                if (campaign.GeneratorVersion >= 5)
                    Check(campaign.Routes.All(r => r.IsWarp == (r.ShortcutKind == ShortcutKind.Keyed)),
                        "warp.kind: Only the three keyed shortcuts may be warps.");
                foreach (int later in new[] { 3, 4, 5 })
                {
                    var matches = shortcuts.Where(r => r.From == "checkpoint-1" && r.To == $"checkpoint-{later}").ToArray();
                    Check(matches.Length == 1, $"shortcut.hub: Missing B-{(char)('A' + later)} connection.");
                    if (matches.Length != 1) continue;
                    var route = matches[0];
                    Check(route.KeyLocationId != null && locations[route.KeyLocationId].RegionId == locations[route.To].RegionId &&
                        locations[route.KeyLocationId].Stage >= locations[route.To].Stage, $"shortcut.keyStage: {route.Id} key must be in its later biome.");
                }
                var normal = CampaignExplorer.Explore(campaign, excludedRoutes: new HashSet<string>(shortcuts.Select(r => r.Id)));
                foreach (var shortcut in shortcuts)
                    Check(normal.ReachableLocations.Contains(shortcut.KeyLocationId!), $"shortcut.keyCycle: {shortcut.Id} key requires a shortcut.");
                foreach (var boundary in boundaries)
                {
                    var cut = CampaignExplorer.Explore(campaign, excludedRoutes: new HashSet<string> { boundary.Id });
                    int stage = locations[boundary.From].Stage;
                    Check(!campaign.Locations.Any(l => l.Kind == LocationKind.Checkpoint && l.Stage > stage && cut.ReachableLocations.Contains(l.Id)),
                        $"shortcut.early: Keys or routes bypass stage {stage} before its normal boundary.");
                }
            }
            foreach (var objective in campaign.ReturnObjectives)
            {
                bool valid = regions.ContainsKey(objective.RegionId) && objective.DestinationIds.Count > 0 &&
                    objective.DestinationIds.All(locations.ContainsKey) && objective.GateIds.Count == objective.DestinationIds.Count &&
                    objective.GateIds.All(id => campaign.Routes.Any(r => r.Id == id));
                Check(valid, "return.references: Invalid destination or gate.");
                if (!valid) continue;
                int firstVisit = campaign.Locations.Where(l => l.RegionId == objective.RegionId && !objective.DestinationIds.Contains(l.Id)).Min(l => l.Stage);
                Check(objective.AcquisitionStage > firstVisit && objective.AcquisitionStage < boundaries.Length,
                    "return.stage: Enabler must be acquired after the initial biome visit and before the final stage.");
                var enablingSources = campaign.Sources.Where(s => s.Capability == objective.EnablingCapability).ToArray();
                Check(enablingSources.Length > 0 && enablingSources.Min(s => locations[s.LocationId].Stage) == objective.AcquisitionStage,
                    "return.acquisitionStage: Enabler providers disagree with the declared acquisition stage.");
                if (objective.Required)
                    Check(enablingSources.All(s => locations[s.LocationId].RegionId != objective.RegionId), "return.enablerRegion: Required return enabler must be acquired in another region.");
                Check(enablingSources.All(s => !objective.DestinationIds.Contains(s.LocationId)),
                    "return.circular: Enabler is inside its own destination.");
                Check(objective.DestinationIds.All(id => locations[id].RegionId == objective.RegionId), "return.region: Destination outside earlier biome.");
                foreach (string id in objective.DestinationIds)
                {
                    var entrances = campaign.Routes.Where(r => r.Other(id) != null).ToArray();
                    Check(entrances.Length == 1 && objective.GateIds.Contains(entrances[0].Id) &&
                        entrances[0].Requirement.Alternatives.Count == 1 && entrances[0].Requirement.Alternatives[0].Equals(CapabilitySet.Of(objective.EnablingCapability)) &&
                        locations[entrances[0].Other(id)!].Stage < objective.AcquisitionStage,
                        "return.gate: Return destination must have one sealed entrance on an earlier path.");
                }
                if (objective.Required)
                {
                    Check(objective.RewardCapability.HasValue && objective.RewardCapability != Capability.Engineering && critical.Contains(objective.RewardCapability.Value),
                        "return.reward: Required return must reward a critical capability other than Engineering.");
                    if (objective.RewardCapability.HasValue)
                    {
                        var providers = campaign.Sources.Where(s => s.Capability == objective.RewardCapability.Value).ToArray();
                        Check(providers.Length == 2 && providers.All(s => objective.DestinationIds.Contains(s.LocationId)), "return.providers: Both providers must require returning.");
                        Check(!CampaignExplorer.Explore(campaign, excludedCapability: objective.RewardCapability).ReachableLocations.Contains(campaign.FinalLocationId),
                            "return.bypass: Completion can omit the return reward.");
                    }
                }
                else Check(objective.DestinationIds.All(id => locations[id].Kind == LocationKind.Secret && !locations[id].Required), "return.optional: Optional returns must be optional secrets.");
            }
            var optional = new HashSet<string>(campaign.ReturnObjectives.Where(o => !o.Required).SelectMany(o => o.DestinationIds));
            Check(CampaignExplorer.Explore(campaign, excludedLocations: optional).ReachableLocations.Contains(campaign.FinalLocationId), "return.optional: Completion depends on optional returns.");
        }
        var full = CampaignExplorer.Explore(campaign);
        var guaranteed = CampaignExplorer.Explore(campaign, guaranteedOnly: true, criticalOnly: true, maxPersonalCapabilities: 1);
        foreach (var location in campaign.Locations)
        {
            Check(full.ReachableLocations.Contains(location.Id), $"reachability.all: {location.Id} is unreachable.");
            if (location.Required || location.Id == campaign.FinalLocationId)
                Check(guaranteed.ReachableLocations.Contains(location.Id), $"reachability.required: {location.Id} is not reachable using guaranteed critical sources with one specialist per excursion.");
        }
        foreach (var entry in campaign.Manifest)
            Check(campaign.Sources.Any(s => s.Capability == entry.Id && full.AcquiredSources.Contains(s.Id)), $"acquisition: {entry.Id} cannot be acquired.");
        for (int tier = 0; tier < 5; tier++)
            Check(campaign.Locations.Any(l => l.Tier == tier && l.Kind == LocationKind.RepeatableDungeon && guaranteed.ReachableLocations.Contains(l.Id)),
                $"repeatable.tier: Tier {tier} lacks a reachable repeatable dungeon.");
        // A capability check must never make one particular companion mandatory.
        foreach (var companion in campaign.Companions)
        {
            var without = CampaignExplorer.Explore(campaign, true, true, 1, companion.Id);
            Check(campaign.Locations.Where(l => l.Required || l.Id == campaign.FinalLocationId).All(l => without.ReachableLocations.Contains(l.Id)),
                $"companion.mandatory: Required content depends on {companion.Id}.");
        }
        return new CampaignValidationResult(errors, guaranteed);
    }
}
