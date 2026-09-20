# Audit — `Procedural_RPG_Generation_Spec.md` (v5) vs. Eternal Enigma as built

Audited 2026-09-16 against `main` @ `ea2593d5`. Every claim below is sourced to a
file in this repo; nothing is taken from the document's own assertions.

---

## 0. Verdict

**The document is not a plan for this game. It is a plan for a different game
that could reuse this game's dungeon layer.**

Compatibility splits almost perfectly along the spec's own phase boundary (§20):

| Spec phase | What it needs | State in this repo |
|---|---|---|
| Phase 0 — progression core | capabilities, regions, tiers, locks, solution sets | **nothing exists** |
| Phase 1 — geography & lock realization | 256×256 terrain, reservations, perimeters, vantage | **nothing exists** |
| Phase 2 — walkable overworld & interaction model | grid movement, locks, Field Inventory, knowledge layer | movement exists; interaction model **nothing** |
| Phase 3 — interiors (towns) | town maps, buildings, NPCs, roster | **nothing exists** |
| Phase 4 — dungeon tactical mode & combat | turn engine, enemies, items, defeat | **substantially built and shipping** |
| Phase 5 — narrative | factions, chronicle, quests | out of spec scope too |

§20 calls Phase 4 "the largest single build in the project and the only one the
other phases do not depend on," and schedules it last. **This project built it
first.** That inversion is the single most important fact here, and it is good
news: nothing built so far is wasted, because the dependency arrows all point
the other way.

What it also means: adopting v5 is not a modification of this game. It is
building four new phases underneath it. Roughly two-thirds of the document
describes systems with zero code behind them.

---

## 1. What this game actually is today

- **Eternal Enigma: Avarice's Abyss** — shipping to itch.io from CI
  (`.github/workflows/main.yml`: WebGL + StandaloneWindows64; CI builds, runs no
  tests). 152 commits, 227 `.cs` files, Unity 6000.2.7f2, one gameplay assembly.
- **A Mystery-Dungeon roguelike with a hub village.** The damage formula even
  cites a Torneco reference page (`MovementAction.cs`), and the enemy spawn
  tables are Torneco-derived CSVs.
- **Flow:** `Common` (persistent bootstrap, build index 0) → `FartScene`
  (splash) → `MainMenu` → `OverworldScene` → `DungeonScene`, via full
  `SceneManager.LoadScene` transitions.
- **Overworld:** a **15×15** L-system village
  (`Assets/TileWorldCreator/VillageLSystemAsset.asset`), generated at scene start
  from the save's seed (`Overworld.Start` → `SetCustomRandomSeed` →
  `ExecuteAllBlueprintLayers`). Four hardcoded interactable buildings — entrance,
  shop, statue, ballista (`Overworld.GenerateInteractableBuildings`). No NPCs, no
  dialogue system, no doors.
- **Dungeon:** 32×32 floors (`Assets/Prefabs/Dungeon/DungeonAsset.asset`, a
  TileWorldCreator graph of 56 `BSPDungeon` actions), 12×12 throne floors at the
  start and end floor, floor ranges picked from 4 `DungeonTierData` assets
  (1–5, 5–10, 10–20, 20–30; all four have an empty `TierName`).
- **Tactical layer, real and complete:** `TurnManager.ProcessTurnRoutine` runs
  controlled ally → other allies → enemies, with an action/response cascade,
  multi-action-per-turn support, and a two-phase logic-then-animation replay.
  26 action types, 33 enemy prefabs with priority AI policies, 8 status effects,
  traps, hunger, 74 weapon definitions, 11 skills, thrown items, fog, minimap.
- **Saves:** `GameSaveData` = overworld seed, gold, donation total, inventory
  item *names*, recruited ally names + skill names, plus start/end floor. Stored
  as one `JsonUtility` blob in a single PlayerPrefs key (`SaveSystem.cs`).
  Character level and EXP are **reset to 1 on every dungeon entry**
  (`Game.InitializeGame`) and are not persisted at all.

---

## 2. Strong alignment — reuse as-is

These are not incidental; they are why the document is worth taking seriously
against this codebase at all.

1. **§5.1's shared grid foundation is already implemented as designed.**
   `Assets/Scripts/GridMovement.cs` is a static module whose header reads
   *"Terrain rules only. Occupancy, turns, interactions and input belong to each
   mode."* It is shared by the overworld, the dungeon, A\* and BFS. The
   appendix's demand — *"one table, read by both"* — already has its home, and
   `GridMovementTests` already pins its semantics.

2. **§5.2's 8-way, one-input-one-tile movement is built.** One `Facing` enum,
   `GetFacingOffset`, discrete steps gated by a `_busy` flag while the tween
   plays. The appendix caution *"an implementation that interpolates the
   character along a multi-tile path is a multi-tile step wearing a costume"* is
   already satisfied in both modes. §22's 0.20 s base walk rate is literally the
   existing overworld tween (`OverworldMovement.cs:64`).

3. **§14.2's tactical mode exists and is good.** Two-phase resolution (logic
   first, then batched simultaneous animation) is more sophisticated than the
   spec asks for, and `GameAction.CanBeCombined` is exactly the seam a turn
   engine needs.

4. **§14.5's recommended answer is already the built answer.** Four allies are
   real actors in the turn list with per-ally strategies
   (`AllyStrategy { Follow, Aggresive, HoldPosition }`) and a `SwapAlly` input.
   §9.1's "three free combat slots" argument has something real to protect.

5. **§5.7's visibility model has a reusable, near-portable implementation.**
   `DungeonSight.cs` is deterministic integer LOS over a `bool[,]`, with walls as
   visible-but-blocking endpoints and explicit refusal to squeeze sight past a
   blocked corner. It maps onto all three of the spec's layers: raise the radius
   to §22's 48 for overworld terrain-occluded sight, reuse unchanged for §14.9's
   town wall occlusion. Three-state fog already exists
   (`MinimapTileVisibility { Unseen, Explored, Visible }`, `FogOverlay`,
   `FogHiddenVisual`), with explored-forever semantics — §5.7's "concealment is
   never knowledge erasure," done.

6. **§14.9's "None" interior depth is the existing building model.** Buildings
   are impassable footprints whose door opens a service UI
   (`OverworldBuilding.Interact` → dialog → reverse step off the tile). The
   spec's single largest town-generation saving is what this game already does.

7. **§4's difficulty bands have a data shape.** `DungeonTierData`
   (TierName, StartFloor, EndFloor) plus `SpawnDefinition` with
   `FloorMin`/`FloorMax` is where §15's power-band check would attach.

8. **The testing culture the spec asks for partly exists.** `GridMovementTests`
   covers diagonal policy per mode and A\*/BFS agreement on blocked corners;
   `DungeonSightTests` covers wall occlusion and corner peeking. The document's
   repeated "this guarantee should be a test, not a memo" (G47, G55) lands in a
   project that already writes those tests.

---

## 3. Direct contradictions — spec says X, code does not-X

### 3.1 Critical — there is no engine-independent core, and no headless harness

§2 is the document's foundation: *"The simulation is engine-independent … A
headless harness generates and validates thousands of worlds with no engine
running. The project's primary safety net."* The appendix: *"Engine independence
has to be enforced, not intended. A separate library with a build-time check
that it cannot reference the engine."*

Today:
- One gameplay assembly, `EternalEnigma.Game.asmdef`, referencing
  `Unity.InputSystem`, `Unity.TextMeshPro`, `Unity.ugui`,
  `Unity.2D.Tilemap.Extras`, `TileWorldCreator`, `TileWorldCreator.OdinSerializer`.
- Generation **is** the MonoBehaviour lifecycle. `Overworld.Start()` triggers it;
  `WalkableMap.blueprintLayersComplete` derives passability from a
  TileWorldCreator callback; terrain itself comes from TWC blueprint layers — a
  Unity editor asset pipeline, not portable code. Dungeon passability is queried
  live from `GetMapOutputFromBlueprintLayer` on **every** `IsWalkable` call.
- Even the two most portable modules, `GridMovement` and `DungeonSight`, are
  built on `UnityEngine.Vector3Int` / `Mathf`.
- The harness is **editor-only by design**: PlayMode fixtures load production
  scenes through `EditorSceneManager`, driven by an MCP bridge to a running
  editor (`Tools/unity-mcp.mjs`). `Assets/Tests/README.md` states plainly they
  "are not player tests."

**Consequence:** §15 — thirteen checks over thousands of seeds in "under a couple
of seconds" — cannot be built in this codebase as structured. §15 is the
document's entire value proposition (pillar 2: "provable by construction, not by
playtesting"). This is the gating prerequisite for everything else, not cleanup.

### 3.2 Critical — generation is not deterministic today, and the seeding
mechanism is actively hostile to making it so

§2 requires generation to be "deterministic from a seed; worlds are shareable,
bugs reproducible." The appendix wants *"a dedicated seeded generator with a
separate stream per pipeline stage"* and *"Activation must get its own RNG
stream, and it must be stream zero."*

Three separate problems, in ascending order of severity:

1. **Everything uses the global `UnityEngine.Random` static.** Gameplay
   (`ItemManager.cs:24`, `ItemDefinition.cs:25`, every enemy policy) and all
   TileWorldCreator generators (`BSPDungeon.cs:193`, `LSystem.cs:169`,
   `Select.cs:70`, `Pick.cs:37`, …) each call
   `UnityEngine.Random.InitState(_twc.currentSeed)`. Two places shuffle with
   `Guid.NewGuid()` (`Enemy.cs:177`, `EnemyManager.cs:21`), which is not
   seedable at all. `Assets/Tests/README.md` already concedes: *"A fixed seed
   controls generation, not every gameplay decision."* No per-stage streams
   exist, so neither the appendix's "adding a stage later doesn't reshuffle
   existing seeds" property nor per-town stream derivation is available.

2. **Dungeon floors are not seeded at all.** `DungeonAsset.asset` and
   `DungeonThroneAsset.asset` both carry `useRandomSeed: 0` / `randomSeed: 0`,
   and `TileWorldDungeonGenerator.GenerateDungeon()` never calls
   `SetCustomRandomSeed` — so each floor seeds from
   `System.Environment.TickCount` (`TileWorldCreator.cs:587`). There is no
   seed-plus-floor-index derivation anywhere; conversely, when the test harness
   *does* force a fixed seed, every floor generates **identically**. §14.3's
   requirement that a regenerating dungeon's seed derive from "the world seed
   plus a visit counter, so it is reproducible for bug reports without being
   static" has neither half of that property today. Only the 15×15 village is
   reproducible, and only because `Overworld.Start` seeds it explicitly.

3. **Seeding mutates the generator asset.** `SetCustomRandomSeed` writes
   `twcAsset.useRandomSeed = true; twcAsset.randomSeed = _seed` — it dirties a
   project ScriptableObject. `GameTestHarness` has to clone the asset to work
   around it, with the comment *"TWC.SetCustomRandomSeed mutates its
   ScriptableObject; clone to protect project assets."* Seeding is a stateful
   side effect on an editor asset rather than a parameter to a function, which is
   the opposite of what "deterministic from a seed" needs and cannot survive the
   thousands-of-worlds bulk runs §2 describes.

### 3.3 High — the corner-cut model is the wrong shape and the wrong default

§5.2 declares **three** classes (Sealing / Permissive / Open) as a **property of
each terrain class**, with reserved structure **forced** to Sealing and
non-overridable (G54), and the overworld defaulting to Sealing "because every
reachability proof lives here."

`DiagonalMovement` has **two** values, and they are a **call-site argument, not a
terrain property**:

- `AllowCornerCutting` ≡ spec's **Open**
- `RequireOpenSides` ≡ spec's **Sealing**
- the spec's middle class **Permissive** — allowed past one blocker, forbidden
  between two — **does not exist**, and it is the spec's *dungeon default*.

And the default is inverted exactly where it matters: `WalkableMap.CanWalkTo`
passes `AllowCornerCutting` — the most permissive setting — on the **overworld**,
while the dungeon (`TileWorldDungeon.CanWalkTo`) and both pathfinders use
`RequireOpenSides`. `GridMovementTests.DiagonalPolicyPreservesEachMode` locks
this in as intended current behavior.

G54 and G55 therefore require replacing a boolean policy argument with a per-tile
class carried in the world description. The contained good news: that is one
68-line file plus a handful of call sites, with tests already in place to
re-pin.

### 3.4 High — scale

| Spec (§22) | Built |
|---|---|
| Overworld 256×256, 6–8 regions, 5 progression tiers | 15×15 village, no regions, no tiers |
| 1 start town + 5, at 64×64 / 96×96, 12–30 buildings each, ≥3 rumour NPCs each | no town scale; 4 hardcoded buildings; **zero NPCs** |
| 4 story dungeons + final + ≥1 repeatable per tier | 1 dungeon, floor-band presets |
| 8 recruitable companions, party of 4, roster in every town | 4-cap party, no bench/roster concept |

The overworld is ~291× smaller in area than specified, and it is not a
region-partitioned continent — it is a menu you can walk around in. Nothing in
the current generator reserves structure, partitions regions, or assigns tiers.

### 3.5 High — no capability, lock, or knowledge vocabulary exists

A grep over `Assets/Scripts` and `Assets/Tests` returns **zero** files mentioning
`capability`, `town`, `NPC`, `dialogue`, `vantage`, or `perimeter`. `lock`
appears once, as the word "unlock" inside an ally's backstory string
(`OverworldAllyManager.cs:69`). `climb|breach|boat` → zero hits. `tier` exists
only as dungeon floor bands.

So §3 (capability model), §4 (world structure, solution sets, roles), §6
(lock-and-key interaction, Field Inventory, failure feedback, legibility), §7
(lock forms, reservation, thresholds, vantage), §8 (vehicles, navigable zones,
enclaves), §10 (knowledge layer, journal, rumors, learned classes), §11 (themes,
landmarks, naming) and §14.7–14.12 (towns) are **all greenfield**. That is the
majority of the document.

### 3.6 High — the save model cannot hold the spec's progression state, and the
game deliberately resets progression

§12 and G1 require append-only progression covering capabilities, companions,
**visited towns**, **map annotations**, **heard rumors**, **learned lock
classes**, **resolved locks**, plus §5.7's explored terrain, all as save state.

`GameSaveData` holds six scalars and two name lists. Not persisted: level, EXP,
HP/SP, equipped state, item stack counts, item variants, any dungeon state. And
`Game.InitializeGame` sets `ally.Vitals.Level = 1` on **every** dungeon entry —
character progression is per-run by design. That is correct for a Mystery Dungeon
game and structurally opposite to G1.

Also:
- **No version stamp.** The appendix requires refusing to load on mismatch.
- **The save stores a seed rather than the world** — precisely the case the
  appendix flags as invalidated by any generation change.
- Items persist **by name**, and `Assets/Tests/README.md` notes several
  definitions share an `ItemName` and the format "cannot distinguish these
  variants." Under G2 and G50 (defeat whitelist), name-keyed persistence is not a
  safe substrate for progression items.

### 3.7 Medium-high — passability is one runtime boolean mask from two art layers

§5.2 requires one mask per capability-relevant terrain class plus a corner-cut
class per terrain, carried in the world description; the appendix adds *"area
lock perimeters must be computed and stored, never inferred at runtime."*

`WalkableMap` builds a single `bool[,]` in a TWC callback as
`!(Houses[x,y] || Trees[x,y])`; roads, parks and roofs are ignored. The dungeon
reads its "Floor" blueprint layer per call. There are no semantic terrain
classes, no terrain move costs, no elevation (`z != 0` is rejected outright), and
no per-capability passability of any kind. This is the inverse of §2's "the
simulation never selects a sprite — it emits semantic terrain."

Redeeming detail: `GridMovement.CanStep` takes `Func<Vector3Int, bool>
isWalkable`. Per-capability masks slot into that signature without touching the
step rules. It is the right seam, already there.

Related: `FootPrint.Size3x3` does **not** restrict which tiles a unit may enter —
`CanWalkTo` ignores footprint entirely, and `BigUnitStuckInHallway` applies a
`Stuck` status *after* the illegal move. The spec's chokepoint-width guarantees
(§7.2: "1–3 tiles at the lock site … a 1-tile pass is genuinely one tile") assume
footprint-aware legality.

### 3.8 The places the built game actively inverts a spec principle

§4: shop availability is "gated by tier, **never by price** — a player can grind
past any price." §5.4/§7.5: *"A difficulty wall is grindable, and a grindable
wall is not a wall."* §12: gold "never appears on the required path."

In this game, **gold is the only progression currency, and everything is priced**:

- **Companions cost 300 gold**, hardcoded (`AllyRecruitDialog.cs`). Under the
  spec, companions are the sole source of personal traversal capabilities — so
  the capabilities that gate the world would be purchasable with grindable
  currency. This is the exact failure §4 exists to forbid.
- **Skills cost `Skill.LearnCost` gold** at the ballista (`SkillGridItem`).
- **The shop is 5 random items at `Random.Range(100, 300)` gold**, regenerated
  per overworld load — availability by price *and* by luck.
- **Dungeon tiers are entirely ungated.** `EntranceDialog.Setup` renders every
  `DungeonTierData` as an immediately clickable button, with no requirement
  check anywhere. The game's only progression structure is "deeper floors are
  harder" — the unvalidatable soft difficulty wall the spec removes.
- **Dismissal can empty the party** (`RemoveAlly` guards on `Count >= 1`); §9
  requires an undismissable protagonist, and §9's proof that you cannot strand
  yourself inside an area lock depends on it.

None of these are bugs. They are the right design for a roguelike. They are
simply the design v5 is written to replace.

Compatible by accident: there is no overworld combat, so G47/G48 hold — but
*vacuously*, since the overworld has no gating of any kind, so "capability locks
are the overworld's only gating mechanism" currently guarantees nothing.

### 3.9 Smaller frictions worth knowing before planning

- **Overworld auto-repeat is dead code.** `OverworldPlayer.holdTime` only
  increments when `ControllerHeld` is true, and `ControllerHeld` is never
  assigned anywhere; `repeatTime` is never read. The only throttle is the 0.2 s
  tween, so holding a direction walks at 5 tiles/sec. §5.2's "held input
  auto-repeats at the current movement rate" works in the dungeon
  (`PlayerController`, `repeatTime = 0.1f`) and not on the overworld.
- **No overworld fog or sight range at all** — grep for `Fog|Visible|visibility`
  under `Assets/Scripts/Overworld` returns nothing. §5.7's overworld layer is new
  work, even though the algorithm to do it exists next door.
- **Dungeon room reveal is radius-8 + LOS, not whole-room flood.** §5.7 says
  "the room you occupy is fully visible." Close, but not the same rule.
- **No movement-speed or animation-rate data anywhere.** §5.3's road and vehicle
  rates, and §15's seconds budget, need a per-terrain rate table that does not
  exist; every duration is a hardcoded literal.
- **Nothing tests generation.** There is no connectivity or reachability check,
  no "is the stair reachable from the start" assertion, no seed-reproducibility
  test, no golden-map snapshot, and no generation-time budget. The nearest thing
  is `HarnessSmokeTests.DungeonScenarioLoadsRealSceneAndPlacesAlly`, which
  asserts only that the scene loaded. Test weight is on UI/menu input, sight/fog,
  grid movement, equipment edge cases, and save-store isolation; there is also no
  coverage of combat math, AI, XP, status effects, item effects, throwing, traps
  or the economy.
- **Content catalogs are scene-serialized lists, not asset-discovered.**
  `ItemManager`, `EnemyManager` and `SkillManager` hold `List<ItemDefinition>`,
  `List<Enemy>`, `List<SpawnDefinition>` as inspector fields on scene objects.
  The appendix's *"nothing may read the capability list except through the
  manifest"* has no place to live yet, and a manifest-driven design cannot be
  fed from hand-maintained scene lists.
- **A wave-function-collapse implementation is vendored and entirely unused.**
  `Assets/unity-wave-function-collapse/` has zero references from any scene,
  prefab or script, and `Assets/tiles.xml` is an empty 4-line stub. If anyone is
  counting it as existing capability toward §13's terrain pipeline, it isn't.
- **Dead and duplicated code sits exactly where a rewrite would land:**
  `DungeonGenerator.cs` (629 lines, BSP + Perlin) and `MazeGenerator.cs` are
  unreferenced by any scene or prefab; `Dungeon/Game/Player.cs` (503 lines) is
  entirely commented out; `Dungeon/Game/Dungeon.cs`'s instance methods are dead
  while its statics are live callers; four `WalkableMap` methods reference
  blueprint layers (`"DungeonPosition"`, `"PlayerStartPosition"`, `"Walkable"`)
  that do not exist in `VillageLSystemAsset` and are never called;
  `enum TurnPhase` is never referenced. `EnemyManager`'s editor import commands
  hardcode an absolute path from another machine.

---

## 4. Issues inside the document itself

Independent of this codebase — worth fixing in the doc before anyone builds to it.

1. **§16's provenance line is stale.** "1–45 carry forward from v4 …; 46–53 are
   new in v5" — the table runs to **64**, and §0 itself cites G54 and G57–G64.
   Should read 46–64.
2. **§4.2's role counts can exceed the activation count.** Critical 5–7 plus
   exploratory 4–6 sums to 9–13, but activation yields 9–12 capabilities
   (3–4 + 2–3 + 4–5). The upper ends are jointly unsatisfiable, so validation
   would reject seeds the parameter table appears to permit.
3. **§22's two traversal budgets are in tension by construction.** ≤150 steps
   *and* ≤25 s, at 0.20 s/tile open terrain, means 150 open-terrain steps costs
   30 s — over budget. The pair is only jointly satisfiable if a meaningful share
   of every long leg is road (0.10 s) or vehicle (0.07 s). That may be the
   intent, but as written the seconds budget silently *mandates* road coverage
   rather than measuring it, and a generator can pass §15's pacing check and fail
   this pair with no road-laying bug present.
4. **§3.1's combinatorics check out but are an upper bound.** 126 × 10 × 252 =
   317,520 ✓ (C(8,3)+C(8,4)=126; C(4,2)+C(4,3)=10; C(9,4)+C(9,5)=252), before
   the "≥1 native Area", "≥1 of Boat/Icebreaker", "Engineering always" and "≥1
   narrative" constraints prune it. The doc says "from subsets alone," which is
   fair, but the real number is meaningfully smaller.
5. §5.7 and §22 are consistent (sight 48 > vantage 40 ✓), and §15's check count
   is correct at thirteen. Both are worth keeping as asserted invariants rather
   than adjacent table rows.

---

## 5. Scorecard

| Spec area | Compatibility | Why |
|---|---|---|
| §5.1 shared grid | **High — built** | `GridMovement` shared by all four consumers |
| §5.2 8-way, one-input-one-tile | **High — built** | discrete steps, `_busy` gate, 0.2 s tween |
| §5.2 three corner-cut classes, per-terrain, forced Sealing | **Low** | 2-value enum, call-site arg, overworld defaults to Open |
| §5.2 held-input auto-repeat | **Medium** | works in dungeon; overworld repeat gate is dead code |
| §5.3 no overworld simulation | **High** | nothing ticks on the overworld |
| §5.3 road / vehicle animation rates | **None** | no per-terrain rate data; all durations hardcoded |
| §5.4 no overworld combat | **High but vacuous** | no combat *and* no gating |
| §5.7 visibility | **High (dungeon) / None (overworld, town)** | `DungeonSight` + three-state fog reusable; overworld has none |
| §14.2 tactical mode | **High — built** | two-phase turn engine with response cascade |
| §14.5 full tactical party | **High — built** | 4 allies as turn actors |
| §14.6 defeat whitelist | **Medium** | ejection to overworld exists; loss rules need auditing vs. G50 |
| §14.9 None-depth buildings | **High — built** | `OverworldBuilding.Interact` → dialog |
| §2 engine independence + headless harness | **None** | one Unity assembly; editor-only harness |
| §13 deterministic 13-stage pipeline | **Low** | MonoBehaviour `Start()` + TWC layers, global RNG |
| §2 determinism from a seed | **Low** | only the village is seeded; dungeon floors use `TickCount`; seeding mutates the asset |
| §14.3 persistent vs. regenerating dungeons | **Low** | all floors regenerate; no seed+visit derivation, so not reproducible either |
| §15 thirteen validation checks | **None** | no world model to validate; zero generation tests today |
| §3 capability model | **None** | zero occurrences in code |
| §4 regions / tiers / progression graph / solution sets | **None** | — |
| §4 tier-gated, never price-gated | **Inverted** | companions, skills and shop stock are all priced in gold |
| §6 lock-and-key interaction, Field Inventory, legibility | **None** | — |
| §7 lock forms, reservations, thresholds, vantage | **None** | — |
| §8 vehicles, docks, navigable zones, enclaves | **None** | — |
| §9 roster, towns-only dismissal, undismissable protagonist | **Low** | no bench; party can be emptied |
| §10 knowledge layer, journal, rumors, learned classes | **None** | — |
| §11 themes, signature landmarks, naming | **None** | — |
| §12 append-only progression | **Inverted** | level/EXP reset every run; 6-field save, no version stamp |
| §14.7–14.12 towns | **None** | no town scale, no NPCs, no dialogue |
| §22 scale parameters | **Low** | 15×15 vs 256×256; 0 towns vs 6 |

---

## 6. Recommendation

**A. Adopt v5 as a new game built on this game's dungeon.** Recommended if the
ambition is real. Hard prerequisites, in order, before any Phase 0 content work:

1. **Extract `EternalEnigma.Core`** — a no-Unity-reference assembly with a
   build-time check that it cannot reference the engine. Port `GridMovement`,
   `DungeonSight`, `AStar` and `BFS` onto a core `Coord` struct instead of
   `Vector3Int`/`Mathf`, leaving thin Unity adapters behind. These four files are
   the cheapest possible proof that the boundary can hold, and they are already
   the most portable code in the repo.
2. **Replace corner-cut policy arguments with a per-tile corner-cut class** in
   the world description, and add the missing **Permissive** class. Promote
   `GridMovementTests` into G55's diff test — resolver vs. walker on a generated
   fixture — rather than a note in a document.
3. **Introduce a seeded RNG with per-stage streams** (activation = stream 0);
   retire the global `UnityEngine.Random`, both `Guid.NewGuid()` shuffles, and
   asset-mutating seed injection. A seed must be an argument, not a write to a
   ScriptableObject.
4. **Stand up the headless validator** as a plain `dotnet test` assembly over
   `EternalEnigma.Core`, running in CI beside the existing player builds. Until
   this runs, the document's central claim is unenforced.
5. **Version-stamp saves** and move progression state off name-keyed strings.

Then Phase 0 → 1 → 2 → 3 as written, adapting the existing dungeon as Phase 4's
already-built payload. Note that step 1 reframes the project: terrain stops
coming from TileWorldCreator and starts coming from portable code, with TWC
demoted to a rendering aid at best. That is a real loss of leverage on an asset
the project currently leans on hard, and it should be a conscious decision rather
than a consequence.

**B. Absorb only what this game can carry now.** Keep the hub-scale overworld and
the existing dungeon; add capability locks, the Field Inventory, fiction-first
lock descriptions, the three failure-feedback tiers, and learned lock classes at
village scale — and use capabilities rather than gold to gate the dungeon tiers
that are currently ungated (§3.8). This buys pillars 3, 8 and 9 — the parts about
*feel* — for a small fraction of the cost, and it turns the existing tier
selection into a real gate. It abandons pillar 2 (provability), which is the
document's reason for existing.

**C. Shelve the document.** Defensible. The built game is a competent, shipping
Mystery Dungeon roguelike, and v5's overworld is a different genre wearing the
same grid.

My recommendation is **A, with step 4 as an explicit go/no-go gate**: if the
headless validator is not running in CI by the end of the first milestone, the
project has taken on all of v5's cost and none of its guarantees, and should fall
back to B.
