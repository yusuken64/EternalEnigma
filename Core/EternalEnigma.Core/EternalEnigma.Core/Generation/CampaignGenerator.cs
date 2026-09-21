using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;

namespace EternalEnigma.Core.Generation;

public static class CampaignGenerator
{
    public const int Version = 6;
    public const int TierCount = 5;

    public static Campaign Generate(int seed)
    {
        // Stage identifiers are explicit: adding a new stage must not renumber existing streams.
        var activation = new SeedStream(seed, 0);
        var topology = new SeedStream(seed, 1);
        var identity = new SeedStream(seed, 2);
        var personal = Select(activation, CapabilityKind.Personal, 3 + activation.Range(2), c => c.HasAreaForm());
        var vehicles = Select(activation, CapabilityKind.Vehicle, 2 + activation.Range(2), c => c == Capability.Boat);
        var utility = new List<Capability> { Capability.Engineering };
        utility.Add(activation.Shuffle(CapabilityCatalog.All.Where(c => c.IsNarrative()))[0]);
        utility.AddRange(activation.Shuffle(CapabilityCatalog.All.Where(c => c.Kind() == CapabilityKind.Utility && !utility.Contains(c)))
            .Take(2 + activation.Range(2)));
        var active = personal.Concat(vehicles).Concat(utility).ToArray();
        int minimum = Math.Max(5, active.Length - 6);
        int maximum = Math.Min(7, active.Length - 4);
        int criticalCount = minimum + activation.Range(maximum - minimum + 1);
        var critical = new List<Capability> { Capability.Engineering, personal[0], vehicles[0] };
        foreach (var candidate in activation.Shuffle(active.Where(c => !critical.Contains(c))))
        {
            if (critical.Count == criticalCount) break;
            if (candidate.Kind() == CapabilityKind.Personal && critical.Count(c => c.Kind() == CapabilityKind.Personal) == 2) continue;
            if (candidate.Kind() == CapabilityKind.Vehicle && vehicles.Count == 3 && critical.Count(c => c.Kind() == CapabilityKind.Vehicle) == 2) continue;
            critical.Add(candidate);
        }
        if (critical.Count != criticalCount) throw new InvalidOperationException("Activation constraints could not be satisfied.");
        critical = new[] { Capability.Engineering }.Concat(topology.Shuffle(critical.Where(c => c != Capability.Engineering))).ToList();
        var exploratory = topology.Shuffle(active.Where(c => !critical.Contains(c)));
        var sourceIndex = critical.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        for (int i = 0; i < exploratory.Count; i++) sourceIndex.Add(exploratory[i], i % critical.Count);
        int Tier(int index) => Math.Min(4, index * TierCount / critical.Count);
        var manifest = active.OrderBy(c => c).Select(c => new ActivatedCapability(c,
            critical.Contains(c) ? CapabilityRole.Critical : CapabilityRole.Exploratory, Tier(sourceIndex[c]))).ToArray();
        int[] tierAnchor = Enumerable.Range(0, TierCount).Select(t => Enumerable.Range(0, critical.Count).First(i => Tier(i) == t)).ToArray();

        var themes = identity.Shuffle(new[] { "Highlands", "Marsh", "Coast", "Forest", "Desert", "Tundra", "Ruins", "Volcanic" });
        var regions = Enumerable.Range(0, 6)
            .Select(i => new CampaignRegion($"region-{i}", themes[i], Tier(i), progressionOrder: i)).ToList();
        var locations = new List<CampaignLocation>();
        var routes = new List<CampaignRoute>();
        var sources = new List<CapabilitySource>();
        var companions = new List<CampaignCompanion>();
        void Location(string id, int tier, LocationKind kind, bool required = false, string? region = null, int? stage = null) =>
            locations.Add(new CampaignLocation(id, region ?? $"region-{Math.Min(5, stage ?? tierAnchor[tier])}", tier, kind, required, stage ?? tierAnchor[tier]));
        void Connect(string from, string to, Requirement? requirement = null, LockForm form = LockForm.None, bool required = false, bool boundary = false) =>
            routes.Add(new CampaignRoute($"route-{routes.Count:D3}", from, to, requirement ?? Requirement.Open, form, required, boundary));

        for (int i = 0; i <= critical.Count; i++) Location($"checkpoint-{i}", Tier(i), LocationKind.Checkpoint, true, stage: i);
        for (int tier = 0; tier < TierCount; tier++)
        {
            Location($"town-{tier}", tier, LocationKind.Town, true);
            if (tier == 0)
                routes.Add(new CampaignRoute("starter-exit", "town-0", "checkpoint-0", Requirement.Open,
                    LockForm.Interaction, required: true, shortcutKind: ShortcutKind.Keyed,
                    keyId: "Starting key", keyLocationId: "story-0", keyCondition: KeyAcquisition.DungeonCompletion));
            else Connect($"checkpoint-{tierAnchor[tier]}", $"town-{tier}", required: true);
            Location($"repeatable-{tier}", tier, LocationKind.RepeatableDungeon, true);
            Connect(tier == 0 ? "checkpoint-0" : $"town-{tier}", $"repeatable-{tier}", required: true);
            if (tier < 4)
            {
                Location($"story-{tier}", tier, LocationKind.StoryDungeon, true);
                Connect($"town-{tier}", $"story-{tier}", required: true);
            }
        }
        // If two personal area gates occupy one tier, a roster stop between them prevents a two-specialist spine.
        var personalIndices = Enumerable.Range(0, critical.Count).Where(i => critical[i].Kind() == CapabilityKind.Personal).ToArray();
        int extraTownIndex = personalIndices.Length == 2 && Tier(personalIndices[0]) == Tier(personalIndices[1])
            ? personalIndices[0] + 1 : critical.Count;
        Location("town-5", Tier(extraTownIndex), LocationKind.Town, true, stage: extraTownIndex);
        Connect($"checkpoint-{extraTownIndex}", "town-5", required: true);

        for (int i = 0; i < critical.Count; i++)
        {
            var alternatives = exploratory.Where(c => sourceIndex[c] == i).Take(2).ToList();
            if (alternatives.Count == 0) alternatives.Add(exploratory[0]);
            var requirement = new Requirement(new[] { CapabilitySet.Of(critical[i]) }
                .Concat(alternatives.Select(c => CapabilitySet.Of(c))).ToArray());
            Connect($"checkpoint-{i}", $"checkpoint-{i + 1}", requirement, Form(critical[i]), true, boundary: true);
        }
        Location("final-dungeon", 4, LocationKind.FinalDungeon, true, stage: critical.Count);
        Connect($"checkpoint-{critical.Count}", "final-dungeon", required: true);

        foreach (var entry in manifest)
        {
            var capability = entry.Id;
            int index = sourceIndex[capability];
            for (int provider = 0; provider < 2; provider++)
            {
                string id = $"source-{capability}-{provider}";
                string location;
                location = id + "-site";
                Location(location, entry.Tier, capability.Kind() == CapabilityKind.Vehicle ? LocationKind.Converter : LocationKind.Landmark, stage: index);
                Connect($"checkpoint-{index}", location);
                string? companionId = null;
                if (capability.Kind() == CapabilityKind.Personal)
                {
                    companionId = $"companion-{capability}-{provider}";
                    companions.Add(new CampaignCompanion(companionId, capability));
                }
                sources.Add(new CapabilitySource(id, location, capability, companionId, prerequisites:
                    capability.Kind() == CapabilityKind.Vehicle ? new Requirement(CapabilitySet.Of(Capability.Engineering)) : Requirement.Open));
            }
            // Every capability has a sole-solution payoff, in addition to its spine/alternate use.
            string payoff = $"payoff-{capability}";
            Location(payoff, entry.Tier, LocationKind.Secret, stage: index);
            Connect($"checkpoint-{index}", payoff, new Requirement(CapabilitySet.Of(capability)), Form(capability));
        }
        foreach (var region in regions)
        {
            string landmark = region.Id + "-landmark";
            Location(landmark, region.Tier, LocationKind.Landmark, region: region.Id, stage: region.ProgressionOrder);
            Connect($"checkpoint-{region.ProgressionOrder}", landmark);
        }

        // Physical home and acquisition stage are deliberately independent.
        var returns = new List<ReturnObjective>();
        var reward = critical[2];
        var enabling = critical[1];
        var rewardSites = sources.Where(s => s.Capability == reward).Select(s => s.LocationId).ToArray();
        void Relocate(string id)
        {
            int at = locations.FindIndex(l => l.Id == id); var old = locations[at];
            locations[at] = new CampaignLocation(old.Id, "region-0", old.Tier, old.Kind, old.Required, rewardSites.Contains(id) ? 1 : old.Stage);
        }
        var rewardGates = new List<string>();
        foreach (string site in rewardSites)
        {
            Relocate(site);
            int at = routes.FindIndex(r => r.To == site); var old = routes[at];
            routes[at] = new CampaignRoute(old.Id, "checkpoint-0", site, new Requirement(CapabilitySet.Of(enabling)), LockForm.Interaction);
            rewardGates.Add(old.Id);
        }
        returns.Add(new ReturnObjective("region-0", rewardSites, rewardGates, enabling, 1, true, reward));
        int boundaryIndex = routes.FindIndex(r => r.IsProgressionBoundary && r.From == "checkpoint-2");
        var boundary = routes[boundaryIndex];
        var displaced = boundary.Requirement.Alternatives.Where(a => !a.Contains(reward)).ToArray();
        routes[boundaryIndex] = new CampaignRoute(boundary.Id, boundary.From, boundary.To,
            new Requirement(CapabilitySet.Of(reward)), boundary.Form, true, true);
        // Preserve exploratory required-route uses, without weakening the return boundary.
        foreach (var alternative in displaced)
        {
            int stage = alternative.Values.Select(c => sourceIndex[c]).Max();
            int at = routes.FindIndex(r => r.IsProgressionBoundary && r.From == $"checkpoint-{Math.Max(3, stage)}");
            var old = routes[at];
            routes[at] = new CampaignRoute(old.Id, old.From, old.To, new Requirement(old.Requirement.Alternatives.Concat(new[] { alternative }).ToArray()), old.Form, true, true);
        }
        foreach (var capability in critical.Where(c => sourceIndex[c] >= 1).Take(3))
        {
            string site = $"payoff-{capability}"; Relocate(site);
            int at = routes.FindIndex(r => r.To == site); var old = routes[at];
            routes[at] = new CampaignRoute(old.Id, "checkpoint-0", site, old.Requirement, old.Form);
            returns.Add(new ReturnObjective("region-0", new[] { site }, new[] { old.Id }, capability, capability == reward ? 1 : sourceIndex[capability], false));
        }
        // A-B-C-D-E-F is always the normal route. Later-zone keys add the B hub's missing edges.
        foreach (int later in new[] { 3, 4, 5 })
        {
            string label = ((char)('A' + later)).ToString();
            routes.Add(new CampaignRoute($"shortcut-B-{label}", "checkpoint-1", $"checkpoint-{later}",
                Requirement.Open, LockForm.None, shortcutKind: ShortcutKind.Keyed,
                keyId: $"Biome {label} key", keyLocationId: $"region-{later}-landmark", isWarp: true));
        }

        var campaign = new Campaign(seed, Version, "town-0", "final-dungeon", manifest, regions, locations, routes, sources, companions, returns);
        var validation = CampaignValidator.Validate(campaign);
        if (!validation.IsValid) throw new InvalidOperationException($"Campaign {seed} failed validation:\n{string.Join("\n", validation.Errors)}");
        return campaign;
    }

    private static List<Capability> Select(SeedStream random, CapabilityKind kind, int count, Func<Capability, bool> mandatory)
    {
        var pool = CapabilityCatalog.All.Where(c => c.Kind() == kind).ToArray();
        var chosen = new List<Capability> { random.Shuffle(pool.Where(mandatory))[0] };
        chosen.AddRange(random.Shuffle(pool.Where(c => !chosen.Contains(c))).Take(count - 1));
        return chosen;
    }
    private static LockForm Form(Capability capability) => capability.HasAreaForm() ? LockForm.Area :
        capability.Kind() == CapabilityKind.Utility ? LockForm.Interaction : LockForm.Obstacle;
}
