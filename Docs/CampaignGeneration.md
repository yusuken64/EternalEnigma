# Core campaign generation

`CampaignGenerator.Generate(int seed)` produces an immutable logical campaign and
validates it before returning. Generation version 1 uses explicit seeded streams
for activation, topology and identity. It does not use Unity, global random state,
time, hash-table iteration order or runtime-dependent `System.Random` sequences.

```csharp
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;

var campaign = CampaignGenerator.Generate(42);
var fingerprint = CampaignFingerprint.Compute(campaign);
var session = new CampaignSession(campaign);
// Render campaign.Locations/Routes; use session methods for logical interactions.
```

Rebuild/import the DLL using **Tools > Eternal Enigma > Core > Build and Import
DLL** after changing core code. The current Unity hub is not automatically replaced
by this campaign; this is the core model and traversal API for that integration.

## Generated content

| Component | Version 1 behavior |
|---|---|
| Manifest | Closed 21-capability vocabulary; activates 3–4 personal, 2–3 vehicle and 4–5 utility capabilities, with required area/water/narrative coverage. |
| Roles | 5–7 critical and 4–6 exploratory, partitioning the active set; Engineering critical, 1–2 personal critical, at least one vehicle critical. Three active vehicles always include an exploratory one. |
| Progression | Five tiers, with 5–7 gated stages along a spine. Every boundary includes a critical solution and at least one exploratory alternate. |
| Regions | 6–8 semantic regions, unique themes, and one designated region landmark each. No coordinates or physical adjacency are generated yet. |
| Towns | Six roster/fast-travel locations. A town separates same-tier personal gates when needed to preserve the one-specialist required route. |
| Dungeons | Four story locations, a final dungeon, and one source-free repeatable location in every tier. These are dungeon interfaces, not floor layouts. |
| Sources | Two guaranteed providers per capability, respecting source tiers. Personal abilities have two distinct companion providers; utility/vehicle abilities latch permanently. |
| Payoffs | Every active capability is used by at least two locks and has a sole-solution optional payoff. Exploratory capabilities also appear as required-route alternates. |
| Converters | Vehicle sources are converter sites requiring Engineering. Claiming a reward consumes no permanent capability. Component items/recipes are not implemented yet. |

The spine topology is deliberately bounded. Seeds vary capabilities, roles, their
order, alternatives, source placement, region count and themes. This does not yet
implement arbitrary region topology, geographical embedding or the full spec's
pacing/content distributions. Specialist providers number 6–8; a complete authored
eight-person combat roster is separate from this capability-provider model.

## Runtime rules

`CampaignSession` keeps mutable progression separate from the immutable campaign:

- `TryClaimSource(id)` requires the source's location and prerequisites. The host
  must call it **after** completing the encounter/quest; this layer does not fight
  a boss or verify quest completion. A source can be claimed only once.
- Personal rewards recruit to the permanent roster, not directly into the active
  party. `TrySetParty(ids)` works only in towns, allows up to three recruited
  companions alongside the implicit protagonist, and rejects duplicates.
- `TryMove(routeId)` traverses a connected bidirectional route using held
  capabilities. Obstacle/interaction locks stay resolved. Area gates always check
  current capabilities. A new recruit cannot become active in the field.
- `TryFastTravel(townId)` reaches visited towns only, from any location.
- `ReturnAfterDefeat()` returns to the starting town and preserves all campaign
  progression. Combat gold/consumable penalties belong to the host.
- `IsAtFinalDungeon` means the final dungeon is **reachable and currently entered**;
  it is not a claim that its boss has been defeated.

Public collections are read-only snapshots. Persistent utility and vehicle
capabilities never depend on the currently active companions. This is an in-memory
session model; save serialization/migration and existing Unity save integration
remain separate work.

## Validation and its limits

`CampaignValidator.Validate` returns actionable errors and a guaranteed-critical
exploration report. It checks IDs/references, manifest composition, role counts,
source tiers and providers, converter requirements, repeatable-source exclusion,
payoffs, required destinations and companion independence.

Declared progression boundaries define cuts between stages. Every route crossing
a cut must imply its intended DNF requirement, including shortcuts across several
cuts. The boundary route itself supplies the intended alternatives, making the
effective graph requirement equivalent. A free bypass is rejected even though it
makes completion easier. This checks the **logical graph**, not physical terrain.

Exploration repeats legal excursions from visited towns, considering available
personal-capability subsets without switching party mid-route. It shares the
runtime DNF and route-legality rules. It checks all locations with up to three
specialists, then required locations with guaranteed critical sources only and
at most one specialist per excursion. It repeats the required-content check with
each companion excluded. Tests separately traverse generated worlds through the
actual `CampaignSession` API.

The monotone closure is valid for this restricted graph model: all routes are
bidirectional, acquired progression and resolved locks never disappear, parties
change only in towns, and visited towns permit unconditional fast travel. Source
completion is assumed possible. If future mechanics introduce one-way travel,
consumed prerequisites, inaccessible roster changes or mutually exclusive rewards,
this explorer must be revised before claiming equivalent guarantees.

Acquisition witnesses record the source, starting town, active personal
capabilities and a route found during exploration. They are diagnostics, not a
globally shortest spoiler itinerary or an independently replayable action log.

The separate [overworld grid generator](OverworldGrid.md) now realizes this graph
as a walkable grid and validates graph/tile connectivity under sealing corner rules.

Not validated by campaign generation: combat victory, dungeon floors/exits, deterministic dungeon
visits, full geographical perimeters, docking, skins,
visibility, rumours, travel-time budgets, acquisition pacing or physical towns.
The output therefore establishes logical campaign completion under the stated
assumptions, not all 64 v5 guarantees.

## Reproducibility and checks

`CampaignFingerprint` hashes all logical fields in canonical order. The test suite
pins seed 42 for generation version 1. An intentional generation change requires
a version and fixture update; a shared seed should always include that version.
CLI JSON contains both versioned campaign content and its fingerprint, but is not
a supported restore format.

From `Core/`:

```powershell
dotnet test EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --output ../Temp/CampaignPreview
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed -500 --count 1000
```

The tests cover signed/boundary seeds, a 128-seed sweep, culture/order stability,
AND/OR requirements, circular sources, invalid references, missing final routes,
ungated bypasses, repeatable-dungeon rewards, field recruitment, composed area
gates, permanent resolutions, converters, defeat and actual session traversal.
Unity's EditMode `EternalEnigma.Tests.CoreIntegration.CampaignGenerationTests`
also checks the imported DLL's seed-42 fingerprint against the headless golden
value and starts a session using that DLL.

The `Core checks` CI workflow builds all three .NET projects, runs the core tests,
and validates campaign/grid pairs for seeds -500 through 499. It uploads test results and the seed-sweep
log. The Unity integration check runs with the existing EditMode harness.
