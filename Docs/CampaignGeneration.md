# Core campaign generation

`CampaignGenerator.Generate(seed)` returns an immutable logical campaign after validation.
Independent seeded streams control activation, topology, identities and lock narration.
Core does not depend on Unity or global random state. Current generator metadata is defined
by `CampaignGenerator.Version` (12); grid metadata is `OverworldGrid.GenerationVersion` (12).
These identify generated content and diagnostics, not a player-save schema.

## Generated content

Six ordered semantic regions A-F have distinct themes and landmarks. Five progression tiers
contain four story dungeons, the final dungeon, repeatable locations and towns. Interior
dungeons have `ParentTownId`; their grid projection shares the town tile. Towns provide the
five service kinds: bakery, consumables, items, inn and one trainer. `TownLayout` assigns them
to seeded slots with one authored dungeon entrance and seven residential plots. Unity gives
town-0 a home in its first residential slot.

The 21-capability vocabulary activates a seed-dependent subset of personal, vehicle and utility
abilities. Roles partition active abilities into critical and exploratory uses. Required
routes retain critical solutions; exploratory abilities supply alternatives and optional payoffs.
Two guaranteed providers protect capability access. Personal providers recruit companions;
utility/vehicle sources permanently latch capabilities. Vehicle converter sites require
Engineering, without consuming it; component crafting recipes are not implemented.

The opening requires two separate completions: story-0 unlocks the town exit, repeatable-0
supplies the key for the physical starter-exit. Ordinary location reward claims award neither.
One required return objective and three optional return secrets reuse earlier regions.
Keyed B-D, B-E and B-F warps reduce later return travel. Actual water still requires Boat.

## Lock fiction

`LockNarrator` runs after topology, using `LockSkinCatalog` keyed by capability/form/theme.
Routes store `SkinId`, `LockText` and displayed `KeyName`. `KeyId` remains mechanical identity.
The validator rejects unknown skins, leaked capability names, unresolved templates, missing
fiction and duplicate key names. Narration does not alter route requirements.

Legibility tiers, graded wrong-capability feedback, finer local-terrain selection and
chronicle/faction-aware narration remain unimplemented. The completed first-pass narrative
plan has been removed; these limitations remain here.

## Runtime and validation

`CampaignSession` is the abstract graph traversal model. `CampaignContext` hosts Unity's
position, party, scene travel, dungeon completion, interrupted-run recovery and snapshot capture.
The protagonist has up to three active companions; changing them requires a town. Personal
capabilities depend on the active party. Obstacle/interaction resolutions and acquired keys
persist; area locks recheck capabilities. Unity combat determines when completion is awarded.

`CampaignValidator` checks references, manifest composition, source redundancy, prerequisite
cycles, gate alternatives, progression cuts, required returns, shortcut bypasses and reachability.
Its closure considers party choices at towns and companion exclusion. It assumes reachable
encounters can be completed; it does not prove combat balance. `OverworldGridValidator`
separately checks physical realization, components and sealed gate contacts. Floor/town
validators check their own layouts; Unity fixtures verify adapters and scene gameplay.

`CampaignFingerprint` hashes canonical logical content, including services and fiction.
Collection order and culture do not change it. Current-schema campaign restores check this
fingerprint against regenerated content. Snapshots store `GeneratorVersion`; snapshots without
it regenerate version 11, retaining standard layouts. Version 12 assigns intact town districts
to half of eligible repeatable dungeons. `GenerateVersion` preserves version-11 campaigns.

## Reverse-world finale

`FinalePlan` validates five overworld maps before play. Floor 1 is the campaign's
exact grid; floors 2–5 use deterministic derived seeds and the same generator version.
Incompatible derived maps retry deterministically. The original campaign is never
replaced. Every floor travels from its final-dungeon marker back to its starting town,
opening regions in reverse progression order. Region IDs keep equal-biome regions separate.

Every region receives the normal tier-4 placement budget and normal endgame enemies,
including packs, awareness, attacks, experience and drops. Populations are finite.
Safe arrivals, location approaches, stairs and warps are reserved. Required placements
are validated without Boat; former water-lock crossings become visible finale bridges.
Campaign keys cannot bypass region boundaries; movement skills and warps enforce them too.

Towns and external dungeons remain miniatures until an adjacent legal entry attempt
reveals a one-tile mimic. Hidden mimics cannot act or take damage, and reveal grants no
enemy attack that turn. The three town families are Castle Monster, Bone Dragon and
Ice Golem; dungeon families are Flame Knight, Little Demon, Skeleton Mage, Blue Slime,
Crawler, Flower Monster and Star Fish. All ten occur each run and are excluded from
ordinary spawn pools. Floor 5's starting town is Castle Monster. No interior battles
or scene transitions occur during a floor. Finale models use the same uniform combat
scale as ordinary dungeon enemies, preserving the asset pack's native proportions.
Their combat footprints remain one tile.

Every enemy has a floor-qualified identity and fixed home region. Pack members and
summons join that region's ledger. Only actual deaths count; hidden mimics, conversion
and arbitrary despawning cannot satisfy clearance. Summons resolve before gates open.
Enemies activate only in unlocked regions and cannot leave their home region.
The HUD reports remaining enemies; hidden location markers remain visible, and the
minimap reveals all surviving ordinary enemies after the last hidden mimic is revealed.

Only a fully cleared floor enables its starting-town staircase. Ascending preserves
party state and loot using normal floor rules, without a free rest. Previous terrain
and actors are released. Using floor 5's staircase commits completion once and starts
the wake-only sequence in the original bedroom, with restored vitals and nearby allies.
Control and camera return after waking. No victory dialog, new-adventure reset,
starting supplies, checkpoint or automatic save occurs. Retreat retains loot in memory
and resets finale objectives; defeat and quit restore the last explicit save.
Completed slots remain playable, track playtime and allow ordinary dungeons, while
the completed finale stays closed.

## Commands

From `Core/`:

```powershell
dotnet test EternalEnigma.Core/EternalEnigma.Core.slnx --configuration Release
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --output ../Temp/CampaignPreview
dotnet run --project EternalEnigma.Core/EternalEnigma.Campaign.Cli --configuration Release -- --seed 42 --grid --output ../Temp/OverworldPreview
```

CLI JSON/SVG are diagnostic exports, not player save files. The console explorer supports
interactive world/town/dungeon inspection and skill-tree editing. Core CI builds/tests and
runs campaign/grid seed sweeps. Rebuild/import the Core DLL after code changes before Unity
tests. See [Core](../Core/README.md), [campaign flow](CampaignFlow.md) and [overworld](OverworldGrid.md).
