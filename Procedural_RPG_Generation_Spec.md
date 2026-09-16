## Procedural RPG Generation — Design Document v5

Supersedes `Procedural_RPG_Generation_Spec_v4.md`, and through it v3, v2, v1,
`Procedural_RPG_World_Generation_Plan.txt`, and
`Procedural_RPG_Capability_System_Addendum.txt`. Where those conflict with this
document, this one wins.

v1 proved every world works. v2 made every world different, tolerable to
backtrack through, and worth remembering. v3 made the player meet the world
through fiction rather than requirement checks. v4 gave every lock a place.
**v5 specifies how the player moves, and splits the game into two modes on one
grid.**

This closes the last structural hole. Every prior version described a world
without saying how it is traversed, which left several claims resting on nothing
— "overworld distance travelled" in the pacing budget, "free movement" in
navigable zones, "repeatable way to gain power" in guarantee 17. v5 makes all
three concrete, and one of them turns out to have been an inconsistency rather
than a gap.

Design-level. Combat math still deliberately deferred — v5 specifies *when* time
advances, not what a hit does.

---

## 0. Changes in v5

| Change | Why | Where |
|---|---|---|
| **One grid, two modes.** Tile-based movement everywhere. The overworld advances no simulation; dungeons are fully turn-based, Mystery Dungeon style. | Consistent controls, deterministic pathfinding, precise lock interaction — and each layer gets to be good at one thing. | §5 |
| **8-way movement; corner-cutting is a declared property of terrain and layer.** Three classes — Sealing, Permissive, Open — with per-layer defaults, and reserved structure forced to Sealing. | Preserves control consistency while letting terrain decide how tightly a corner blocks: a cliff should seal, a pillar should be squeezable. Perimeter sealing becomes a local assertion instead of a global assumption. | §5.2 |
| **Combat is dungeon-resident. The overworld has none.** | Therefore **capability locks are the overworld's only gating mechanism**, and all overworld gating is provable. No soft difficulty walls, no "you may go there but monsters will kill you." This makes the design's central claim stronger than it was. | §5.4 |
| **Guarantee 17 gets a realization: every tier needs a re-enterable dungeon.** | "A repeatable way to gain power" was abstract. With no overworld combat, repeatable power is necessarily dungeon-resident, which is a real new generation constraint. | §14.4, G49 |
| **Story dungeons persistent; grind dungeons regenerating.** | Mystery Dungeon convention regenerates floors per entry, which would break determinism and let validated capability sources move. Split by function: validated dungeons are fixed, source-free repeatable dungeons regenerate. | §14.3 |
| **Defeat ejects; it never removes progression.** | The exact point where a roguelike convention would break guarantee 1. Specified rather than discovered. | §14.6, G50 |
| **"Free movement" in navigable zones corrected.** v2 promised sailing animations and free movement; under a universal grid that was wrong. Zones are *topologically* free — any tile, no rails — and vehicles move multiple tiles per input. | An inconsistency I introduced, not a new constraint. | §8.1 |
| **Roads and vehicles speed up the *animation*, never the tile count.** One input is always exactly one tile, everywhere, for everyone. | A 256×256 grid walked tile-by-tile has a real traversal cost that unspecified movement hid — but multi-tile steps would fork the game into two grid geometries, and a single shared geometry is what every proof in §15 rests on. | §5.3, §8.1 |
| **Town architecture: one continuous town map, buildings are enclosures on it.** No sub-maps below the town level; interiors are cut into the town grid inside their own footprints. Three interior depths — None, Minimal, Full — so residences stay cheap and important buildings get room. | Consistent spatial representation, no transition inside a town, and no generating thousands of tiles of uninteresting bedroom. Filed under §14 because a town is an interior by §14.1's definition. | §14.7–14.11 |
| **A visibility model, stated for all three layers.** Overworld sight-ranged and explored-forever; towns occluded by building walls; dungeons room-based. | Concealing a building's contents until entry requires a vision system the document was using implicitly — §7.6's vantage sightlines already assumed one. | §5.7 |
| **Town role moves upstream.** Role is assigned by the *overworld* pipeline before placement, not by the town generator. | A port town must be sited on a coast with a dock; the town generator learns its role too late to influence where it was put. A correction to the drafted ordering. | §14.10, G63 |
| **Building walls are reserved structure at town scale**, forced Sealing, with doors as their only thresholds. | The corner-cut work pays off concretely: nobody slips diagonally into a locked house through a wall corner, and it is the same perimeter-plus-thresholds mechanism as area locks and navigable zones. | §14.8, G60 |
| **Traversal budget becomes a validation check (13th)**, measured twice — in **steps** and in **seconds**. | Pacing could now be measured instead of gestured at. A world can be perfectly paced in *events* and still be forty minutes of walking per capability. Because speed is animation-only, roads cut the seconds and never the steps, so the two numbers fail independently. | §15 |
| **Pillar 10.** | One grid, two questions. | §1 |

---

## 1. Pillars

1. **Generate progression first, then geography.** Geography realizes a
   validated progression structure; it never discovers it.
2. **Every world is winnable.** Provable by construction, not by playtesting.
3. **Progression creates exploration.** New capabilities reopen old places.
4. **No companion is mandatory.** Gates check capabilities, never identities.
5. **The world feels larger than it is.** Discovery of routes, enclaves, and
   optional content outpaces the map's actual size.
6. **Seeds differ in structure, not only in placement.** Which capabilities
   exist, and which of them matter, is itself generated.
7. **Meaning is derived, not authored per seed.** Themes, names, landmarks, and
   eventually history are read off the progression graph, so they can never
   contradict it.
8. **Obstacles are fiction; requirements are infrastructure.** The player meets
   a boulder, never a requirement. Neither layer knows the other's vocabulary.
9. **A lock is a place.** Every requirement occupies real coordinates the player
   can stand in front of, see from a distance, and remember afterward.
10. **One grid, two questions.** The overworld asks *where can I go?* and
    advances nothing. The dungeon asks *can I survive?* and advances on every
    action. Same controls, different rules, no overlap in purpose.

---

## 2. Two halves

The simulation is **engine-independent**. It takes a seed and produces a
complete, validated world description. Unity is a **presentation layer**
consuming valid world descriptions and rendering them.

The simulation never selects a sprite — it emits semantic terrain and Unity
decides how it looks. Presentation never mutates the world; player progress is a
separate layer laid over it.

Design constraints:

- The world is generated **once, whole, at new game.** No streaming —
  validation needs the complete structure.
- Generation is **deterministic from a seed**; worlds are shareable, bugs
  reproducible.
- A headless harness generates and validates thousands of worlds with no engine
  running. The project's primary safety net.

The world description carries:

- the **activation manifest** (§3.1) — presentation never assumes a capability
  exists;
- the **lock skin assignment** (§6.6) — presentation renders fiction, never
  chooses it;
- the **lock realization table** (§7.2) — every lock's form, coordinate,
  footprint, thresholds, perimeter, vantage points;
- the **movement grid and passability layers** (§5.2) — one passability mask per
  capability-relevant terrain class, which is what makes the
  capability-parameterized reachability walk possible at all.

---

## 3. Capability model

Capabilities have a **persistence kind** (generation), a **presentation
category** (§3.2, player-facing), and a **native lock form** (§7.1, terrain).

| Kind | Examples | Source | Persistence |
|---|---|---|---|
| **Personal traversal** | climbing, teleportation, breaching | companion only | held only while that companion is in the party |
| **Vehicle traversal** | boat, icebreaker, airship | built at a converter site | permanent world state once built |
| **Utility** | engineering, translation, royal authority, remedy | companion, item, spell, quest, or story event | permanent once first acquired |

- **Personal traversal is party-bound**, so party composition is a real
  exploration decision.
- **Vehicles are built objects.** You don't un-build a boat because the engineer
  left.
- **Utility latches** so a translation door deep in a dungeon never becomes a
  cross-map fetch trip.

### 3.1 Pool and activation

The vocabulary is **fixed and closed**; it is a **pool**, and each seed
**activates a subset**.

**Personal traversal pool** (8):

| Capability | Locks it answers | Native form |
|---|---|---|
| Climb | cliff faces, escarpments, walls | Obstacle or boundary Area |
| Grapple | chasms, gaps, breaks with an anchor beyond | Obstacle |
| Icewalk | frozen expanses, shallows | Area |
| Deep Dive | flooded basins, submerged passages | Area |
| Breach | boulders, rockfalls, collapsed mines, thorn walls, cracked masonry | Obstacle |
| Waystone Step | paired teleport stones flanking a barrier | Obstacle |
| Phase Shift | ward-fields, spirit barriers | Area or Obstacle |
| Hazard Ward | toxic fog, spore bloom, heat waste, cursed ground, sandstorm | Area |

Hazard Ward is one capability rather than four because its skins are **overlay
fields on ordinary terrain** sharing one visual treatment, keyed by region theme.

**Vehicle traversal pool** (4) — each an Area lock with discrete thresholds:

| Capability | Medium | Built from |
|---|---|---|
| Boat | open water | shipyard + ship parts |
| Icebreaker | frozen sea | shipyard + reinforced hull |
| Airship | sky, enclaves | skydock + lift core |
| Tunneler | subterranean routes | delve works + boring head |

**Utility pool** (9) — all Interaction locks:

| Capability | Problems it answers |
|---|---|
| Engineering | construction sites, mechanisms, converters, collapses |
| Translation | inscriptions, sealed archives, unfamiliar script |
| Royal Authority | guarded borders, official gates, checkpoints |
| Ancient Attunement | relic seals, resonant doors |
| Beast Speech | wilderness passage, animal guides, blocking creatures |
| Guild Standing | token purchases, closed markets |
| Remedy | the injured, the sick, the poisoned, contaminated water |
| Diplomacy | feuds, refusals, hostile welcomes, standoffs |
| Sacred Rite | the unquiet, the cursed, the unconsecrated, the haunted |

Twenty-one total. A seed activates **3–4 personal traversal** (≥1 with a native
Area form), **2–3 vehicle** (≥1 of Boat or Icebreaker), **4–5 utility**
(Engineering always, plus ≥1 of Remedy / Diplomacy / Sacred Rite).

Unactivated capabilities are **absent entirely**: no lock requires them, no
companion provides them, no barrier or field of their type is placed. Activation
runs at stage 0, before any reservation. Structural seed shapes from subsets
alone: 126 × 10 × 252 ≈ **317,000**.

### 3.2 Presentation categories

Orthogonal to persistence and lock form. Organizes the Field Inventory (§6.2)
and keys the skin tables (§6.6). No effect on generation or validation.

| Category | Understood as | Kinds | Typical forms |
|---|---|---|---|
| **Traversal** | acting on the physical world to pass through it | personal, vehicle | Obstacle, Area |
| **Utility** | knowledge or craft applied to a world problem | utility | Interaction, Obstacle |
| **Narrative** | acting on people and world state | utility | Interaction |

Remedy, Diplomacy, and Sacred Rite are **utility-kind** capabilities in the
Narrative category; they latch permanently. The capability is the *craft*, not
the dose — you know how to treat the injured, and you do not run out of knowing.

### 3.3 Cost of the pool, stated honestly

Eight barrier families, four navigable media, twenty-one skin tables, area-lock
field art. Mitigations: barrier **families** share transition sets; area locks
are largely **overlays** on existing terrain, with only the perimeter bespoke;
**Interaction locks remain the cheapest gating in the document** — text, a
portrait, a stationary actor; and activation is **parameterized per build**, so
the pool is a dial, not a commitment.

---

## 4. World structure

**Node types:** start town, town, dungeon, region, dock, converter, landmark,
secret, final dungeon.

**Converters** consume items *and* capabilities to produce a capability. Ship
parts plus engineering at a shipyard yields the boat. Converters are how a
capability feels earned rather than found, and under §5.5 they are the
overworld's only terminal that is not a dungeon.

**Edges:** walking routes realized as terrain corridors; vehicle routes realized
as navigable zones between docks.

**Progression tiers** (5) order the world. Every source of a capability sits at
or above its capability's tier, with at least one at exactly that tier. Shop
availability is gated by tier, **never by price** — a player can grind past any
price. §7.5 is this principle applied to terrain; §5.4 is it applied to combat.

### 4.1 Gates are solution sets

A gate is a **set of solution sets, each a conjunction**; passable if **any one**
is satisfied. Disjunctive normal form.

| Solution set | Reads as |
|---|---|
| `{Breach}` | you shatter the fall |
| `{Engineering}` | you shore the roof and clear a path |
| `{Waystone Step}` | you step past it entirely |

Cost to the validator: solvability satisfies a gate on any set; no-softlock needs
one set satisfied *entirely* by guaranteed sources; route width is a *minimum
over sets*; exploratory independence needs every required lock to retain a fully
critical set; the spoiler log becomes minimum path plus per-lock alternates.

Bounds so the DNF does not explode: **1–3 solution sets per lock**, each of
**1–2 capabilities**. Required locks carry ≥2 where the manifest allows.

### 4.2 Capability roles

| Role | Meaning | Count |
|---|---|---|
| **Critical** | may appear in the only-critical solution set of a required lock | 5–7 |
| **Exploratory** | never necessary; gates optional content, and appears as an *alternate* solution on required locks | 4–6 |

Roles **rotate between seeds** — Breach is load-bearing in one, purely
exploratory in the next. With 5–7 critical of 9–12 active, roughly half the
graph hangs off the spine as genuine optional branches. Provable with one extra
solvability run (§15).

Constraints: Engineering always critical; ≥1 vehicle critical (≥1 exploratory if
three are active); one or two personal traversal capabilities may be critical,
never all; every active capability solves ≥2 locks and is the **only active
solution** to ≥1.

### 4.3 Boundary equivalence

A region boundary carries an **intended requirement** and is realized as one or
more physical crossings. Its **effective requirement** is the *disjunction* of
its crossings' requirements. **Validation asserts effective ≡ intended.**

- A boundary with a gated pass and one ungated goat track has effective
  requirement `{}`. The tier is deleted, and everything downstream still
  validates — the world is winnable, just not in the order anyone designed. The
  most destructive bug the pipeline can produce.
- Multiple crossings are **welcome** when each is gated equivalently or is an
  intentional alternate registered in the graph.
- Crossings discovered *after* terrain generation — a chasm narrow enough to
  walk, a lake shallow enough to ford — are bugs, and the
  capability-parameterized map walk (§15) exists to find them.

---

## 5. Movement and modes

New in v5. The game uses a grid at every scale, but the overworld and dungeons
run different rules and serve different purposes.

Goals: consistent spatial controls across the whole game; fast overworld
exploration focused on discovery; turn-based tactics reserved for dungeons;
precise capability and lock interaction; simple deterministic movement rules that
procedural generation can reason about.

### 5.1 Shared grid foundation

Both layers are **tile-based**. The player moves tile to tile; there is no
continuous free movement anywhere in the game. This buys four things the design
has been quietly assuming:

- **Precise positioning** — a lock occupies tiles, and "in front of it" is
  exactly defined.
- **Clear capability interactions** — a threshold is a tile set, not a trigger
  volume.
- **Deterministic pathfinding** — the validator walks the same graph the player
  does, which is the whole basis of §15's map reachability check.
- **Consistent controls** between exploration and combat.

### 5.2 Movement rules

**One input is one tile. Always, everywhere, for everyone.** There is no
multi-tile step, no movement that covers two tiles at once, and no exception for
roads, vehicles, or any capability. The grid has exactly one geometry.

This is the load-bearing rule of the whole section. A road that moved two tiles
per input, or a boat that moved three, would give the game two step-lengths — and
then reachability, chokepoint width, perimeter sealing, adjacency, vantage
distance, and the traversal budget would each mean something different depending
on who was asking. Every proof in §15 walks the grid one tile at a time, and the
player must walk the same grid the validator does.

**Speed is animation rate, never step size.** Faster movement means the
tile-to-tile walk animation plays faster and the next input is accepted sooner.
Nothing about position, distance, or legality changes. A road is not shorter; it
is quicker to walk. This is entirely a presentation-layer property (§2) and the
core never knows about it.

**Held input auto-repeats** at the current movement rate. This is what makes
animation-only speed actually feel fast: a player crossing a region holds a
direction and the steps come as quickly as the current terrain's rate allows,
so a road reads as a road rather than as identical key presses with a different
sprite. Discrete taps remain exact for positioning at locks and in tactical
mode.

**8-way movement.** Diagonals are available in both modes, which is what keeps
pillar 10's promise that controls do not change between overworld and dungeon.

**Corner-cutting is declared, not global.** Whether a diagonal step may squeeze
past an impassable corner is a property of the *blocking terrain*, with a default
per layer. Each terrain class declares one of three:

| Class | Diagonal past one blocker | Diagonal between two blockers | For |
|---|---|---|---|
| **Sealing** | forbidden | forbidden | cliffs, chasm edges, area lock perimeters, enclave rings, all reserved barriers |
| **Permissive** | allowed | forbidden | trees, rubble, pillars, furniture, crowds, most dungeon walls |
| **Open** | allowed | allowed | low walls, fences, decorative blockers that stop walking but not slipping past |

Resolution for a diagonal step: the destination must be passable, and each of the
two orthogonal neighbours is consulted; **the most restrictive blocker wins.** One
Sealing blocker forbids the step outright; two Permissive blockers forbid it; one
Permissive blocker with a passable neighbour permits it.

Layer defaults:

| Layer | Default | Why |
|---|---|---|
| **Overworld** | Sealing | every reachability proof lives here, so the safe default is the strict one |
| **Dungeon** | Permissive | squeezing diagonally past a pillar is real tactical vocabulary, and no proof depends on dungeon sealing — interiors have no traversal gates (G10, G52) |

Terrain may override the layer default in either direction, which is the point of
making this declarable: an overworld thicket can be Permissive, and a dungeon's
interior area lock perimeter must be Sealing.

**Reserved structure forces Sealing, and that is not overridable.** Area lock
perimeters, enclave rings, reserved barriers, and chokepoint walls are assigned
the Sealing class at reservation time (stage 5) regardless of what terrain class
the tile would otherwise carry. This is what preserves the v4 appendix's concern
— *a perimeter sealed against orthogonal movement is not sealed against diagonal
movement* — while still allowing permissive corners everywhere they are harmless.
The difference from a global prohibition is that sealing is now a **local,
checkable property of specific tiles** (guarantee 54) rather than a global
assumption that one movement-code change could silently invalidate everywhere at
once.

**Passability is per-capability.** The world description carries a passability
mask per capability-relevant terrain class rather than one "walkable" bitmap,
plus the corner-cut class per terrain. Movement legality is
`mask[terrain] ⊆ player capabilities`, then the corner rule. This is what makes
area locks expressible and the parameterized reachability walk cheap.

### 5.3 Overworld exploration mode

**The overworld is not turn-based.** Movement is on the grid, but **no global
simulation advances while travelling.** Absent from overworld play:

- enemy turns
- time advancement, day/night, schedules
- hunger, fatigue, resource decay
- wandering monster movement
- active tactical combat

The player moves tile-by-tile at their own pace. The overworld's purpose is
exploration, navigation, capability discovery, lock identification, route
planning, and town interaction. Its central question is **"where can I go?"**

**Overworld travel therefore costs nothing but the player's own time and
attention.** That has a consequence worth naming: the *only* cost of
backtracking is player patience, which is exactly why §10.3's caps — 2–5 places
reopened per capability, fewer than 8 outstanding locks, one payoff within a
region of the source — are load-bearing rather than polish. In a game with
overworld attrition, a bad backtracking design is expensive; in this one it is
merely boring, and boring has no error message.

**Speed.** A 256×256 grid walked one tile per input has a real traversal cost
that unspecified movement was hiding. Three mitigations, all already in the
design and now doing work — and note that only the third changes the number of
steps:

| Mechanism | Effect | Steps | Seconds |
|---|---|---|---|
| **Roads** | faster walk animation and auto-repeat rate along roads, trails, bridges | unchanged | reduced |
| **Vehicles** | fastest animation rate, inside a navigable zone (§8.1) | unchanged | reduced |
| **Fast travel** | instant to any visited town (§9) | skipped entirely | zero |

Because roads cut seconds and not steps, **the shortest path and the fastest path
differ** — which is the cheapest possible source of interesting overworld
navigation, and it finally gives pipeline stage 8's road-laying gameplay meaning
rather than just readability.

It also means step count is an independent cost that speed cannot buy off. A
400-tile detour on a fast road is still 400 steps of the player's attention, even
if it is half the wall-clock. That is why §15's traversal budget measures both,
and why fast travel — the only mechanism that removes steps — carries more weight
in the budget than the other two combined.

### 5.4 No overworld combat, and why that strengthens the design

Combat is **dungeon-resident**. The overworld contains no encounters, no
wandering enemies, and no fixed fights.

The consequence is larger than it looks: **capability locks are the overworld's
only gating mechanism, and every overworld gate is therefore validated.** There
are no soft difficulty walls — no "you can walk there, but the monsters will
kill you," which is the standard RPG method of implying a boundary and the least
provable one in existence. A difficulty wall is grindable, and a grindable wall
is not a wall (§4, §7.5). Removing overworld combat removes the last
unvalidatable gate from the map.

What that buys, concretely:

- The reachability proofs in §15 describe the world the player actually
  experiences, with no "technically reachable but practically lethal" caveat.
- A player who walks to a tier-4 dungeon entrance at tier 2 has done nothing
  wrong and nothing has broken — the locks permitted it, so it was intended. The
  dungeon itself is the difficulty wall, and §14.6 guarantees losing there costs
  no progression.
- The overworld's difficulty band parameters (5, one per tier) apply to
  **dungeon contents only**, which is what §15's power band check has always
  actually been measuring.

### 5.5 The core loop

```
Explore overworld
      ↓
Discover a lock                  ← fiction-first (§6), annotated (§10)
      ↓
Locate its answer                ← rumors and journal point the way
      ↓
Conquer a dungeon                ← turn-based, resource-managed (§14)
   or complete a converter        ← the overworld's other terminal (§4)
      ↓
Acquire a capability
      ↓
Return to the lock
      ↓
Unlock a region, a route, or a secret
      ↓
Repeat
```

The overworld **drives** progression; dungeons and converters **grant** it. Two
terminals rather than one is deliberate: an all-dungeon loop makes every
capability a reward for combat, and converters are what make a capability feel
*built* rather than *looted* (§4). Target mix is a parameter (§22), not a law.

This loop is the reason the two modes do not compete. The overworld asks a
question it cannot answer; the dungeon answers a question it never asks.

### 5.6 Locks under a grid

Grid movement makes lock interaction exact, which several earlier sections
needed and none could specify:

- **Obstacle and Interaction locks** present their fiction when the player
  occupies a tile orthogonally adjacent to the lock tile, and invocation
  resolves from there.
- **Area locks** present their fiction on attempted entry to a perimeter tile —
  the threshold *is* the perimeter tile set (§7.4). Entry is refused, with
  description, until the capability is invoked.
- **Vehicle area locks** present at their docks, which are discrete threshold
  tiles.

Adjacency uses the same corner-cut rule as movement (§5.2), and because lock
sites and chokepoint walls are forced to the Sealing class, a lock in a diagonal
pinch can never be interacted with — or slipped past — through a wall corner.

### 5.7 Visibility

The document has been assuming a vision model since §7.6 required a lock to be
*visible* from a vantage point. v5 states it, because §14.9 needs it to conceal a
building's contents and the three layers want different answers.

| Layer | Model |
|---|---|
| **Overworld** | Terrain-occluded line of sight at long range (≥ the vantage distance, §22). Explored tiles stay on the map permanently; unexplored map is blank. |
| **Town** | Line of sight occluded by building walls. A building's exterior, size, and entrances are always visible; its contents are not, until entered (§14.9). |
| **Dungeon** | Room-based. The room you occupy is fully visible; corridors reveal only adjacent tiles. Standard Mystery Dungeon vocabulary, and it is what makes a corridor tense. |

Two things follow that matter elsewhere:

- **Map knowledge is append-only** (§12). Explored terrain, seen buildings, and
  revealed dungeon floors are never un-revealed. This is the same rule as
  capabilities and learned lock classes, for the same reason.
- **Overworld sight range must exceed the vantage distance**, or §7.6's promise
  fails silently: a lock reserved with a perfect sightline is useless if the
  player cannot see that far. The two numbers are coupled and live together in
  §22.

Concealment is **visibility only, never knowledge erasure.** A building the player
has entered stays revealed; a dungeon floor once mapped stays mapped. Fog that
re-closes behind the player would be a removal of progression state in everything
but name.

---

## 6. Lock-and-key interaction model

Progression remains capability-based and validated as specified; capabilities are
*presented* as solutions to world problems rather than automatic gate checks.

### 6.1 Lock discovery

On attempting a blocked route or interacting with an obstructed object, the game
describes the problem in fiction:

```
A large boulder blocks the way.
A tangled wall of thorns bars the trail.
An unfamiliar inscription covers the stone gate.
A guard refuses to allow passage.
The valley floor is blanketed in a pale, stinging fog.
```

The game does **not** reveal which capability is required.

| Good | Poor |
|---|---|
| `A collapsed mine entrance blocks the tunnel.` | `Requires Engineering.` |
| `The river runs fast and deep here.` | `Requires Boat or Icewalk.` |
| `The shrine's seal has not been spoken to in an age.` | `Requires Ancient Attunement.` |

The obstacle exists in the fiction first. Requirements exist underneath, for
generation and validation only.

### 6.2 Capability invocation

The game never prompts `Use Breach? [Yes] [No]`. The player opens the **Field
Inventory** and chooses:

```
A large boulder blocks the way.

  Field Inventory
    Traversal    Climb · Breach
    Utility      Engineering · Translation
    Narrative    Remedy

  > Breach

You shatter the boulder.
```

The Field Inventory lists exactly what is usable now: held utility capabilities,
built vehicles, and the personal traversal capabilities of the **currently active
party**. A benched companion's capability is **not listed and not greyed out** —
a greyed entry is a requirement display with extra steps, and it leaks the pool.
§10.2 solves the benched case with knowledge, not UI.

Because the overworld advances nothing (§5.3), opening the Field Inventory and
reasoning about an obstacle is **free**. That is the mechanical basis for the
adventure-game feel: deliberation carries no cost, so deliberation is where the
interest lives.

### 6.3 Failure feedback

| Tier | When | Example |
|---|---|---|
| **Irrelevant** | no purchase on this lock | `There is nothing here to translate.` |
| **Relevant, insufficient** | right instinct, wrong tool | `You could climb the rockface beside it, but the path beyond is buried.` |
| **Near miss** | correct category, acknowledged | `The traveler needs more than encouragement.` |

Irrelevant responses keep brute-forcing cheap but boring; the other two let a
player reason. Give enough to reason with, never the answer.

Failed invocation **costs nothing** — no time, resource, or penalty, and on the
overworld there is no clock for it to cost anyway.

### 6.4 Legibility

v2: reachability is not playability. v3: **playability is not legibility** — a
world can be provably winnable and still present a boulder the player never
works out.

| Flag | Meaning | Where allowed |
|---|---|---|
| **Plain** | solution obvious from the description to anyone holding it | anywhere; **required** on the critical path |
| **Inferable** | solvable by reasoning, not recognition | required path only once the class is learned (§10.2) |
| **Obscure** | genuinely a puzzle | optional content and secrets only |

Area locks need extra care: "a pale, stinging fog" does not announce Hazard Ward
the way a boulder announces Breach. Area-lock skins must carry their cue in
description and visual — the fog thins visibly around a warded shrine, the frozen
shallows show a safe rime-path. They tend to sit at Inferable, which is why
§10.2's learning model and §7.6's visibility rule matter more for them.

### 6.5 Items in solution sets

- An item may appear **only** alongside a capability, never alone.
- On the **required path**, an item needs a **repeatable, reachable,
  tier-appropriate source**. One-shot items are barred.
- Consumable-bearing sets are always **alternates**; guarantee 30 requires a
  non-consumable guaranteed set regardless.
- Progression *capabilities* remain non-consumable, non-sellable, non-droppable,
  non-equippable. Explosives are ammunition, not progression.

### 6.6 Narrative skins

Many locks validate against one capability. `Remedy`:

```
An injured traveler blocks the bridge.
The village elder has fallen gravely ill.
The town well is contaminated.
```

Skins come from authored tables keyed by **capability × lock form × region theme
× local terrain**: a boulder in a mountain pass, a rockslide on a cliff road,
thorn growth on a swamp trail, a guard checkpoint at a city gate, an
untranslated inscription in ancient ruins. The lock must look native to where it
stands.

**Skin selection happens at generation time and is stored** (stage 9).
Presentation renders; it never chooses. Two players on one seed must see the same
boulder, and the spoiler log must name what is on screen.

### 6.7 What this model is not

It is **not a puzzle game.** The player should usually know the answer on reading
the obstacle; the pleasure is in *applying* the right tool deliberately.
Hard-to-solve locks are a garnish confined to optional content by §6.4. A design
drifting toward puzzle-first fails the pacing checks (§15) long before a
playtest.

---

## 7. Locks and their realization

A lock is **anything that prevents progression until the player possesses and
correctly applies one or more capabilities.** A chokepoint obstacle is one
realization, not the definition.

### 7.1 Three lock forms

**Obstacle locks.** A discrete object blocks a narrow passage.

```
========== Mountains ==========
              |
       [ Boulder ]
              |
========= Next Region =========
```

Boulder, rockslide, thorn wall, collapsed tunnel, crystal growth, rubble pile,
sealed gate, ancient mechanism. Native to chokepoints. Best legibility of the
three, so they are the backbone of required-path gating.

**Area locks.** The terrain *is* the lock.

```
========== Region A ==========

~~~~~~~ Stinging Fog ~~~~~~~~

========== Region B ==========
```

Poison swamp, spike field, lava wastes, frozen tundra, flooded basin,
ward-field, open water, sky. Native to whole stretches of map. Best at region
identity — players remember "the Pale Valley," not "the boulder."

**Interaction locks.** A person, object, or situation; the space is physically
open.

```
Ancient Inscription -> Translation
Sick Villager       -> Remedy
Broken Lift         -> Engineering
Border Guard        -> Royal Authority
Refugee Camp        -> Diplomacy
```

Cheapest form by a wide margin, and the only one that gates with a *face*.

The progression graph cares about none of this. `Region A → capability → Region
B` is the same edge in all three cases.

```
Lock {
  form:        Obstacle | Area | Interaction
  solutions:   [ {caps...}, ... ]        // §4.1
  site:        coordinate | footprint    // §7.2
  thresholds:  [ tile... ]               // §7.4, Area only
  skin:        id                        // §6.6
  legibility:  Plain | Inferable | Obscure
}
```

### 7.2 Reserved lock sites

Lock placement is driven by progression structure **before** terrain decoration.
When the graph marks a boundary gated, the terrain generator must reserve a
physical realization capable of hosting that lock.

Stage 5 reserves: corridor geometry; barrier geometry; dock locations;
**chokepoint geometry and lock sites**; **area lock footprints with perimeters
and thresholds**; navigable zones; signature landmark footprints; **vantage
points** (§7.6).

A **chokepoint** is a narrow passage connecting two traversable areas that can be
blocked: mountain pass, bridge, canyon crossing, cave entrance, forest trail,
city gate, dock entrance, tunnel, ruined causeway, valley mouth. Chokepoint
geometry is *generated to host the lock* — the pass is narrow because a lock
needs to sit in it. Under §5.2, "narrow" is now exact: 1–3 tiles at the lock
site, with corner-cutting prohibition guaranteeing a 1-tile pass is genuinely
one tile.

### 7.3 Perimeter integrity

An area lock's boundary must be **sealed**: every perimeter tile is impassable
terrain, map edge, another lock, or a declared threshold. Nothing else.

The enclave-ring problem generalized, and leakier, because an enclave ring is
drawn deliberately while an area lock's perimeter is wherever the field ends. A
three-tile gap between a lava waste and the cliff it abuts is a free pass
through a tier and validates clean under every pre-v4 check.

- Area locks spanning a **region boundary** must span it completely, sealed edge
  to sealed edge. A hazard belt covering 90% of a boundary is scenery with a gap
  in it.
- Area locks may **abut and compose**. A lava waste meeting a flooded basin
  seals both perimeters at the join; crossing the pair requires both
  capabilities, which counts toward route width (§9.1) and is therefore
  spine-illegal by guarantee 22 and excellent for secrets.

§5.2's Sealing class is what makes this checkable: because every perimeter tile
is forced to Sealing at reservation time, orthogonal closure is sufficient
closure, and the perimeter test is a flood fill. The check has two halves —
assert the loop is orthogonally closed, *and* assert every tile on it actually
carries the Sealing class. The second half is the one that catches a permissive
thicket that terrain generation grew into the ring.

### 7.4 Thresholds

An area lock with no interaction point is a silent automatic check — precisely
what §6 abolishes. Every area lock has **thresholds**.

| Kind | Used by | Behavior |
|---|---|---|
| **Continuous** | personal traversal area locks | the entire perimeter tile set. Attempting entry anywhere presents the description and permits invocation. |
| **Discrete** | vehicle area locks | declared tiles only. **A dock is a discrete threshold** — boarding and landing happen nowhere else. |

Once invoked, the capability stays applied while the player holds it and remains
in the field — no per-step reconfirmation. You invoke Hazard Ward at the edge of
the fog and then you walk through fog, one tile at a time, with nothing else
advancing.

This is a generalization, not new machinery: **open water gated by Boat was
always an area lock, and docks were always its thresholds.** The pipeline has
been building area locks since v1 and calling them enclaves.

### 7.5 Area locks are absolute, never attritional

**Crossing an area lock without its capability is impossible, not expensive.**
Not survivable with potions, not crossable at 1 HP, not a dash with a healer.

The reason is the reason price is not a gate (§4) and the reason there is no
overworld combat (§5.4): an attritional barrier is grindable, a grindable
barrier is not a barrier, and a world whose tier boundaries are grindable has no
proven progression order. Every proof in §15 assumes binary passability.

| | Gates | Damages |
|---|---|---|
| **Area lock** | yes, absolutely | never — there is no overworld damage |
| **Hazard terrain** | never | no longer applicable on the overworld |

v5 makes this cleaner than v4 could. With no overworld simulation (§5.3), there
is **no mechanism by which overworld terrain could damage anyone** — no turns to
tick, no clock to advance. Attritional gating is not merely forbidden by policy;
it is unrepresentable. Damaging terrain, if wanted, lives in dungeons where time
advances and it can be modeled honestly.

In fiction, sell it accordingly: the fog is not "damaging," it is
*unbreathable*; the lava waste is not "hot," it is *molten ground*; the spike
field is not "dangerous," it is *impassable without wings*. A player who spends
twenty minutes testing the potion theory has been misled by us.

### 7.6 Lock visibility

The player should see a lock before they can pass it: a blocked mountain pass
from the valley below, a guarded city gate across the fields, a poisoned valley
from the ridge, a sealed ruin on the skyline. This converts a wall into a goal,
and it is how a 256×256 map delivers pillar 5.

Made validatable: every **boundary lock** has at least one **vantage point** — a
tile reachable at or before the tier where the lock is first met, from which the
lock site is visible by line of sight within a declared distance. Vantage points
are reserved alongside lock sites (§7.2) so terrain detail cannot grow a forest
across the sightline.

Area locks get this nearly free. Obstacle locks in deep passes are the hard case
and the reason vantage is reserved rather than hoped for.

### 7.7 Choosing a form

Area locks give regions identity; obstacle locks give legibility and focal
encounters; interaction locks give faces and cost almost nothing. A map gated
entirely by fields reads as colored fog walls, loses the memorable single
encounters, and pushes the critical path into the Inferable tier where it is
least safe. So:

- Each capability has a **native form** (§3.1) and the generator prefers it.
- **Personal traversal on region boundaries** prefers Area where native form
  allows — this is where the Pale Valley and the Rime Shallows come from, and
  they become region identities under §11.1.
- **Personal traversal inside regions**, and on the required path where
  legibility is tightest, prefers Obstacle.
- **Utility and Narrative** are Interaction, sited at chokepoints so they still
  read as gates: the guard stands *in* the gate, the injured traveler lies
  *across* the bridge.
- Per seed: **≥1 Area boundary lock per critical traversal capability whose
  native form allows it**, and **≤50%** of region boundaries gated by Area locks.

---

## 8. Vehicles and enclaves

A vehicle is a **route between docks**, not a license to cross terrain freely.
Boarding and disembarking happen only at docks. A vehicle is summonable at any
dock the player has visited, so it can never be left somewhere unreachable.

**Enclaves live on the same map**, ringed by impassable terrain, accessed by
vehicle to a dock inside. One bitmap, one coordinate space, no map-plane
switching. Any vehicle-only region contains its own dock; the first shipyard is
reachable on foot from the start.

In v4's vocabulary: **a navigable medium is an Area lock keyed to a vehicle
capability, its docks are its discrete thresholds, and enclave rings are its
perimeter.**

### 8.1 Navigable zones — and a correction

Every vehicle route is realized as a **navigable zone**: a wide band of its
medium connecting its docks. Zones of the same medium merge where they touch, so
four water routes read as one sea rather than four rails.

**Correction to v2.** v2 promised "free and direct movement" inside zones, with
sailing animations and a horizon. Under a universal grid (§5.1) that was simply
wrong, and it was my error rather than a constraint discovered late. The accurate
statement:

- Movement inside a zone is **topologically free** — any tile of the zone, in
  any order, no fixed routes. That was always the design intent and it survives
  intact.
- Movement is **still one tile per input**, like everywhere else in the game
  (§5.2). A vehicle does not cover more ground per step than a walker does.
- The ownership fantasy is delivered by **animation rate and presentation, not by
  step size**: the vehicle's walk-between-tiles animation is the fastest in the
  game and auto-repeat is quickest, so the sea crosses briskly and reads as a
  vehicle rather than a menu. Presentation supplies the wake, the horizon, and
  the speed; the grid supplies the positions, unchanged.

Three rules keep validation intact:

1. **Disembarkation points are exactly the zone's docks.** The reachable set of a
   zone equals the dock set of the abstract route edge.
2. **In-zone discoveries are optional content only.** Derelicts, reefs, sky
   beacons. Anything required in a zone must be a dock, validated as a node.
3. **Zones are reserved structure** (stage 5), not terrain detail.

---

## 9. Party, dismissal, and travel

- Party size is **4**, including an undismissable protagonist.
- **All recruited companions are present in every town** — a guild network with a
  branch in each. Dismissal happens only in towns.
- **Any visited town is a permanent fast-travel destination.** Fast travel
  bypasses traversal requirements; re-earning access to a town you already
  unlocked adds tedium and nothing else.
- Fast travel reaches **towns only** — not dungeons, not arbitrary tiles. The
  overworld still matters for reaching dungeons, landmarks, and secrets.

**Area locks make the dismissal rule a proof, not a convenience.** A player mid
hazard-field holds Hazard Ward via an active companion. If composition could
change in the field, they could be stranded inside an absolute barrier with no
way out — an instant, unrecoverable, generator-blameless softlock. Towns-only
dismissal makes that impossible, and the same argument covers vehicles: you
cannot un-build a boat while standing on the sea. This looks like a convenience
feature and is not.

### 9.1 Traversal capacity

| Route class | Max distinct personal traversal in cheapest solution | Free slots |
|---|---|---|
| Required path | **1** | 3 |
| Optional and shortcuts | 2 | 2 |
| Secrets and deep optional | 3 | 1 |

Utility capabilities do not count — they latch. Only party-bound personal
traversal consumes capacity. **Crossing an area lock counts**: a route through a
hazard field then a flooded basin needs both simultaneously, costing two slots.
Composed area locks (§7.3) are the cheapest three-capability route, which makes
them excellent for secrets and illegal on the spine.

Measured as a **minimum over solution sets**. The critical path is always
beatable with three preferred fighters plus one specialist, so combat identity
survives the whole game — and under §14 that matters far more than it did in v4,
because the party is now four actors on a tactical grid rather than an abstract
stat block.

A route whose cheapest solution exceeds its class is **rejected, not repaired**.

---

## 10. The knowledge layer

The solvability simulation already computes which places each capability newly
opens. The job is to surface that **without displaying requirements**.

### 10.1 Three levels of disclosure

**1. Locks annotate themselves in fiction.** Contact permanently creates a map
annotation recording **the lock's description**, not its requirement — a pin that
says *boulder*, not *needs Breach*. Area locks annotate their whole footprint on
first threshold contact, so "the stinging fog" appears as a named map region,
which is most of §11.1's identity work done free.

**2. The journal aggregates by description.** Every touched, unresolved lock,
grouped **by description class** and region, with **tile distance** and nearest
fast-travel town — distance is a real number now that movement is gridded, and
the journal should show it, because route planning is the overworld's actual
gameplay (§5.3). Eleven boulders appear as one group of eleven.

The journal shows **only locks personally contacted.** Never a content list.

**3. Rumors point outward, in fiction.** Town NPCs describe locks and places not
yet contacted — the rumour-bearers guaranteed in every town by §14.10, with
residences carrying local colour and important buildings carrying the
consequential ones — drawn from the spoiler log restricted to current tier or
below,
phrased loosely and locationally — "the ridge road east of Elmsford is blocked by
a fall of rock"; "nothing that walks into the Pale Valley walks out." Never
coordinates, never capability names, never above tier.

### 10.2 Learning lock classes

**The first successful resolution of a lock class teaches the mapping**
permanently: *boulders can be shattered.* Thereafter the journal annotates every
logged instance — past and future — as solvable and promotes them to **"newly
openable"** when the capability is available; if the provider is benched, it says
which companion to bring; and rumors may name the class plainly.

Deduction happens **once per class**, where it is enjoyable — not eleven times,
where it is a chore. First contact is an adventure game; the tenth is logistics,
and logistics should be fast.

Learned mappings are a third kind of append-only progression state (§12).

### 10.3 Capping the load

- Each capability newly opens **2–5** previously visited places.
- **Outstanding contacted-but-unresolved locks stay under 8** at every step.
- ≥1 newly opened place per capability sits **within one region** of its source.
- **Step distance** from acquisition to payoff stays inside the traversal budget
  (§15) — counted as actual grid steps along the legal path, not straight-line,
  and separately as travel seconds once road and vehicle animation rates are
  applied.

These are the only cost of backtracking in a game with no overworld attrition
(§5.3), which is why they are validated rather than advisory.

---

## 11. World identity

### 11.1 Regional themes

Each region draws a **theme** from an authored set (ashland, drowned lowland,
high steppe, glass desert, fungal deep, storm coast, terraced farmland, dead
capital) at stage 2, alongside tier assignment. Theme drives choices the pipeline
was going to make anyway: palette weights, eligible barrier families, eligible
landmark stamps, naming word lists, dungeon dressing, eligible lock skins, and
which area lock fields are plausible.

Area locks and themes reinforce each other: a themed region with a matching area
lock on its boundary is a *named hazard the player sees from outside and
eventually conquers* — three of this document's goals on one piece of terrain.

Constraints: no theme repeats in a seed; themes must be compatible with activated
barriers (a drowned lowland in a seed without Deep Dive or Boat is a lake nobody
enters); adjacent regions cannot both be extreme variants.

### 11.2 Signature landmarks

Each region contains exactly **one signature landmark** from an authored pool of
multi-tile set pieces matched to its theme, with **no landmark type repeating
within a seed**. It must be **visible from outside its region**, **named**
procedurally and referred to by that name in rumors and directions, sited at a
**notable position**, and carry a **small non-required reward**.

§7.6's vantage points and §11.2's visibility want the same sightlines; reserve
them together — the ridge you see the Pale Valley from is the ridge the Glass
Vault should stand on.

### 11.3 Naming

Theme-keyed word lists name regions, towns, dungeons, signature landmarks,
vehicle routes, and **area locks**. A field the player has learned to cross
deserves a name: the Pale Valley, the Rime Shallows, the Ashen Waste. Cheapest
identity multiplier in the document.

### 11.4 What is deliberately not here

Factions, historical events, procedural lore, and quest chains are **not** in
scope; §21 defines the interface they attach to.

---

## 12. Persistence and purchases

Progression state is **append-only**. Acquired capabilities, recruited
companions, visited towns, map annotations, heard rumors, **learned lock
classes**, and **resolved locks** are never removed by any mechanic. Progression
items live in a dedicated inventory: not sellable, droppable, consumable, or
equipment.

The only reversible state is **which four companions are currently active**,
re-chosen at any town — and §9 explains why "at any town" is a proof.

**Required purchases use tokens, not gold.** A token is a capability granting the
right to buy; permanent permission, never consumed. Gold buys consumables and
equipment upgrades only and never appears on the required path.

v5 gives gold a clean home. With no overworld attrition and all combat in
dungeons (§5.3, §5.4), **consumables and equipment matter exactly where
resources are consumed and nowhere else**: inside dungeons, against a turn
clock. The economy is therefore fully contained in the layer that cannot gate
progression, which is the strongest possible version of "a broken economy cannot
produce an unwinnable world."

Invoking a capability at a lock **costs nothing and consumes nothing**. Resolved
obstacle and interaction locks stay resolved as permanent world state. Area locks
are never "resolved" — they remain crossable exactly while the player holds the
capability, which is the point of their being party-bound.

---

## 13. Generation pipeline

Topology before terrain. **Noise decorates; it never decides structure.**

0. **Activate the capability subset and assign roles** (§3.1, §4.2).
1. Partition the map into regions whose adjacency is drawable on a 2D map by
   construction.
2. Assign tiers outward from the start region, assign **regional themes**
   (§11.1), label every boundary open, gated, or vehicle-only.
3. Build the progression graph: locks with **solution sets**, sources, converters,
   shortcuts, and **the per-tier repeatable dungeon** (§14.4). Required routes
   respect traversal capacity (§9.1) by construction.
4. **Choose a lock form for every lock** (§7.1, §7.7) and fix each boundary's
   intended requirement (§4.3).
5. **Reserve the structure** — corridors, barriers, docks, chokepoint geometry
   and lock sites, area lock footprints with perimeters and thresholds, navigable
   zones, signature landmark footprints, vantage points.
6. Place towns, dungeons, and landmarks against those reservations.
7. Generate terrain detail around the reservations, never across a reserved
   sightline or perimeter.
8. Lay roads, trails, and bridges — **capability-aware**, so a road never routes
   around a lock its boundary is meant to enforce — and assign their **movement
   multipliers** (§5.3).
9. **Assign lock skins** from theme-and-terrain-keyed tables, honoring legibility
   rules.
10. Name regions, settlements, dungeons, landmarks, routes, and area locks.
11. **Generate interiors** against their declared interfaces (§14).
12. Validate.
13. Repair only if validation fails, never breaching a reserved barrier, pinching
    a zone, holing a perimeter, or blocking a vantage line. Repair frequency is a
    tuning signal.

Stages 4–5 decide lock realization **before terrain exists**, so a pass is narrow
because a boulder needs to sit in it, not because noise left a gap.

---

## 14. Interiors: dungeons and towns

Dungeons and towns are **procedurally generated and self-contained**. Each
declares only an interface: entrances, capabilities required to enter, and
capability sources contained. The overworld sees nothing else.

### 14.1 Interface rules

- **No traversal gates inside interiors.** There is no town inside a dungeon, so
  arriving with the wrong party must never strand a player. Interiors use their
  own local locks.
- **All locks are permanent flags, never consumed keys.**
- **Cross-dungeon dependencies exist only at the interface level** — one dungeon
  may require a capability another grants. Never interior to interior.
- Interiors are always re-enterable. Nothing is one-way.
- Interiors read the activation manifest for dressing and local lock flavor, but
  never introduce a requirement their interface does not list.
- **Interior locks use the same three forms and the same presentation** (§6, §7).
  A jammed mechanism, a flooded stair, a collapsed gallery — described problems,
  not colored doors. Interior area locks must satisfy perimeter integrity within
  the interior, and **may never require a capability the entrance did not**;
  otherwise a player crosses a threshold and strands themselves where §9's safety
  net cannot reach.
- **Personal traversal may be useful inside dungeons but never required.** A
  climber reaching an optional ledge, a grapple crossing a tactical gap — good.
  Anything on a dungeon's required internal path must be solvable by any party
  that could legally enter, which is what guarantee 10 has always meant.

### 14.2 Tactical mode

Dungeons are **fully turn-based**, Mystery Dungeon style. **Whenever the player
acts, time advances**: move, attack, use an item, cast a spell, use a dungeon
ability. After each player action, enemies and systems take their turns.

```
Overworld                Dungeon
---------                -------
Player moves.            Player moves.
Nothing else advances.   Enemies act.
                         Time advances.
```

Controls are identical; the rules are not. One arrow key means "move one tile"
in both places, which is the whole of pillar 10.

A dungeon's purpose is combat, resource management, tactical decision-making,
risk and reward, and capability acquisition. Its central question is **"can I
survive long enough to succeed?"**

### 14.3 Persistent and regenerating dungeons

Mystery Dungeon convention regenerates floors on every entry. Applied naively
that would break determinism, invalidate §15's interior validation, and let
validated capability sources move between visits. Split by function instead:

| Kind | Layout | Contains progression sources | Purpose |
|---|---|---|---|
| **Story dungeon** | generated once with the world, persistent, validated | yes | capability acquisition; the loop's main terminal |
| **Repeatable dungeon** | regenerated per entry from a seeded descent | **never** | the per-tier repeatable power source (§14.4) |

This gets both things at once: the 4 story dungeons plus final dungeon are fixed
worlds the validator can prove and the player can learn, while repeatable
dungeons deliver the endlessly-fresh roguelike descent — and because they contain
no sources, regenerating them cannot affect any proof. A regenerating dungeon's
seed derives from the world seed plus a visit counter, so it is reproducible for
bug reports without being static.

### 14.4 Guarantee 17, realized

"Every tier offers a repeatable way to gain power" was abstract in v1–v4. With no
overworld combat (§5.4), repeatable power is necessarily **dungeon-resident**, and
that turns an abstract promise into a generation constraint:

**Every tier must contain at least one re-enterable dungeon, reachable within
that tier, whose difficulty band matches the tier.**

Placed at pipeline stage 3, alongside the progression graph, because it is
progression structure rather than decoration. It is also the mechanism that makes
§17's softlock policy honest: telling a player that a hard fight is "a signal to
grind or find power elsewhere" requires that somewhere to grind provably exist
within reach.

### 14.5 The party on a tactical grid

Party size 4 now means **four actors on a turn-based grid**, which is a real
design commitment and the largest new cost in v5. Two viable shapes:

| Option | Party is | Cost |
|---|---|---|
| **Full tactical party** (recommended) | four units on the grid, each acting on its turn | ally AI or direct control, formation handling, pathing in corridors |
| **Abstracted companions** | protagonist on the grid; companions contribute abilities and passive support | far cheaper, but party composition stops mattering tactically |

Recommended: **full tactical party.** Party composition is this design's central
player decision, and §9.1 spends real effort guaranteeing three free combat slots
on the required path. If those slots do not translate into tactical presence, the
guarantee protects nothing. Direct control of all four in corridors is the
conventional solution and the one that makes a specialist-heavy party feel like a
genuine trade rather than a UI note.

Either way this is a **Phase 4** decision (§20) and does not block Phases 0–3.
Flagged here because §9.1's whole argument depends on the answer.

### 14.6 Defeat

The precise point where roguelike convention would break the contract.

**Defeat in a dungeon never removes progression state.** It ejects the party to
the dungeon entrance or the nearest visited town, and may cost **gold and
consumables only** — never capabilities, never progression items, never learned
lock classes, never recruited companions, never visited-town or annotation state.

This follows from guarantee 1 and it is not negotiable, because a Mystery
Dungeon-style item loss applied to a progression item would manufacture exactly
the unwinnable world the entire document exists to prevent. It also makes §5.4's
claim safe: a player can walk to an over-tier dungeon and lose, and the only cost
is the trip.

### 14.7 Map scales, and the rule about buildings

Exactly **three map scales exist**: overworld, town, dungeon. **No map exists
below them.** A building is not a map; it is an enclosure on the town grid.

| Scale | Extent | Entered from |
|---|---|---|
| Overworld | 256×256, one map, enclaves included | new game |
| Town | one continuous map per town | an overworld tile |
| Dungeon | one map per dungeon (floors for regenerating ones) | an overworld tile |

A town is therefore a single continuous space: roads, walls, gates, districts,
landmarks, buildings, and NPCs all on one grid, in one coordinate system, with
**no transition of any kind once inside.** Entering the town from the overworld is
a scale change; everything after it is walking.

This is the same decision as §8's "enclaves live on the same map" and for the
same reason — one coordinate space is cheaper to generate, cheaper to validate,
and impossible to get inconsistent. A door that opened onto a separate 12×12
bedroom map would multiply the interior count by the building count for no
gameplay return.

### 14.8 Buildings as enclosures

Each building receives an **exterior footprint** during town generation, and its
interior is generated **inside that footprint**. Walls, doors, floor, furniture,
NPCs, and interactive objects are all tiles on the town grid.

```
before entry                 after entry
+--------+                   +--------+
| ?????? |                   |  Bed   |
| ?????? |                   | Table  |
| ?????? |                   |  NPC   |
+--------+                   +--------+
```

Mechanically this is **the third use of one mechanism**: a sealed perimeter with
declared thresholds. Area locks have perimeters and threshold tiles (§7.4);
navigable zones have perimeters and docks (§8); buildings have walls and doors.
All three are validated the same way, and all three benefit from the same rule —
**building walls are reserved structure at the town scale and are forced to the
Sealing corner-cut class** (§5.2, guarantee 54). Nobody slips diagonally into a
closed house through a wall corner, and the check that proves it is the flood
fill already written for area lock perimeters.

**Footprint communicates importance before entry.** A guild hall visibly occupies
more ground than a residence. This is pillar 9's principle applied at town scale —
the same reason a signature landmark sits on high ground and a lock sits in a
pass. Scale is monotonic in gameplay importance (guarantee 59), so a player
crossing a strange town can read what matters from the rooftops.

**Interior size is bounded by the footprint**, which makes the ordering in §14.10
load-bearing: important buildings are placed and sized *before* their interiors
are generated, because a building that needs four rooms needs a footprint that
holds four rooms. Doing it the other way produces guild halls that do not fit in
themselves.

### 14.9 Interior depth

Three depths, assigned by category. This is what keeps town generation from
producing thousands of tiles of uninteresting bedroom.

| Depth | Interior | Categories |
|---|---|---|
| **None** | solid building; interacting at the door opens a service UI directly | most shops, banks, storage, crafting services, stables |
| **Minimal** | one room, a few furnishings, one or two NPCs | ordinary residences, small inns, workshops |
| **Full** | multiple rooms, unique NPCs, progression content | guild hall, temple, library, mayor's hall, converter site, shipyard, academy |

- **Ordinary residences** are Minimal and provide world flavor, local dialogue,
  and minor rumors. Generation stays compact deliberately.
- **Service buildings** are usually None. A shop is a menu with a door, and
  pretending otherwise costs generation time and player patience in equal
  measure. A service building gets an interior only when it also hosts something
  else — an inn with a rumor-heavy common room, say.
- **Important buildings** are Full and are where the town's declared interface
  (§14.1) actually lives: capability sources, companion management, converters,
  unique NPCs, important rumors, story events.

**Contents are concealed until entry** (§5.7): exterior walls occlude vision, so
the player sees the building, its size, and its entrances, and nothing within.
This is what preserves the feeling of entering and discovering a building on a
map that never transitions.

### 14.10 The town's interface, realized

§14.1 says every interior declares an interface — entrances, capabilities
required, sources contained. For a town, **that interface is realized as specific
buildings at specific coordinates**, exactly as §7.2 realizes graph locks as lock
sites. The guild interface is a guild hall you can stand in front of.

Two constraints on that realization, and they are safety-net rules rather than
flavor:

- **Every town has a designated arrival tile** — its primary gate — which is
  where fast travel (§9) lands and where the overworld entrance leads.
- **Every interface feature is reachable from the arrival tile with no
  capabilities and no traversal locks**: the roster building, shops, converters,
  and rumor NPCs. Guarantee 10 already forbids traversal gates inside interiors;
  guarantee 57 says the *approach* to a town's functions is additionally
  unconditional.

The distinction that makes this workable: **an Interaction lock may gate a
building's function, never its approach.** A shipyard whose construction requires
Engineering is correct and expected — you walk in freely and the lock is on the
work. A shipyard behind a gate requiring Engineering is a bug, because fast travel
plus dismissal is the mechanism that makes party-bound traversal survivable (§9),
and it fails the moment a town's roster building can be unreachable.

**Every town contains a roster building** — the guild or tavern branch — which is
what realizes guarantee 6's promise that the full roster is present in every town
and dismissal happens there.

### 14.11 Readability, centerpiece, and role

**Layout communicates function through physical structure**, so a player
understands a town without opening a menu:

- market buildings cluster on major roads;
- workshops occupy an industrial district;
- temples sit on plazas or beside the centerpiece;
- guild halls take prominent positions on the main approach;
- docks connect directly to a waterfront district.

**Every town has exactly one centerpiece** — ancient tree, fountain, windmill,
statue, crystal formation, watchtower, shipyard — with buildings and roads
arranged around it. It is a navigation aid and the thing that distinguishes one
town from another. No centerpiece type repeats within a seed (guarantee 62).

Note the deliberate distinction from §11.2: a **region signature landmark** is an
overworld-scale set piece, one per region; a **town centerpiece** is town-scale,
one per town. A town may host its region's signature landmark — a shipyard or
watchtower plausibly serves both — but it is never required to.

**Town role** — port, market, temple town, industrial, frontier, capital — drives
district mix, building set, eligible centerpieces, and naming, and combines with
the region theme (§11.1) so that a port in an ashland reads differently from a
port on a storm coast.

**Role is assigned by the overworld pipeline, not the town generator.** This is a
correction worth stating plainly: a port town must be sited on a coast with a
dock, and §13's stage 6 already places dock towns on coasts *by construction
rather than by luck*. A town generator that picked its own role would learn it
after the siting decision was made, and would eventually declare itself a port
inland. Role is decided at stage 6 and handed down; the town generator consumes
it (guarantee 63).

### 14.12 Town generation pipeline

Runs at stage 11 of §13, once per town, from its own RNG stream:

```
Receive role, theme, and siting   ← from the overworld pipeline (§13 stage 6)
        ↓
Place centerpiece
        ↓
Lay out districts
        ↓
Generate road network            ← arrival tile fixed here
        ↓
Place major buildings            ← against districts and roads
        ↓
Assign building footprints       ← scale from importance (G59)
        ↓
Generate Full interiors          ← inside their footprints
        ↓
Generate Minimal interiors
        ↓
Populate NPCs                    ← including ≥3 rumour-bearers (G64)
        ↓
Decorate, then validate          ← interface, reachability, perimeters
```

Order matters in two places. Major buildings precede footprints, and footprints
precede interiors, so that importance flows outward into physical scale and then
inward into content. And the road network precedes building placement, so the
arrival tile exists before anything needs to be reachable from it.

---

## 15. Validation

v1 had four checks, v2 eight, v3 ten, v4 twelve, v5 thirteen.

**Solvability.** Simulate an exhaustive player: visit what's reachable, acquire
everything, repeat. A lock passes if **any** solution set is satisfied. The final
dungeon must be reachable. Produces the **spoiler log** — minimum path plus
per-lock alternates — serving debugging, the hint layer, pacing, and rumors.

**No-softlock.** Repeat using only *guaranteed* sources. Every required lock must
have a solution set satisfied **entirely** by guaranteed, non-consumable sources.
Solvability proves a perfect player can win; this proves anyone can.

**Route width.** No required destination's **cheapest solution set** demands more
simultaneous party-bound traversal capabilities than its class allows. Area lock
crossings count.

**Map reachability, capability-parameterized.** Walk the finished tilemap
directly — **once per progression state along the spoiler log**, using 8-way
movement with the per-terrain corner-cut classes and per-capability passability
masks (§5.2). The walk must use the same corner-cut resolution the movement code
does, from the same table. At each state, tiles reachable on the map must match exactly the nodes the
structural simulation says are reachable. Extra map reachability is a bypass;
missing map reachability is a lock sealing something it shouldn't.

Catches: a town in a lake, a road crossing a chasm, a region sealed by terrain
detail, an enclave whose ring isn't closed, a hazard field with a gap along the
cliff, a ford terrain detail made walkable, a diagonal pinch that shouldn't be
passable.

**Lock realization.** Every graph lock has a reserved physical realization at
specific coordinates, and still hosts it after terrain detail and repair. No lock
exists only in the graph; no barrier exists on the map without a graph entry
explaining it.

**Boundary equivalence and perimeter integrity.** For every boundary, the
disjunction of requirements over all physical crossings equals the intended
requirement (§4.3). For every area lock, the perimeter is sealed and no other
tile permits entry (§7.3); boundary-spanning area locks span completely. Run this
first when a seed behaves strangely.

**Exploratory independence.** Re-run solvability with **every exploratory
capability withheld**. Every required lock must retain a fully-critical solution
set.

**Solution-set hygiene.** Per lock: 1–3 solution sets, each 1–2 capabilities, all
from the manifest, no duplicates, no superset of another. Required locks have ≥2
where the manifest allows. Items only alongside capabilities, and only with
repeatable sources on the required path. No gating terrain is attritional (§7.5).

**Legibility and visibility.** No Obscure skin on the required path; no Inferable
skin there before its class is learned in the spoiler walk. Every (lock class ×
active capability) pair has an authored failure response with a defined fallback.
Every boundary lock has a reserved, reachable vantage point with an unobstructed
sightline.

**Progression pacing.** At every step of the spoiler log: 1–3 locks newly
solvable — never zero, never a flood; outstanding unresolved locks under 8; each
capability newly opens 2–5 previously visited places, ≥1 within a region of its
source; the gap between consecutive acquisitions inside the pacing budget.

**Traversal budget.** New in v5, and only possible now that movement is gridded.
Measure the spoiler log path **twice**:

- **Steps** — actual grid steps along the legal shortest path, using fast travel
  from visited towns where available. This is the player's input and attention
  cost, and speed cannot reduce it (§5.3).
- **Seconds** — the same path weighted by per-terrain animation rates, so roads
  and vehicles count for what they are worth.

Assert both stay inside budget for the walk between consecutive progression
events and for the round trip from each capability source to its nearest payoff.

Two numbers rather than one because they fail independently, and because speed is
animation-only they cannot be collapsed. A path that is brisk in seconds and
enormous in steps is a road across the map — tolerable, but it is still four
hundred key presses. A path short in steps but slow in seconds is an
off-road slog, and the fix is a road rather than a re-layout. Pacing counts
events; this counts the walking.

It is also the check that tells us whether the animation rates and fast-travel
rules are tuned, the only structural argument available for or against shrinking
the 256×256 map, and — run both ways — the way to settle whether fast travel
should be available from the start (§21).

**Power band monotonicity.** Encounter bands along every required route are
non-decreasing in tier; each tier's required encounters fall inside its band; and
**every tier contains a re-enterable dungeon reachable within it** (§14.4). Uses
the abstract band index; tightens into a real expected-power check when combat
lands. Since all combat is dungeon-resident (§5.4), this check now has a precise
domain rather than an implied one.

**Identity.** Every region has exactly one signature landmark. No landmark type,
theme, name, or lock skin repeats within a seed beyond declared multiplicity.
Every active capability solves ≥2 locks and is the sole active solution to ≥1.
Form mix holds (§7.7).

Interiors are validated independently against their declared interfaces.

**Dungeons** — persistent ones fully; regenerating ones by asserting they contain
no progression sources and that every generated floor has a reachable exit.

**Towns** — a distinct check list, because a town's failures are safety-net
failures rather than progression failures:

- every declared interface feature exists as a building at coordinates, and a
  roster building is among them (G57, G58);
- every one of them is reachable from the arrival tile with an empty capability
  set and no traversal lock on the approach — walked on the grid, not asserted
  from the graph;
- every building interior is reachable through a door, and every building
  perimeter is closed and carries the Sealing class (G60), by the same flood fill
  used on area lock perimeters;
- every interior fits inside its footprint, and footprint scale is monotonic in
  importance (G59);
- exactly one centerpiece, no repeated centerpiece type in the seed (G62);
- the town's siting satisfies its assigned role — a port is coastal and has a
  dock (G63);
- at least three rumour-bearing NPCs, so §10.1's disclosure layer works in every
  town rather than only the ones that happened to get talkers (G64).

---

## 16. The contract

1–45 carry forward from v4 (9, 22, 23 amended in v3); 46–53 are new in v5.

| # | Guarantee |
|---|---|
| 1 | Progression state is append-only, including learned lock classes. |
| 2 | Capability-granting items are never sellable, droppable, consumable, or equippable. |
| 3 | Every capability on the required path has at least one guaranteed source. |
| 4 | Capability sources respect tier ordering; shop availability is gated by tier, not price. |
| 5 | A companion's recruitment site is reachable without that companion's own capability. |
| 6 | Dismissal only in towns; the full roster is present in every town. |
| 7 | The start region contains a source of the first capability. |
| 8 | The world is winnable using guaranteed sources alone. |
| 9 | No required route's cheapest solution set exceeds the party's traversal capacity. |
| 10 | No traversal gates inside interiors; all locks permanent. |
| 11 | Nothing is one-way; every interior has a reachable exit. |
| 12 | Reserved structure survives terrain generation and repair. |
| 13 | Every vehicle-only region contains a valid dock inside it. |
| 14 | The first shipyard is reachable on foot from the start. |
| 15 | Secrets are never required, and always eventually reachable. |
| 16 | Region adjacency is drawable on a 2D map. |
| 17 | Every tier offers a repeatable way to gain power. |
| 18 | Enclaves are genuinely sealed to foot travel. |
| 19 | The seed activates a valid subset: 3–4 personal traversal (≥1 native Area), 2–3 vehicle (≥1 water), 4–5 utility (Engineering plus ≥1 narrative-category). Nothing outside the manifest appears anywhere. |
| 20 | Exploratory capabilities are never necessary — the world is winnable with all withheld. |
| 21 | Every active capability solves ≥2 locks, is obtainable, and is the sole active solution to ≥1. |
| 22 | No required lock's cheapest solution set requires more than one distinct personal traversal capability. Optional allows two, secrets three. |
| 23 | Every lock presents a fiction-first description on contact and creates a permanent annotation of that description. Requirements are never displayed before the class is learned. |
| 24 | Each capability newly opens 2–5 previously visited places, ≥1 within a region of its source. |
| 25 | Outstanding contacted-but-unresolved locks never exceed 8 at any point on the spoiler log. |
| 26 | Every region has exactly one signature landmark; no landmark type, theme, or name repeats within a seed. |
| 27 | Every vehicle route has a navigable zone connecting all its docks, with landing at its docks and nowhere else. |
| 28 | Pacing holds: 1–3 locks newly solvable at every step, no acquisition gap beyond budget, encounter bands non-decreasing along required routes. |
| 29 | Every lock has 1–3 solution sets of 1–2 capabilities, all from the manifest, none a superset of another. Required locks have ≥2 where the manifest allows. |
| 30 | Every required lock has ≥1 solution set satisfied entirely by guaranteed, non-consumable, critical-role sources. |
| 31 | Items appear in solution sets only alongside a capability; on the required path only with a repeatable, reachable, tier-appropriate source. |
| 32 | No Obscure skin on the required path; no Inferable skin there before its class is learned. |
| 33 | Lock skins are selected at generation time and stored; presentation never selects a skin. |
| 34 | Every (lock class × active capability) pair has an authored failure response, with a defined fallback. |
| 35 | Invoking a capability never consumes, costs, or removes anything; resolved locks stay resolved. |
| 36 | No lock skin repeats within a seed beyond its declared multiplicity. |
| 37 | Every graph lock has a reserved physical realization at specific coordinates and still hosts it after terrain generation and repair. No barrier exists without a graph entry. |
| 38 | **Boundary equivalence:** for every boundary, the disjunction over all physical crossings equals the intended requirement. No ungated bypass. |
| 39 | Every area lock's perimeter is sealed by impassable terrain, map edge, another lock, or a declared threshold — and nothing else. Boundary-spanning area locks span completely. |
| 40 | Area locks are absolute. No gating terrain is attritional, survivable, or grindable. |
| 41 | Every area lock has declared thresholds — continuous perimeter for personal traversal, discrete docks for vehicles. Entry is possible nowhere else. |
| 42 | Crossing an area lock counts toward route width. |
| 43 | Every boundary lock has a reserved vantage point, reachable at or before its tier, with an unobstructed sightline. |
| 44 | Lock skins are compatible with local terrain and region theme at their site. |
| 45 | Form mix holds: ≥1 Area boundary lock per critical traversal capability whose native form allows it; ≤50% of region boundaries Area-gated. |
| 46 | Movement is tile-based everywhere and **one input moves exactly one tile**, with no exception for roads, vehicles, or capabilities. Speed is animation rate only and never alters position, distance, or legality. |
| 47 | The overworld advances no simulation: no enemy turns, time, decay, hunger, or wandering movement. Overworld travel costs nothing but player input. |
| 48 | All combat is dungeon-resident. No overworld encounter gates progression; capability locks are the overworld's only gating mechanism. |
| 49 | Every tier contains at least one re-enterable dungeon, reachable within that tier, whose band matches the tier. |
| 50 | Defeat never removes progression state. It ejects the party and may cost gold and consumables only. |
| 51 | Story dungeon layouts are generated once with the world, persistent, and validated. Regenerating dungeons contain no progression sources and every floor has a reachable exit. |
| 52 | Personal traversal capabilities may be useful inside dungeons but never required on an interior's internal path. |
| 53 | Every lock's interaction tile set is explicit: Obstacle and Interaction locks resolve from adjacent tiles, Area locks on threshold entry. |
| 54 | Every terrain class declares a corner-cut class (Sealing, Permissive, Open) with a per-layer default. **Reserved structure — area lock perimeters, enclave rings, reserved barriers, chokepoint walls, lock sites — is forced to Sealing and cannot be overridden.** Perimeter closure is asserted against the declared classes, not assumed. |
| 55 | Validation and movement resolve diagonals from the same corner-cut table. A divergence between them is a build failure, not a tuning difference. |
| 56 | Exactly three map scales exist — overworld, town, dungeon — and no map exists below them. A building is an enclosure on the town grid, never a separate map. A town has no internal transitions. |
| 57 | Every town has a designated arrival tile, and every declared interface feature is reachable from it with an empty capability set and no traversal lock on the approach. An Interaction lock may gate a building's function, never its approach. |
| 58 | Every town contains a roster building where the full companion roster is present and dismissal occurs. |
| 59 | Every interior fits within its building's exterior footprint, and footprint scale is monotonic in gameplay importance. |
| 60 | Building walls are reserved structure at town scale: closed, forced to Sealing, with doors as their only thresholds. Every interior is reachable through a door. |
| 61 | Interior contents are concealed until entry; walls occlude vision. Concealment is never knowledge erasure — anything once revealed stays revealed. |
| 62 | Every town has exactly one centerpiece landmark; no centerpiece type repeats within a seed. |
| 63 | Town role is assigned by the overworld pipeline before placement, and the town's siting satisfies its role. |
| 64 | Every town contains at least three rumour-bearing NPCs. |

---

## 17. Softlock policy

**Player-caused softlocks are accepted.** If a player sequence-breaks past
intended progression, that is their responsibility and the world does not rescue
them. Enemies too hard are a signal to grind or find power elsewhere, not a
balance failure — and guarantee 49 now makes that advice honest, because
somewhere to grind provably exists within every tier.

Generator-caused locks remain bugs. Guarantees 6, 10, 11, 13, 20, 22, 27, 30,
32, 39, 40, 41, **50, and 52** are not waived. The two new entries:

- **50** — a defeat that removes a progression item manufactures precisely the
  unwinnable world this document exists to prevent, and it does so from a
  convention nobody would think to question.
- **52** — a traversal requirement on a dungeon's internal path strands a party
  that legally entered, in the one place §9's town-based safety net cannot reach.

v5 also *removes* a whole category of ambiguity. With no overworld combat
(§5.4), there is no such thing as a region that is reachable but lethal, so
"sequence-breaking" now has a sharp meaning: holding a capability earlier than
its tier. Everything else the player does on the overworld is, by construction,
something the locks permitted — therefore intended, therefore ours to have
validated.

The practical response remains diagnostic: the generator never *offers* a
capability ahead of tier, the spoiler log always exists, and a debug view reports
what the player holds, what they have learned, and what is still reachable.
Multiple save slots and an autosave before any point of no return remain strongly
recommended.

---

## 18. Success metrics

- **Clear next objective** — one to three locks newly solvable at each point on
  the critical path. Never zero, never more than three.
- **Abilities reopen old areas** — each capability newly opens 2–5 previously
  visited places.
- **Backtracking stays light** — under eight outstanding unresolved locks; every
  capability pays off within a region of its source; **round-trip tile counts
  inside budget**.
- **Travel feels brisk** — median steps *and* median seconds between progression
  events, plus the share of travel on roads versus open terrain. Both numbers
  matter: steps measure the player's patience, seconds measure the clock, and
  animation speed only buys the second one. Together they tell us whether
  256×256 is right.
- **The world feels larger** — reachable map share grows gradually across tiers;
  every player can point at somewhere they have *seen but not reached*, which
  §7.6 makes structural.
- **Seeds differ structurally** — activated capabilities, role assignment,
  themes, lock forms, landmarks, and skins, not merely layout and ordering.
- **Optional content exists and is worth it** — a meaningful number of locations
  off the required path; every exploratory capability provides ≥1 shortcut or
  alternate solution on the spine.
- **The party stays a party** — three combat-free slots on the required path, and
  those slots demonstrably matter in dungeon tactics (§14.5).
- **Locks read** — a playtester solves each first-encounter required lock within
  two invocations; brute-forcing is rare. Tracked separately for Area locks, the
  form most at risk of reading as an unexplained wall.
- **Nobody tries to tank a gate** — no playtester attempts to cross a gating
  field by surviving it. If they do, the fiction failed §7.5 even though the
  rules made it impossible.
- **The two modes feel different** — playtesters describe the overworld and
  dungeons in different vocabulary. If dungeon language shows up for the
  overworld, §5.3's no-simulation rule has leaked.
- **Solutions are plural in practice** — across playthroughs of one seed, players
  resolve required locks by measurably different solution sets.
- **Seeds are memorable** — a playtester names three places from a seed a day
  later. Named area locks are the cheapest way to win this.
- **Generation is healthy** — repair rarely fires; generation stays under a couple
  of seconds with all thirteen checks running. The capability-parameterized map
  walk is the dominant cost and scales with both map area and progression depth.

---

## 19. Authored content

Six grains, none requiring hand-built maps:

1. **Tiles.** Terrain art with transition and blending sets, one barrier family
   per activated personal traversal capability, one navigable medium per vehicle.
   Mandatory — without it, semantic terrain reads as noise. Families share
   transition sets to contain cost.
2. **Area lock fields.** Per area-lock capability: a field treatment (tint,
   particle, overlay), a **perimeter edging** set, and a threshold presentation.
   The perimeter is the expensive and important part — it tells the player without
   words that the edge of the fog is a boundary, not a gradient.
3. **Multi-tile stamps.** Ruined tower, stone circle, bridge assembly, shrine
   approach — authored internally, procedurally placed. Includes **chokepoint
   furniture**: pass walls, gate arches, causeway ends that make a reserved
   chokepoint read as a place worth blocking.
4. **Signature set pieces.** Theme-keyed stamps used only as region signature
   landmarks. Fewer than a dozen buys the thing players remember a seed by.
5. **Room, district, and building templates.** Authored rooms for dungeons,
   district blocks and building shells for towns, procedurally selected and
   stitched. Carrying more weight than in v4 for two reasons. Dungeon rooms are
   **tactical spaces** under §14.2, so they need sightlines, cover, chokepoints,
   and retreat options, not just shape — a template set authored for exploration
   will produce dungeons that look fine and fight badly. And town buildings need
   **exterior silhouettes that read their importance** (§14.8) plus interior
   furnishing sets per depth tier; the cheap win here is that **None-depth service
   buildings need no interior authoring at all**, which is roughly 40% of the
   buildings in a town.
6. **The lock table.** Per capability: skins with description text, legibility
   flag, lock form, eligible themes and terrains, multiplicity, visual treatment,
   and **failure responses** against every other active capability.

The lock table is mostly text and stationary actors — the cheapest gating variety
available — but the failure-response matrix grows as *capabilities × skins*.
Mitigations: responses default by presentation category; only the Relevant and
Near-miss tiers need bespoke writing; and the matrix need only cover *activated*
capabilities.

Plus **word lists** for names and rumor phrasing.

Grain 1 buys cohesion. Grains 3–5 deliver the handcrafted-world pillar. Grains 2
and 6 deliver pillars 8 and 9, and neither has a partial version: a lock without
failure responses is a wall that says nothing, and an area lock without perimeter
art is a wall nobody can see the edge of.

Grains 5 and 6 are substantially cheaper to design for now than to retrofit.

---

## 20. Phasing

- **Phase 0 — Progression core.** Capability model with activation, roles, and
  solution sets; world structure; progression graph including per-tier repeatable
  dungeons; lock form assignment; and every check that does not need a tilemap.
  No engine. Proven in bulk against §16.
- **Phase 1 — Geography and lock realization.** Terrain pipeline, chokepoint
  generation, lock sites, area lock footprints and perimeters, thresholds,
  vantage points, docks, navigable zones, roads and their multipliers, themes,
  signature landmarks, skin assignment, naming — plus map reachability, lock
  realization, boundary equivalence, perimeter integrity, and traversal budget.
  The largest early phase; build boundary equivalence early in it, not at the
  end, because it is the check most likely to send the terrain pipeline back.
- **Phase 2 — Walkable overworld and the interaction model.** Grid movement with
  corner-cut rules, roads, locks in all three forms, thresholds, Field Inventory,
  invocation, failure feedback, the knowledge layer, vehicles, fast travel, and
  debug tooling to grant or revoke capabilities and learned classes. No combat, no
  interiors. **This is where the design is first judged as fun** — and v5 raises
  the stakes on it, because with no overworld simulation the overworld must be
  interesting on exploration and deduction alone. If tile-by-tile travel across
  256×256 is tedious here, the fix is cheap now (map size, road multipliers,
  region count) and expensive later.
- **Phase 3 — Interiors, structural.** Dungeons and towns against their interface
  contract, with stub contents and no turn system. Proves the interface end to
  end. Towns are the better half to build first: they exercise the whole interface
  contract — arrival tile, roster building, converters, reachability from the gate
  — and unlike dungeons they are playable without a turn engine, so a town is a
  finished, judgeable thing at the end of this phase while a dungeon is a room
  layout waiting for Phase 4. Town visibility (§5.7) also lands here, since
  building occlusion is the first place the vision model does real work.
- **Phase 4 — Dungeon tactical mode and combat.** The turn engine, the §14.5
  party decision, enemies, resources, defeat handling, and repeatable dungeon
  regeneration. Power band monotonicity tightens into a real expected-power check.
  Deliberately last of the mechanical phases: it is the largest single build in
  the project and the only one the other phases do not depend on.
- **Phase 5 — Narrative layer.** Factions, chronicle, quests (§21).

The ordering point: §11's identity work is Phase 1, §7's realization work is
Phase 1, §6's interaction model is Phase 2, §14's tactical mode is Phase 4,
§21's narrative work is Phase 5. They all sound like "making the game good" and
they are five very different sizes.

---

## 21. Deferred, with interfaces

- **Combat system, experience and gold curves.** The only constraint generation
  needs is guarantees 17 and 49. Guarantee 28's band check is the seam.
- **Party control model in tactical mode** (§14.5) — recommended answer given,
  decision deferred to Phase 4. §9.1's argument depends on it.
- **Narrative generator.** Following pillar 7 — *narrative realizes progression,
  it never discovers it*:
  - **Factions** attach to regions and tiers. A faction's holdings are a tier's
    gated nodes; its rival holds an adjacent theme-incompatible region.
    Interaction locks are the natural attachment point — a guard barring a gate is
    already a faction statement.
  - **The chronicle** is read off the progression graph. Every converter, barrier,
    enclave, and area lock already has a structural reason to exist; the chronicle
    gives each an in-world cause, ordered by tier, because tier order *is*
    chronological depth. Area locks are the best hooks — a named poisoned valley
    with a sealed perimeter is a historical event waiting for an explanation.
  - **Quest chains** wrap existing converter chains, so quest generation cannot
    invent an unreachable objective.
  - **Lock skins are the seam**: upgrade skin selection from theme-keyed to
    chronicle-keyed and the boulder becomes the rockfall that closed the old road
    in the war, without touching progression.
- **Day/night and NPC schedules.** Structurally unavailable: §5.3 advances no
  overworld time. A cosmetic cycle is possible; anything with behavior is not,
  and adding it later means adding an overworld clock, which reopens the
  attrition question guarantee 40 closes.
- **Dynamic world state.** Append-only progression is load-bearing. Any future
  dynamic state must be non-progression state.
- **Whether area locks can shrink.** A drained basin, a cleared fog, a thawing
  strait would be strong late-game moments and are exactly the mutation the
  append-only contract forbids.
- **Whether fast travel is available from the start or unlocked early.** Now
  measurable rather than a matter of taste: the traversal budget check (§15) can
  be run both ways and the answer read off the tile counts.
- **Whether the capability pool widens past 21.** The activation mechanism is
  pool-size-agnostic; the lock table and barrier families are not.

---

## 22. Parameters

Starting values, freely tunable; none affect structure.

| | |
|---|---|
| Overworld | 256×256 tiles, single map, enclaves included |
| Movement | tile-based everywhere; 8-way; one input = one tile |
| Corner-cut default | Sealing on the overworld, Permissive in dungeons; reserved structure always Sealing |
| Base walk animation | 0.20 s per tile (open terrain) |
| Road animation rate | 0.10 s per tile (×2 faster, same step count) |
| Vehicle animation rate | 0.07 s per tile inside a navigable zone (same step count) |
| Held input | auto-repeats at the current animation rate |
| Regions | 6–8 |
| Tiers | 5 |
| Capability pool | 21 (8 personal traversal, 4 vehicle, 9 utility) |
| Activated per seed | 3–4 personal traversal (≥1 native Area), 2–3 vehicle, 4–5 utility |
| Critical / exploratory split | 5–7 critical, 4–6 exploratory |
| Providers per capability | 2 |
| Solution sets per lock | 1–3, of 1–2 capabilities each; ≥2 on required locks |
| Lock forms | Obstacle, Area, Interaction |
| Area-gated region boundaries | ≤50% |
| Area lock footprint | 12–40% of a region's area, or a boundary-spanning belt |
| Chokepoint width | 1–3 tiles at the lock site |
| Vantage distance | within 40 tiles, unobstructed line of sight |
| Max personal traversal per route | 1 required / 2 optional / 3 secret (cheapest solution) |
| Lock skins per capability | 3–5, theme- and terrain-keyed |
| Legibility on required path | Plain, or Inferable once learned |
| Invocation cost | none |
| Places reopened per capability | 2–5, ≥1 within one region of the source |
| Max outstanding unresolved locks | 8 |
| Traversal budget | ≤150 steps **and** ≤25 s of travel between consecutive progression events |
| Recruitable companions | 8 (party of 4) |
| Towns | 1 start + 5 |
| Town map | 64×64 tiles; 96×96 for the start town and any capital |
| Town roles | port, market, temple town, industrial, frontier, capital |
| Town centerpiece | exactly 1, no type repeated in a seed |
| Buildings per town | 12–30, by role |
| Interior depth mix | ~15% Full, ~45% Minimal, ~40% None |
| Residence footprint | 3×3 to 5×4 |
| Important building footprint | 7×6 to 12×10 |
| Rumour-bearing NPCs per town | ≥3 |
| Overworld sight range | 48 tiles, terrain-occluded (must exceed vantage distance) |
| Dungeon visibility | room-based; corridors reveal adjacent tiles only |
| Story dungeons | 4 + 1 final, persistent layouts |
| Repeatable dungeons | ≥1 per tier, regenerating, no progression sources |
| Capability sources from dungeons / converters / other | ~60 / 25 / 15 % |
| Optional locations | 5–10 |
| Shortcuts and alternate solutions | ≥1 per exploratory capability |
| Signature landmarks | 1 per region |
| Region themes | 8 authored, no repeats per seed |
| Encounter difficulty bands | 5, one per tier, dungeon-resident only |
| Defeat penalty | ejection; gold and consumables only |
| Level cap | 30 for the prototype |
| Generation time | under a couple of seconds, all thirteen checks included |

---

## Appendix — implementation cautions

The last four are new to v5.

- **Engine independence has to be enforced, not intended.** A separate library
  with a build-time check that it cannot reference the engine. Left to
  discipline, this boundary erodes within months.
- **Determinism is fragile.** A dedicated seeded generator with a separate stream
  per pipeline stage, so adding a stage later doesn't reshuffle existing seeds.
  Never depend on the iteration order of an unordered collection.
- **Saves must be version-stamped.** If a save stores a seed rather than the
  world, any generation change invalidates it. Refuse to load on mismatch.
- **Repair must treat reserved barriers as truly impassable**, not merely
  expensive. Re-validate after every repair.
- **Pathfinding cost functions must be capability-aware.** A naive shortest path
  will carve a walking route across an intended vehicle gate and quietly delete a
  tier. Doubly important now that stage 8 lays roads *after* lock sites are
  reserved: a road-laying pass that routes around a boulder rather than through
  the pass it blocks produces a world that passes every structural check and
  fails boundary equivalence.
- **Activation must get its own RNG stream, and it must be stream zero.**
- **Nothing may read the capability list except through the manifest.** One
  hardcoded reference turns a seed without that capability into a crash or a soft
  unwinnable. Cheapest guard: a test generating with the minimum viable manifest
  and walking the whole game.
- **Navigable zones are structure, not water.** A zone is an area lock, and an
  area lock without declared thresholds violates guarantee 41.
- **Role assignment must be validated, not trusted.**
- **Skin selection must live in the core, not the presentation layer.** It
  determines what the spoiler log says and whether two players on a seed see the
  same world.
- **The failure-response matrix needs a fallback path from day one.** An
  unauthored pair must produce a sane category-level default, never an empty
  string — the player cannot tell "unwritten" from "this lock doesn't work that
  way," and the second reading teaches them something false.
- **Solution sets must be stored normalized and compared as sets.**
- **Learned lock classes are save state, and they are progression.**
- **Area lock perimeters must be computed and stored, never inferred at
  runtime.** Store the perimeter as an explicit closed loop and assert closure at
  generation time. Orthogonal closure suffices **only because reserved structure
  is forced to the Sealing corner-cut class** (§5.2), so the perimeter test must
  assert both halves: the loop is closed, and every tile on it carries Sealing. A
  Permissive tile anywhere on a ring is a diagonal gap, and it is far more likely
  to arrive by terrain generation growing a thicket into the ring than by anyone
  editing the movement rules.
- **Movement and validation must share one corner-cut table, by construction.**
  Not two implementations of the same intention — one table, read by both. The
  moment they are separate constants, someone tunes dungeon feel and silently
  changes what the validator believed about sealing. Guarantee 55 should be a
  test that diffs the resolver against the walker on a generated fixture, not a
  note in a document.
- **"Just move two tiles" will be proposed for feel.** It is the obvious fix the
  first time someone crosses the map on foot, and it breaks guarantee 46 and with
  it every distance the design depends on — chokepoint width, adjacency, vantage
  range, perimeter thickness, the traversal budget. The correct lever is the
  animation rate, and it is available: halve the per-tile time and the crossing
  is twice as fast with the geometry untouched. If travel still feels long after
  the rates are tuned, the honest fixes are a smaller map, more roads, or earlier
  fast travel — all of which §15's traversal budget can evaluate, and none of
  which fork the grid.
- **Auto-repeat must not skip the per-tile step.** Holding a direction has to
  emit discrete one-tile moves at the animation rate, each one evaluated against
  passability and the corner rule. An implementation that interpolates the
  character along a multi-tile path while input is held is a multi-tile step
  wearing a costume, and it will eventually slide someone diagonally through a
  sealed corner between animation frames.
- **The capability-parameterized map walk is the most expensive check.** O(map
  area × progression states), and it will dominate generation time. Walk only the
  states where reachability changes; reuse the previous state's flood fill and
  expand from newly opened thresholds; keep it in the headless harness for bulk
  runs even if a shipping build samples it. Do not respond to the cost by walking
  once and assuming.
- **Attritional gating will be proposed again.** Guarantee 40 exists because the
  change silently invalidates every proof in §15 and the invalidation is
  undetectable by any test — the world still generates and validates, and is
  simply no longer the world the validator described.
- **Lock sites and vantage sightlines are reservations**, and terrain detail must
  respect both. A forest across a sightline is a guarantee 43 failure no
  structural check notices.
- **The overworld's no-simulation rule will erode by accretion.** Each individual
  addition is defensible — a day/night tint, a weather effect, a wandering
  merchant, a timed festival, a hunger meter "for realism." Each one also adds a
  clock, and a clock adds attrition, and attrition reopens grindable gating
  (guarantee 40) and makes free deliberation at a lock (§6.2) cost something.
  Guarantee 47 should be a test, not a memo: assert that no overworld system
  registers a tick handler.
- **Defeat handling must be written against a whitelist, not a blacklist.**
  "Lose gold and consumables" implemented as "drop a random subset of inventory"
  will eventually drop a progression item, because someone will add a new item
  category and not know about guarantee 50. Enumerate what *may* be lost; treat
  everything else as untouchable by default.
- **Regenerating dungeons must be provably source-free.** The temptation is to
  reuse the story dungeon generator with a different seed, which will happily
  place a capability source in a dungeon that regenerates it away. Interiors
  should declare their kind and the source-placement pass should refuse to run on
  a regenerating one — enforced in code, not by convention.
- **Do not let tactical-mode requirements leak into the overworld grid.** Combat
  wants facing, initiative, attacks of opportunity, and zones of control; every
  one of them is a system that advances on movement, and the overworld advances
  nothing. Keep two movement controllers over one grid, sharing only the tile
  geometry and the passability masks.
- **Every town needs its own RNG stream**, derived from the world seed plus a
  stable town identifier — not a counter, and not the order towns were placed in.
  Add a seventh town later, or change placement order, and counter-derived streams
  reshuffle every existing town in every existing seed.
- **A building is a perimeter, so reuse the perimeter code.** The temptation is to
  treat town walls as ordinary impassable tiles and interiors as a separate
  concern. They are the same structure as an area lock perimeter with discrete
  thresholds, and if the same closure test does not run over them, the first
  reported bug will be a house with a diagonal gap at a corner or an interior with
  no reachable door.
- **Footprints must be reserved before interiors are generated, and the reservation
  must be authoritative.** An interior generator that overflows its footprint by a
  tile will silently eat a road or the neighbouring house, and the failure is
  visible only as a town that looks slightly wrong. Assert containment rather than
  trusting the generator to respect a bound it was merely told about.
- **"Just make the shop a real room" is a 40% cost increase for nothing.**
  None-depth service buildings (§14.9) are the single largest saving in town
  generation, and every one converted to a real interior needs a layout, a
  furnishing pass, NPC placement, and validation. Convert one only when it hosts
  something a menu cannot express.
- **Concealment must not be implemented as deletion.** Hiding a building's
  interior by not generating it until entry looks like an optimisation and breaks
  determinism, the spoiler log, and headless validation all at once — the
  validator cannot walk a town whose insides do not exist yet. Generate
  everything at world creation; conceal at the presentation layer only, and treat
  guarantee 61's "once revealed stays revealed" as save state.
