# TODO: Feel improvements from assets we already own

## Goal
Use animations, VFX, audio, icons and props that are already in the project but never shown or played, so combat, movement and dungeons feel more alive. No purchases are needed.

Source: `TODOs/10-art-asset-audit.md`. The town dressing item lives in `TODOs/09-diorama-restyle-plan.md` Phase 3 (see the last section).

**Rule for every phase:**
- Existing gameplay timing must not change.
- New animations and sounds are skipped when `DungeonPreferences.AnimationMode == NoAnimations`.
- Generation stays deterministic for a seed.

## Phase 0: Prerequisite (bug fix)
- [ ] Clean the hero clip pools. `HeroAnimator.SetAnimations` (`Assets/Scripts/Town/HeroAnimator.cs:193`) matches clips by substring, so `Die` includes `Die01Stay_*` (a held dead pose), MagicWand `Attack` includes `Attack03_Start`/`_Maintain`, and SingleSword `Idle` includes `Idle_Battle_DoubleSword`.
  - [ ] Exclude `*Stay*`, `*_Start`, `*_Maintain` and clips from other stances.
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
- [ ] Status loops: add an optional `AnimatedAction? Loop` to `StatusVisualProfile` (`Assets/Scripts/Dungeon/Game/StatusVisualProfile.cs`), so statuses choose a loop without code per status.
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
- [ ] Remove `CombatVisualCatalog.ImportedEffects` and `CombatEffectAuthoring.cs:64` in the same change, so only wrapped prefabs ship (audit #1).
- [ ] Check for double audio: catalog VFX with play-on-awake `AudioSource` plus `Skill.CastSound` (`SkillAction.cs:95`, `CastSpellAction.cs:20`). Keep one source per skill.
- [ ] Test: every status in `Resources/CombatEffects/Statuses` resolves to a profile, and no two statuses with different families share an aura unless they are listed as an allowed pair.

## Phase 4: Status icons
Statuses show as 2-letter text badges (`Assets/Scripts/Dungeon/Game/UI/DungeonPartyCard.cs:86-90`).
- [ ] Add `Sprite Icon` to `StatusVisualProfile`.
- [ ] Pick icons from `RPG_skills_and_abilities`: the `red/` folder is fully unused, and about 1,950 icons are unused overall. For buffs and debuffs, Bamao `Equipment 2/State_{HP2,attack2,defense2,speed2,add,Decline}.png` also fit.
- [ ] Assign them in `CombatEffectAuthoring` alongside the aura, and avoid icons already used by skills.
- [ ] `DungeonPartyCard`: show the icon, with the turn count as a small overlay. Keep the text badge as the fallback.
- [ ] Enemy status display: show the same icon where enemy statuses are shown.
- [ ] Test: every status profile has an icon, and no icon is shared between statuses.

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
- [ ] See `TODOs/09-diorama-restyle-plan.md` Phase 3 step 3, which is now "reuse first". Place the TownInteriors Barrel, Crate, Sack, Bench, Basket, Lantern, Plant, PerchSign and Banner outdoors through `TownEnvironmentLayer`. Author only the fence, flower box, mailbox and boulders. Replace the `HomeBed.cs` cube bed with `TownInteriors/Bed.prefab`.

## Order
Phase 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7, one commit each, with EditMode and PlayMode suites green (`Tools/harness-*.json` via `Tools/unity-mcp.mjs`). Phases 4–7 depend on nothing earlier and can be done in parallel.
