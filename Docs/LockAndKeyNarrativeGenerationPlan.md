# Lock-and-key narrative generation: audit and implementation plan

Status: audit complete; no code changes made. Written 2026-09-25.

## Intended outcome

Every gate in a generated campaign should present as fiction first: a boulder
that blocks a mountain pass, a guard who refuses passage at a city gate, an
untranslated inscription in a ruin. The player should never see a raw
capability name. This plan is the gap analysis between what
`Procedural_RPG_Generation_Spec.md` already specifies for this system (§3, §4.1,
§6, §7, §19 item 6, §21) and what exists in code today, plus the order of work
to close it.

## Current state

### Fully specified, not yet built: the narrative/skin layer

`Procedural_RPG_Generation_Spec.md` §6 ("Lock-and-key interaction model") and §7
("Locks and their realization") already define:

- **Skins** (§6.6): authored per **capability × lock form × region theme ×
  local terrain**. E.g. Breach + Obstacle + mountain → "a boulder blocks the
  pass"; Breach + Obstacle + swamp → "thorn growth chokes the trail";
  Diplomacy + Interaction + city → "a guard refuses passage." Skin selection
  happens at generation time and is stored (stage 9); presentation only
  renders it, never chooses it.
- **Legibility** (§6, three tiers: Plain / Inferable / Obscure) — how directly
  the fiction hints at the required capability.
- **Failure feedback** (§6, three tiers: Irrelevant / Relevant-insufficient /
  Near-miss) — response text when the player attempts the wrong capability
  against a lock.
- **Lock forms** (§7.1): `Obstacle` (discrete object), `Area` (terrain itself
  is the lock), `Interaction` (a person/object/situation).
- **The lock table** (§19 item 6): "Per capability: skins with description
  text, legibility flag, lock form, eligible themes and terrains,
  multiplicity, visual treatment, and failure responses against every other
  active capability." This authored data table does not exist yet — no
  JSON/ScriptableObject/YAML file implements it.
- **Chronicle seam** (§21): skin selection is designed to later upgrade from
  theme-keyed to chronicle-keyed (the boulder becomes the rockfall from a
  war), without touching progression logic.

### Implemented: the logical/mechanical layer

`Core/EternalEnigma.Core/EternalEnigma.Core/`:

- `Capabilities/Capability.cs` — the closed 21-value capability enum
  (`Climb, Grapple, Icewalk, DeepDive, Breach, WaystoneStep, PhaseShift,
  HazardWard, Boat, Icebreaker, Airship, Tunneler, Engineering, Translation,
  RoyalAuthority, AncientAttunement, BeastSpeech, GuildStanding, Remedy,
  Diplomacy, SacredRite`), `CapabilityKind`, `CapabilityRole`,
  `CapabilitySet` (bitset "key" combination).
- `Progression/Requirement.cs` — the DNF solution-set structure
  (`CapabilitySet[] Alternatives`, 1–3 alternatives of ≤2 capabilities each),
  matching spec §4.1.
- `Progression/Campaign.cs` — `LockForm` enum (`None, Obstacle, Interaction,
  Area`, matching spec §7.1), `CampaignRoute` (`Requirement`, `Form`,
  `ShortcutKind`, `KeyId`/`KeyLocationId`/`KeyCondition`, `GateHint`),
  `CampaignRegion.Theme` (biome theme, e.g. Highlands/Marsh/Coast/Forest/
  Desert/Tundra/Ruins/Volcanic).
- `Generation/CampaignGenerator.cs` — deterministic seed-based generator that
  activates capabilities and builds the region/route/gate graph.
- `Validation/CampaignValidator.cs` — enforces manifest composition and DNF
  bounds. No legibility/skin/failure-feedback validation exists yet
  (spec rules G32/G33/G36/G44 in §15 are unchecked).

### The current anti-pattern in the player-facing path

`World/OverworldGates.cs` and `Assets/Scripts/Overworld/OverworldScene.cs`
render `GateHint` directly, producing text such as `"Requires: Breach"` and
`"Enter / A: Use Breach to open route-004"`. This is the literal "Poor"
example spec §6.1 calls out as what not to do, versus the target
`"A collapsed mine entrance blocks the tunnel."` This is the only place a lock
currently reaches the player, and it will need to be replaced by skin-driven
text once the skin table exists.

## Context inputs a lock/key generator needs

For each generated `CampaignRoute`/lock, the narrative generator needs:

1. **Capability(ies) that satisfy it** — from `Requirement.Alternatives`;
   determines candidate skin families and solution verbs.
2. **Lock form** — `Obstacle` / `Area` / `Interaction`; determines noun
   category (object vs. terrain vs. person/situation).
3. **Region theme** — `CampaignRegion.Theme`, already generated; selects
   which skin variant fires (mountain boulder vs. swamp thorns vs. city
   guard).
4. **Local terrain/tile context** — finer-grained than region theme, needed
   for multiplicity and placement (cliff road vs. mountain pass, both under
   "Highlands").
5. **Legibility tier** — assigned at generation time per lock, not derived
   from capability or theme alone; controls how directly the fiction reveals
   the solution.
6. **Failure-feedback tier per wrong-capability attempt** — needs the full
   active-capability manifest for the seed, not just the correct one, so a
   response can be authored against every other active capability.
7. **(Deferred per §21) Chronicle/faction state** — not yet modeled anywhere;
   the seam exists precisely so this can be added later without touching
   `Requirement`/`LockForm`.

No stat-tag system (e.g. STR/CHA) exists or is planned — capabilities are
atomic named abilities, not derived from character stats.

## Work to close the gap

1. **Author the lock table** (spec §19 item 6): a data asset (ScriptableObject
   or equivalent) keyed by capability × lock form × theme, holding skin id,
   description text, legibility flag, eligible terrains, multiplicity, visual
   treatment, and failure-response text against every other active
   capability. This is pure content authoring, decoupled from generation
   code.
2. **Add a skin-selection stage to `CampaignGenerator`** (spec stage 9): for
   each generated lock, deterministically pick a skin from the lock table
   using the seeded stream, store the chosen skin id and legibility tier on
   `CampaignRoute` (or a new field), and validate the pick against the active
   manifest. Selection must happen at generation time and be immutable
   afterward — presentation must not choose.
3. **Extend `CampaignValidator`** to check the new spec rules (G32/G33/G36/
   G44 in §15) once skins exist: every lock has a valid skin for its
   theme/form, legibility values are within range, failure responses cover
   every other active capability.
4. **Replace `OverworldGates.GateHint` / `OverworldScene` rendering** to
   render the stored skin's description/failure text instead of the raw
   `Requirement`/`KeyId` value. This is the only consumer that currently
   leaks capability names to the player.
5. **Extend `CampaignFingerprint`** to hash the new skin/legibility fields so
   determinism tests catch regressions in skin selection the same way they
   catch regressions in topology.
6. **Add tests** mirroring the existing generator/validator test structure
   (`CampaignGeneratorTests.cs`, `CampaignGenerationTests.cs`, etc.) for skin
   selection determinism and validator coverage of the new rules.

None of this requires changing `Capability`, `Requirement`, or `LockForm` —
the mechanical model is stable and correct per spec; the gap is entirely the
authored content table and the generation/rendering seam that would consume
it.

## Non-goals for this pass

- Chronicle/faction-keyed skins (spec §21) — explicitly deferred; the plan
  above only needs to preserve the seam (skin lookup keyed by
  capability × form × theme, not hardcoded per-lock).
- Any stat/trait-based key matching — not part of the spec; capabilities
  remain atomic.
