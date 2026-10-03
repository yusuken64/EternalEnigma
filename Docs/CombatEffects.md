# Combat effects and ability icons

Assignment, preview and validation tools remain for ongoing content editing. The save cleanup
does not change combat effect profiles, playback ordering or current-schema item snapshots.

Each `Skill` owns an explicit `VisualProfile` and `Icon` reference. Profiles are shared ScriptableObjects; editing one changes all skills using it. All 202 existing skill assets, including passives, have icons from `Assets/RPG_skills_and_abilities`. Passive profiles are available for authoring; passive training does not automatically play a cast animation.

## Authoring

Open **Tools → Eternal Enigma → Combat Effects → Assignments And Preview**. The ability tab edits icon/profile references, while the other tabs locate character attack bindings, status auras, and profiles. Use **Save** to persist edits. Select a profile to edit its five stages in the Inspector:

1. **Muzzle** and **GroundCircle** start at the caster.
2. **Projectile** flies to the recorded recipient or missile endpoint.
3. **Impact** plays for successful results; **Area** plays once for an area activation.

Stages are optional. Each has an independent prefab, offset, rotation, scale, delay, and lifetime. Cast and impact holds, projectile speed, and minimum/maximum flight times live on the profile. Ground effects use the dungeon's XY ground plane. Projectile rotations correct the source prefab's forward axis after aiming it at the target.

All hit impact profiles enable `FitToTarget`: model bounds are captured with each resolved hit, excluding selection sprites and particles. Playback places body impacts in front of those bounds along the camera viewing direction, keeps them centered on screen, and scales them up for large bodies. Impacts with a ground footprint (circles, portals, and runes) scale with the target while staying on the dungeon floor. The authored scale remains the minimum for small targets. Characters without mesh bounds fall back to their grid footprint. Visibility is checked at the original hit position. New profiles and generated assignments also default to adaptive impacts.

Circles, area stages, and persistent auras have a minimum **3×3 tile footprint**. `MinimumDiameterCells` and the source `ReferenceDiameter` enforce that minimum using the dungeon's cell size, even if the stage's ordinary scale is smaller. Larger area skills scale further. Particle growth/fade curves still animate naturally inside the configured footprint.

Rotation corrections are composed with the prefab's original root rotation. This matters for Magic Arsenal ground meshes whose roots are already tilted. Legacy horizontal billboards, which otherwise ignore root rotation, are converted to the dungeon plane in the owned variants. Explicit footprint/orientation repair commands are available under the same menu; the orientation command resets customized ground rotations.

Profiles and status definitions live in `Assets/Resources/CombatEffects`. Project-owned prefab variants live in `Assets/Prefabs/CombatEffects`; they reference both Magic Arsenal and the 52 Special Effect Pack. Demo movement/input scripts, colliders, and lights are removed from these variants. Imported pack originals remain available in the catalog.

**Assign Missing Profiles And Icons** fills missing references and creates missing defaults. It preserves existing manual assignments and profile tuning. **Validate Assignments** checks coverage; the assignment tests also verify character/status coverage, icon provenance, and safe effect variants. **Export Preview Gallery** writes representative preview PNGs into `Temp/CombatEffectPreviews`.

## Runtime

`CharacterCombatEffects` defines melee, ranged, confusion, root, and theft profiles, plus an optional cast socket. The catalog supplies defaults for dynamically created characters and explicit type mappings for procedural statuses. Status prefabs can override their type's `StatusVisualProfile` directly.

`CombatVisualReplay` captures outcomes around action resolution and passes a shared visual sequence to generated actions. Casting happens once, damage impacts use resolved recipients and misses, individual hits retain their ordering, and missile area attacks have one flight. A secondary status on a damage recipient does not fire another projectile. Existing simulation, targeting, damage, and costs remain authoritative.

Status presentation snapshots are recorded during resolution and applied in replay order. They do not read future status state during playback. Shared aura profiles are deduplicated; up to three aura groups are shown, ordered by priority and then name. Refreshes reuse instances, and expiry, cleansing, death, and song replacement update the ledger. Skipped playback advances status state without transient effects or delays.

The preexisting particle and trail components have been removed from status prefabs. Their status scripts and text indicators remain; persistent particles come exclusively from the new aura profiles.

`CombatEffectPlayer` owns the scene-local particle pool, clears trails/particles on reuse, follows aura anchors, respects fog, and clears effects on floor changes or scene teardown. Legacy skill and projectile playback remains available for unassigned content.

Ability icons appear in dungeon skill menus, town skill lists, trainer rows, and purchase dialogs. The shared row helper creates a non-interactive icon slot for older menu prefabs.

## Checks

- EditMode: `CombatEffectAssignmentTests`.
- PlayMode: **Tools → Eternal Enigma → Tests → Run Combat Effects**.
- Integration: **Run Combat Effects Regression** includes combat, visibility, movement, and song/command tests.
- Harness results are written to `Temp/HarnessResults` across Unity domain reloads.
