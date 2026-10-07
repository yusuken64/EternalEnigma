# TODO: Feel improvements from assets we already own

## Goal
Use animations, VFX, audio, icons and props that are already in the project but never shown or played, so combat, movement and dungeons feel more alive. No purchases are needed.

Source: the asset audit section of `TODOs/09-diorama-restyle-plan.md`. The town dressing item lives in that plan's Phase 3.

**Rule for every phase:**
- Existing gameplay timing must not change.
- New animations and sounds are skipped when `DungeonPreferences.AnimationMode == NoAnimations`.
- Generation stays deterministic for a seed.

## Phase 0: Verify the completed clip-matching fix
`HeroAnimator.MatchesClip` now excludes held poses, start/maintain clips and clips from the wrong stance. The serialized pools still need verification.

- [ ] Confirm the exclusion code remains in place.
- [ ] Re-run "Set Animations" on `Prefabs/Dungeon/Player.prefab`, `TownPlayer` and every `Prefabs/Town/Allies/Ally_MC*.prefab`.
- [ ] Test: each stance has at least one clip for each used action, and no clip name contains `Stay`, `_Start` or `_Maintain`.

## Phase 1: Hero animations
Pools already exist for Victory, LevelUp, Defend, Dizzy, Combo, Dash and Jump. NoWeapon also has DrinkPotion and Greeting.
- [ ] Add `HeroAnimator.PlayOneShot(AnimatedAction, fallback = Idle)`. It plays the clip and returns to idle; if a stance has no clip for that action, it plays nothing.
- [ ] **LevelUp:** play in `LevelUpAction` (`Assets/Scripts/Dungeon/Actions/LevelUpAction.cs:32`, next to the existing LevelUp sound).
- [ ] **Victory:** play on all living heroes when a floor or dungeon is cleared (`GameOverScreen.cs:62` path / floor transition).
- [ ] **Defend:** loop while a guard-type status is active (`ParryStatusEffect`, `DamageReductionStatusEffect`, Bulwark/Guard skills). Use DefendHit for `GetHit` while defending.
- [ ] **Dizzy:** loop while `ParalysisStatusEffect` or `ConfusionStatusEffect` (and Stun) is active.
- [ ] **Sleeping:** loop while `SleepStatusEffect` is active (already used by `HomeBed.cs:79`).
- [ ] **Combo:** use for multi-hit skills (Double Strike, Whirlwind) instead of `Attack`.
- [ ] **Dash:** use for movement skills (Lunge, Shadow Step).
- [ ] **DrinkPotion:** use when a hero uses a potion item. The clip is NoWeapon-only, so hide the weapon for its duration or skip it in other stances.
- [ ] Configure the existing `StatusVisualProfile.Loop` and `HasLoop` fields so statuses choose a loop without code per status.
- [ ] Tests: each hook fires once per event, and the hero returns to the right idle stance afterwards.

## Phase 2: Enemy animations
`Enemy.cs:178-219` plays only IdleNormal, Walk, Attack, GetHit and Die. Every monster also has Taunt, Victory, Dizzy, SenseSomethingStart and IdleBattle.
- [ ] **SenseSomethingStart:** play once when a dormant enemy wakes (`Enemy.Provoke`, `Enemy.cs:158`).
- [ ] **IdleBattle:** use instead of IdleNormal while the enemy is awake and a hero is in range.
- [ ] **Taunt:** play when the enemy applies the Taunt status.
- [ ] **Dizzy:** loop while the enemy is stunned, paralysed or confused (reuse the `StatusVisualProfile.Loop` from Phase 1).
- [ ] **Victory:** play on nearby enemies when a hero goes down.
- [ ] Make `AnimationStates(name)` fall back to IdleNormal when a monster lacks a state, and log it once in the editor.

## Phase 3: Status VFX
20 of 31 statuses use `AuraSimpleShadow`: `StatusFamily()` (`Assets/Scripts/Editor/CombatEffectAuthoring.cs:137`) assigns one aura per family.
- [ ] Add a per-status override table in `CombatEffectAuthoring`, checked before `StatusFamily()`.
- [ ] Use unused MagicArsenal families (wrapped into `Prefabs/CombatEffects/` like the existing 47; lights and scripts stripped, audio routed to the effects mixer):

  | MagicArsenal family | Statuses |
  |---|---|
  | `Curse/` | `CurseStatusEffect`, Weaken, `FrailtyStatusEffect` |
  | `DoT/` | `DotStatusEffect`, `BurnStatusEffect` |
  | `Shields/` (Dome) | `BarrierStatusEffect`, `DamageShieldStatusEffect` |
  | `Orbital/` | `SongStatusEffect` |
  | `Enchant/` | Blazing, Frost and Storm Arms buffs |

- [ ] Stun, Sleep and Confusion: use an overhead effect (a dizzy ring or "Zzz") instead of a ground aura.
- [ ] Do the same for key skills (per-skill override): `Walls/` for Sanctuary Wall and Flame Barrier, `Pillar Blast/` for Thunderclap and Tempest, `Beams/` for Siphon, `Slash/` for melee, and `Charge/` for the cast wind-up instead of `SummonMagicCircle` everywhere.
- [ ] Confirm the already removed `ImportedEffects` field stays absent and measure actual build inclusion after the VFX overrides are authored.
- [ ] Check for double audio: catalog VFX with play-on-awake `AudioSource` plus `Skill.CastSound` (`SkillAction.cs:95`, `CastSpellAction.cs:20`). Keep one source per skill.
- [ ] Test: every status in `Resources/CombatEffects/Statuses` resolves to a profile, and no two statuses with different families share an aura unless they are listed as an allowed pair.

## Phase 4: Validate existing status icons
`StatusVisualProfile.Icon` is populated on the current status assets, and party/enemy status displays use it with a text fallback.
- [ ] Check icon readability, turn-count overlays and text fallback in both party and enemy UI at gameplay scale.
- [ ] Test: every status profile resolves to an icon, and unrelated statuses do not share one unintentionally.

## Phase 5: Footsteps and movement sounds
`Assets/Sounds/RPG_Essentials_Free/12_Player_Movement_SFX` has 12 clips (steps on grass, rock, wood and water, plus jump, landing and dive); none are used.
- [ ] Add `Footstep` clips per surface to `SoundEffects` (`Assets/Scripts/MainMenu/SoundEffects.cs`): Grass, Rock, Wood, Water.
- [ ] Map surfaces:
  - dungeon: by `DungeonTheme.Environment`/`Biome`
  - town: Parks → Grass, Roads → Rock, interiors → Wood
  - overworld: by biome
- [ ] Play one step per cell moved:
  - dungeon `MovementAction` (leader only; quieter for allies or none)
  - `Town/TownPlayer.cs`
  - `Overworld/OverworldScene.cs`
- [ ] Throttle so fast or continuous movement doesn't stack sounds, and vary the pitch slightly with a deterministic hash.
- [ ] Use the Jump/Landing clips for Dash/Lunge skills (with the Phase 1 Dash animation).

## Phase 6: Music per theme
The overworld chooses music per biome (`OverworldScene.cs:75-76`). The dungeon uses one track (`MusicBox_07`, set in `Scenes/DungeonScene.unity`) for every theme and boss.
- [ ] Add `AudioClip Music` and `AudioClip BossMusic` to `DungeonTheme` (`Assets/Scripts/Environment/DungeonThemeCatalog.cs:9`).
- [ ] Fill them in `Resources/DungeonThemes/Catalog.asset` from the 10 existing tracks in `Assets/Sounds/Music`, reusing the biome's overworld track or a darker variant.
- [ ] Play through `AudioManager.PlayMusic` (`Assets/Scripts/MainMenu/AudioManager.cs:38`) when a floor loads; switch to `BossMusic` on throne/boss floors.
- [ ] Wire the unused `Ambush Transition.mp3` as a cue when an ambush or monster house triggers.
- [ ] Consider Bamao `Sound/Music/Guitar-Gentle.wav` for the town or inn interior.
- [ ] Also fix the import settings (audit): Standalone override set to Streaming at quality about 0.5; leave WebGL as is.

## Phase 7: Dungeon decoration
Verified during task 09's art integration (2026-10-06): HEAD already contains TownInteriorCatalog resolution, varied theme lists, source-appropriate materials and generated biome wall textures. Task 09 keeps compact dungeon theme props separate from the larger overworld tree picker and permits up to 250 triangles per piece outdoors as well as indoors. Focused dungeon-theme checks passed in the 22/0 followup run. Task 09 tracks the final player/build regression gate; the sound/animation checklist above needs its own reconciliation with the existing feel-improvements commit.

`DungeonPresentation.Decorate` (`Assets/Scripts/Environment/DungeonPresentation.cs:118-147`) places only `kit.Models`: at most 16 per 32×32 chunk and at most 120 triangles each. Several themes in `Resources/DungeonThemes/Catalog.asset` list only `Rock`.
- [ ] Let `Decorate` resolve decoration ids from `TownInteriorCatalog` (`Assets/Scripts/Town/TownInteriorCatalog.cs`) as well as the kit. These pieces are palette-textured and XY/−Z already.
- [ ] Raise the triangle cap to about 250 for interior themes. Keep 120 for outdoor themes, or measure.
- [ ] Give each theme 4–6 ids, for example:
  - tomb/crypt: Candles, Sconce, Banner, Books, ArcanePedestal, Chest
  - mine/cave: Barrel, Crate, Sack, Lantern
  - outdoor: `DeadTree`, `Flowers`, `Shrine`, `Willow`, `SnowRock` (Tundra), `MountainSpires`/`MountainRidge` (border)
- [ ] Place by deterministic hash on decorative cells only. Decorations add no collision and never cover stairs, items or interactables.
- [ ] Use the right material per source: the TownInteriors palette for interior pieces, the theme `DecorationMaterial` for kit pieces.
- [ ] Apply the biome wall textures in the same change (audit #3): change `PaintedEnvironmentAuthoring.cs:193` so `"Wall"` becomes Masonry, MossMasonry, IceMasonry or BasaltEmbers by biome.
- [ ] Tests: decoration count and triangles per chunk stay within budget, the same seed gives the same layout, and no decoration lands on a protected cell.

## Town dressing (tracked in the Diorama plan)
- [x] Implemented in `TODOs/09-diorama-restyle-plan.md` Phase 3: TownInteriors furniture is reused outdoors, missing forms are authored, and `HomeBed.cs` uses the authored bed. Eight-biome views and roof/sleep regression are retained with task 09.

## Order
Phase 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7, one commit each, with EditMode and PlayMode suites green (`Tools/harness-*.json` via `Tools/unity-mcp.mjs`). Phases 4–7 depend on nothing earlier and can be done in parallel.
