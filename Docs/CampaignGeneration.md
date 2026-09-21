# Core campaign generation

`CampaignGenerator.Generate(int seed)` produces an immutable logical campaign and
validates it before returning. Generation version 7 uses explicit seeded streams
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

Version 6 retains the guarantee of Boat in every campaign, with two converter-site providers
requiring Engineering. This supports navigable water in the biome grid while
keeping acquisition on the original land route network. Earlier fingerprints intentionally change; biome assignment belongs to grid generation version 10.

| Component | Version 7 behavior |
|---|---|
| Manifest | Closed 21-capability vocabulary; activates 3â€“4 personal, 2â€“3 vehicle and 4â€“5 utility capabilities, with required area/water/narrative coverage. |
| Roles | 5â€“7 critical and 4â€“6 exploratory, partitioning the active set; Engineering critical, 1â€“2 personal critical, at least one vehicle critical. Three active vehicles always include an exploratory one. |
| Progression | Five tiers, with 5â€“7 gated stages along a spine. Every boundary includes a critical solution. The required return reward has a sole-solution boundary; other boundaries retain exploratory alternatives. |
| Regions | Six ordered semantic regions A through F, unique themes, and one designated landmark each. Physical coordinates use grid version 10. Later logical stages can occupy earlier regions. |
| Towns | At least one roster/fast-travel town in each of the six biomes (6-7 towns total). A town separates same-tier personal gates when needed to preserve the one-specialist required route. |
| Dungeons | Four story locations, a final dungeon, and source-free repeatable locations covering every tier, plus one in the extra town. These are dungeon interfaces, not floor layouts. |
| Sources | Two guaranteed providers per capability, respecting source tiers. Personal abilities have two distinct companion providers; utility/vehicle abilities latch permanently. |
| Payoffs | Every active capability is used by at least two locks and has a sole-solution optional payoff. Exploratory capabilities also appear as required-route alternates. |
| Converters | Vehicle sources are converter sites requiring Engineering. Claiming a reward consumes no permanent capability. Component items/recipes are not implemented yet. |

The spine topology is deliberately bounded. Seeds vary capabilities, roles, their
order, alternatives, source placement and themes. The grid supports the generated district topology and keyed inter-biome loops, rather than arbitrary nonplanar graphs. Specialist providers number 6â€“8; a complete authored
eight-person combat roster is separate from this capability-provider model.

## Return objectives and shortcuts

Every campaign contains one required return objective and three optional return
secrets. `ReturnObjectives` records each earlier region, destination and gate IDs,
enabling capability, its acquisition stage, required flag and optional critical
reward. The three secrets reuse existing capability payoffs. Their sole capability
first becomes obtainable at least one stage after the initial biome visit.

Both providers of the required reward move behind separate gates in the same
earlier biome. The reward is critical and is never Engineering. Its enabler is
obtained elsewhere after the opening stage. A subsequent progression boundary
requires the reward without alternatives. Source counts and Engineering
prerequisites remain intact. The earlier gate entrances are available before the
enabler; inspecting them is optional. Optional return secrets can all be omitted.

Six ordered regions form the biome chain A–B–C–D–E–F. Three keyed warps join
B–D, B–E and B–F. Each key is collected at the corresponding later biome's
landmark, reached through normal progression first. Keys are permanent and
nonconsumable; collecting one unlocks its warp in both directions. With all keys,
every pair of biomes is at most two transitions apart. Extra progression stages
remain inside F. Region `ProgressionOrder`, route `ShortcutKind`, `KeyId`,
`KeyLocationId` and `IsWarp` are included in fingerprints and diagnostic exports.

`CampaignRoute.TryCollectKey` and `CampaignSession.TryCollectShortcutKey` share
the resolved-route state used by permanent locks. Collecting at any other site
fails; a shortcut cannot be opened by interacting at its endpoint. Legacy far-side
and capability route support remains available in the core. Water requires Boat
regardless of key ownership.

Validation checks return metadata, provider exclusivity, sealed entrances,
circular dependencies, completion without optional secrets, and failure to
complete without the required reward. The explorer simulates reachable unlocking
actions. Grid validation checks all closed components and gate contacts, including
loops, diagonal sealing, compactness and shortcut savings. Fingerprints include
all return and shortcut metadata; earlier fingerprints intentionally change.

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
- `TryCollectShortcutKey(routeId)` requires the route's key location and permanently
  opens that keyed passage. Keys persist through defeat and reset with the session.
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
a cut must imply its intended DNF requirement, except keyed shortcuts, which use
reachable key acquisition instead. Validation removes each progression boundary
and simulates all reachable key collections; no later checkpoint may become
reachable through a shortcut or chain of shortcuts. It also verifies that all key
sites remain reachable with shortcuts excluded. The boundary route itself supplies the intended alternatives, making the
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
pins seed 42 for generation version 7. An intentional generation change requires
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

## Starting enclosure and completion keys

Version 6 adds `Campaign.StarterLocations` and a physical `starter-exit` route.
`KeyAcquisition.DungeonCompletion` gates the two opening steps separately:
`story-0` unlocks the town exit, then `repeatable-0` awards the key for the
physical `starter-exit` out of the town area. Location-only collection leaves
both keys unavailable. The area gate requires a cardinal-adjacent interaction. The graph explorer assumes reachable
story encounters can be completed; gameplay calls the production completion API.
The validator rejects extra starting locations, bypasses of either opening gate,
and warp bypasses. Town exits are enforced by scene travel and map departure;
only the outer area gate has an overworld lock footprint.
Grid version 10 places the starting story dungeon inside town-0. Interior
dungeon nodes share their parent town position; distinct overworld destinations
use eleven-tile minimum spacing (six for the opening town/outdoor dungeon pair). See campaign flow for interior entry
and return behavior.

See [campaign flow](CampaignFlow.md) for launch modes, persistence and travel.
