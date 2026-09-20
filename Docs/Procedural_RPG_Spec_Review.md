# Procedural RPG spec and compatibility audit review

Reviewed 2026-09-20 against `8e1d1aaa`, with a clean starting worktree. This is a source review of `Procedural_RPG_Generation_Spec.md` and `Spec_v5_Compatibility_Audit.md`, including relevant implementation, assets, tests and CI. Tests were inspected, not executed; runtime behavior and performance are not certified here. The earlier audit remains a historical snapshot of `ea2593d5`.

**Verdict:** v5 is a substantial progression/world-generation expansion with reusable dungeon and town infrastructure. It is not ready to serve as an implementation contract: its proof model and several movement/persistence rules need clarification. The older audit correctly identifies the missing generation core, but understates current town and save functionality and overstates what some spec guarantees prove.

## Spec findings, in priority order

### S1 — High: the proposed validator does not establish the stated guarantee

References: spec §§9, 9.1, 15; G8, G9, G22, G30.

The exhaustive acquisition walk collects capabilities, while personal traversal is available only from the active party. Route width counts distinct capabilities, but no provider-to-party assignment algorithm or definition of a route between roster stops is supplied. Per-lock minima are insufficient: consecutive Hazard Ward and Deep Dive areas can each cost one specialist while their combined route needs two. Conversely, one companion providing both changes the slot count.

Define validator state explicitly: location, permanent capabilities, recruited roster, active party, resolved locks, visited towns and relevant progression items. Define legal recruitment, party changes, resolution and fast-travel transitions. Measure required-route capacity across a whole leg between legal party-change locations, with an actual provider assignment. A reduced model is acceptable only with a stated argument that it preserves these behaviors.

Also distinguish existential completion from universal recoverability. A successful exhaustive walk with guaranteed sources establishes a completion path under that model; it does not, alone, prove §15's “anyone can” claim. Require a recovery/completion path from every modeled reachable state, or identify and prove the invariants that make that search unnecessary. Tactical victory remains outside the structural proof while combat math is deferred.

### S2 — High: validating one spoiler sequence leaves alternative states unchecked

References: spec §15, “Map reachability, capability-parameterized”; §§4.1–4.3.

Map comparison is specified once per state along the spoiler log, despite alternate DNF solutions, optional acquisitions and party changes. An area reachable with an exploratory capability before acquiring a critical capability can expose a bypass absent from the selected log. Local boundary equivalence helps, but the document does not show that it covers all composed routes, resolved-lock states and party combinations.

Require graph/map equivalence for all relevant reachable states or a justified symbolic equivalence check. Define a mapping from graph nodes/edges to tile sets and thresholds; “tiles … match exactly the nodes” otherwise has no executable meaning. Keep the spoiler log as a witness and pacing scenario, not the sole proof domain.

### S3 — High: a conjunction-only terrain mask cannot express the lock model

References: spec §5.2 (`mask[terrain] ⊆ player capabilities`) versus §4.1 and G29.

A single required-capability mask expresses AND. A gate allowing `{Breach}` OR `{Engineering}` cannot use that formula without either requiring both or selecting only one alternative. Resolved obstacle/interaction flags also need to affect passability, while area traversal remains party-dependent.

Specify one shared legality predicate using semantic terrain, lock ID, normalized DNF requirements, permanent resolutions and active capabilities. Terrain masks can optimize conjunctive terrain checks, but cannot be the complete authority for movement. The validator and runtime must consume the same predicate.

### S4 — High: converter consumption conflicts with append-only progression

References: spec §4 (“consume items and capabilities”), §12, G2, G35.

Literal capability consumption violates permanent utility/vehicle state. Consuming ship parts is also ambiguous if those are protected progression items. Shared finite ingredients would require resource-state reasoning absent from the acquisition walk.

Recommended rule: converters check permanent prerequisites and latch a constructed-vehicle flag without consuming protected progression state. If consumable ingredients are intended, define their category, repeatability and allocation semantics and include them in the proof model. Do not leave “consume” as an implementation choice.

### S5 — High: interior traversal requirements contradict each other

References: spec §14.1, G10, G52; §5.2's example of a dungeon area-lock perimeter.

“No traversal gates inside interiors” coexists with interior area locks permitted to require entrance capabilities and optional grapple/climb routes. These describe different acceptance criteria. An entrance with `{Breach}` OR `{Engineering}` does not justify an internal mandatory `{Breach}` gate, even though Breach appears in its interface.

Specify that every legal entrance solution admits a path to required content and a safe exit, without changing party. Explicitly permit optional traversal branches if desired, and distinguish them from mandatory local utility locks. Rewrite G10 to match this rule.

### S6 — Medium: movement and timing need a complete shared contract

References: spec §0 vehicle row, §§3.1, 5.2, 8.1, 15, 22.

- The change table still says vehicles move multiple tiles per input, contradicting the one-tile rule elsewhere.
- Waystone Step uses paired teleport stones across a barrier, but no rule explains how it obeys the universal one-tile displacement guarantee. Specify adjacent-edge traversal or an explicit non-movement transition and validate that transition.
- The corner table says a Permissive blocker forbids passage between two blockers; the prose only explicitly handles two Permissive blockers. Define all mixed pairs, especially Permissive + Open, in a truth table. Declare the town default as well.
- Animation rates are said to be unknown to the core, yet the headless traversal check consumes them. Supply a shared versioned travel-cost profile as validator input while keeping animation execution in Unity.
- Define which path is measured when equally short routes have different times, and whether “minimum path” means steps, seconds or progression events. The 150-step/25-second budgets are feasible joint constraints, not a contradiction: 150 steps need at least 50 road steps if the rest are open terrain at the stated rates.

### S7 — Medium: several construction and acceptance rules are underspecified

References: spec §§3.1, 4.2, 13–16, 22.

- Critical and exploratory counts must partition the chosen manifest. Their ranges are not intrinsically inconsistent; independently sampling the ranges is. State `critical + exploratory = active` as a construction constraint.
- Tier-local repeatable dungeon existence proves access to an opportunity, not net power gain. Specify what reward survives the trip and how it can improve the party attempting the story dungeon. Current per-entry level reset makes this particularly relevant; persistent gear/skills could satisfy the intent without persistent levels.
- Bound generation repairs/retries and return an explicit failure when constraints cannot be satisfied. Reproducibility should include generator/content version, stable stream identifiers, dungeon ID and visit/floor identifiers, not just seed and an unspecified counter.
- Separate guarantees, tunable acceptance limits and aspirations. §22 says none of its parameters affect structure, but region counts, party capacity and capability counts clearly constrain construction.
- Correct §16's new-guarantee range to 46–64. The pipeline is numbered 0–13: fourteen stages, distinct from the thirteen top-level validation checks. The old audit calls it thirteen stages.

## Corrections to the existing compatibility audit

| Older claim | Current evidence and corrected assessment |
|---|---|
| Town phase has nothing; four hardcoded buildings | `Assets/Scripts/Town/TownConfiguration.cs`, `TownBuildingDefinition.cs`, `TownBuildingManager.cs`, `TownServices.cs`, and `Docs/Town.md` provide configurable buildings, recruits, menus and services. This is a reusable town foundation, not v5's full generated-town system. The current hub terrain asset remains 15×15. |
| All dungeon tiers are immediately accessible | `TownServices.CanEnter` checks cumulative donation thresholds; the four tier assets use 0/1000/3000/6000. Access is now explicitly gold-gated, still incompatible with v5's capability-only progression. |
| Shops choose five random items/prices each load | `TownServices.Shop/Buy` use authored catalogs and persistent stock keyed by town/building ID, with restock versions. The old randomness claim no longer describes this implementation. |
| Party can be dismissed to zero | `TownServices.Dismiss` refuses when only one member remains. Still missing: a designated undismissable protagonist and a permanent recruited roster separate from active members. |
| Saves cannot persist stacks or equipment | `ItemSaveData` stores stock and `TownAllyData` stores equipment. Ally IDs, completed tiers, shop state and an inventory format version now exist. Items still restore by name; there is no world-generator/content version compatibility check. |
| Defeat rules only need further auditing | `DungeonReturnService.Commit` clears bag and equipment when `LoseItemsOnDefeat` is true; the shipped configuration sets it true. This directly conflicts with G50's **gold and consumables only** loss whitelist, even before progression items exist. |
| Held-input movement does not work in the hub | `TownPlayer.Update` repeatedly reads sustained movement input whenever `_busy` clears. Unused `holdTime` bookkeeping does not prevent repeat movement. Terrain-dependent animation rates are still missing. |
| Coverage is limited to the older fixtures | `TownGameplayTests` covers transactions, donation gates, dismissal and dungeon returns; `InventorySkillTargetingTests` covers targeting/item-use validation; `SaveStoreTests` covers migration. These are inspected tests, not new passing-run evidence. No headless world-generation validation suite is present. |
| Level reset structurally violates append-only progression | `Game.InitializeGame` still sets level to 1. However §12's protected-state list does not explicitly include level/EXP. Reset is a design decision requiring a durable power-gain model, not sufficient evidence by itself of a G1 violation. |

The audit's §4.3 budget observation is a tuning implication, not an inconsistency. Its recommendation B also need not abandon provability: a small hub graph can be exhaustively validated. Scope and proof strength are separate choices. Likewise, the presence of build/deploy CI does not establish that the tactical layer is “complete” or that a release is currently live.

## Compatibility that remains unchanged

| Area | Current assessment |
|---|---|
| Engine-independent generation | Missing. `EternalEnigma.Game.asmdef` references Unity/TWC; town and dungeon generation run through MonoBehaviour callbacks. |
| Deterministic whole world | Missing. `Town.Start` injects a seed into a TWC asset; `TileWorldDungeonGenerator` regenerates floors without world/dungeon/visit seed derivation. TWC contains TickCount seed fallbacks and runtime selection still includes GUID shuffles. |
| Capability manifest, progression graph, lock DNF, reservations and proof harness | Missing as a coherent implementation. Existing dungeon floor tiers and donation thresholds are not these systems. |
| Shared movement | Useful foundation in `GridMovement`, A* and BFS. Two global diagonal policies remain; no per-terrain three-class policy. Town `WalkableMap` still permits corner cutting and derives one boolean mask from Houses/Trees. |
| Tactical dungeon and fog | Reusable turn/action, inventory, targeting and LOS/fog systems. Existing radius/LOS behavior does not implement the spec's whole-room visibility contract. |
| Towns | Reusable scene, configuration, service and save infrastructure. Generated multi-town geography, enclosure interiors, roster network, rumours and town visibility remain new work. |
| Persistent story dungeons | Missing; existing generation destroys/recreates dungeon instances. No serialized story layout/resolved-lock state. |
| Protected progression | Missing capability/knowledge/visited-town/resolved-lock save state and protected progression inventory. Current recruited-save list represents the active party, not an append-only roster. |
| CI assurance | `.github/workflows/main.yml` builds/deploys; it does not run the spec's headless validator or existing Unity test suites. |

## Recommended project-specific sequence

1. Resolve S1–S5 as a short normative contract before implementation. Write down exactly what “winnable” proves and what combat assumptions it excludes.
2. Add a small engine-free progression library beside the current game. Start with IDs, coordinates, seeded streams, DNF requirements, legal state transitions and graph/map equivalence. Port shared movement at its adapter boundary; extracting all sight/pathfinding code first is not a prerequisite.
3. Prove a tiny world: two towns, one party-bound area gate, alternate utility resolution, a converter, one persistent story dungeon and one repeatable dungeon. Include party switching, defeat/re-entry, save/reload and an intentional bypass that validation rejects. Run the proof fixtures in plain .NET CI.
4. Adapt `TownConfiguration`/`TownServices` to generated town interfaces. Separate roster from party; replace required donation/recruitment gates with permanent permissions; add a protected item category and explicit defeat whitelist. Preserve ordinary combat purchases where the spec permits them.
5. Separate world data from mutable player state, introduce generator/content/schema versions and stable item identities, and define dungeon seed derivation. Reuse tactical rendering and actions through adapters.
6. Expand geography and content only after the small slice passes the structural checks. Benchmark generation separately from validation over many seeds; the spec's “under a couple of seconds” target is not evidence of current feasibility.

No gameplay or normative spec changes were made by this review. The report records decisions needed before adopting v5 and the concrete infrastructure available to support it.
