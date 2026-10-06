# Art asset audit (2026-10-06)

> Pending findings and supporting evidence. Checkmark glyphs below mark findings,
> not completed fixes. Tasks overlapping priorities 03/04/05/09 are tracked there.
> Revalidate historical dependency counts before asset cleanup.

## Scope and method
**Scope:** every third-party and generated art or audio pack under `Assets/`. Read-only; no assets were changed.

**Method:**
- A GUID reference index over all YAML assets and `ProjectSettings`, plus a grep of scripts for asset paths.
- One deep-dive per pack group: characters and VFX, UI/2D/audio, environment, props.
- Spot-checked claims are marked ✔.

**Related plans** (findings already covered there are not repeated):
- `TODOs/09-diorama-restyle-plan.md`
- `TODOs/04-weapon-availability-plan.md`
- `TODOs/03-input-prompts.md`

## Top 10 by value / effort

| # | Finding | Fix | Effort | Value |
|---|---|---|---|---|
| 1 | ✔ `Resources/CombatEffects/Catalog.asset` `ImportedEffects` lists **517 raw VFX prefabs** (all of MagicArsenal + 52SpecialEffectPack). It is written by `Editor/CombatEffectAuthoring.cs:64` and **never read** (only declared in `CombatVisualCatalog.cs:18`). Because it sits in Resources, about 470 prefabs, 126 textures and 27 wavs ship in every build and load with the catalog. | Remove the field and line 64, then re-save the catalog. The game uses only the 47 copies in `Prefabs/CombatEffects/`. | S | High (build size, WebGL memory) |
| 2 | ✔ The hero clip pools contain wrong clips. `HeroAnimator.SetAnimations` (`Town/HeroAnimator.cs:193`) matches clips by substring, so `Die` picks up `Die01Stay_*` (a held dead pose: about 1 in 3 deaths skip the fall). MagicWand `Attack` includes `Attack03_Start`/`_Maintain`, and SingleSword `Idle` includes `Idle_Battle_DoubleSword`. | Exclude `*Stay*`, `*_Start`, `*_Maintain` and clips from other stances, then re-run Set Animations on hero prefabs. | S | High (visible bug) |
| 3 | ✔ All 8 dungeon `*_Interior_Boundary.mat` share `PaintedEnvironment/Wall.png`. `PaintedEnvironmentAuthoring.cs:193` hard-codes `"Wall"`, while the biome wall textures `DungeonThemes/Source/{Masonry,MossMasonry,IceMasonry,BasaltEmbers}.png` are generated but have 0 references. | Map the biome to its texture in `:193`: Grassland/Desert → Masonry, Forest/Marsh → MossMasonry, Tundra → IceMasonry, Volcanic → BasaltEmbers. | S | High (dungeon variety) |
| 4 | **NPC portraits are never shown.** `TownNpcDefinition.Portrait` is set for 7 NPCs, but `TownNpc.Greet` (`Town/TownNpc.cs:13`) shows text only. Bamao `QUEST/avatar_bg_*` frames are unused. | Show the portrait in the `TownMenu.ShowMessage` greeting, framed. | S | High |
| 5 | **UI confirm/cancel sounds are assigned but never played.** `Confirm`/`Decline`/`Denied` are set in `Scenes/Common.unity`, but the only UI sound is the hover in `NavigationHandler.cs:54`. | Play them on submit, cancel and invalid actions (`MenuUIInputModule` / `GameUISkin.Button`). | S | High |
| 6 | **Unused hero animations with ready hooks.** Victory, LevelUp, Defend, Dizzy, Combo, Dash, DrinkPotion and Greeting are all in the pools but never played (only 5 of 13 actions are used). | LevelUp at `MovementAction.cs:570`; Victory on floor clear; Defend for Guard/Parry/Bulwark; Dizzy for Stun/Confusion; Combo/Dash for Double Strike, Whirlwind, Lunge and Shadow Step; DrinkPotion on potion use. | M | High |
| 7 | **VFX sameness.** 20 of 31 statuses use one aura, `AuraSimpleShadow` (`CombatEffectAuthoring.cs:137`), and skills share one effect per family. Fitting MagicArsenal families are unused: Curse, DoT, Shields, Enchant, Walls, Pillar Blast, Beams, Slash, Charge, Orbital. | Add per-status and per-skill overrides in the authoring script and wrap only the prefabs that get picked. | M | High |
| 8 | **Footsteps.** `Sounds/RPG_Essentials_Free/12_Player_Movement_SFX` has 12 step/jump/landing clips; none are used. | Add a footstep per surface or biome on move: dungeon (`MovementAction`), town player, overworld. | M | High |
| 9 | **Lighting mismatch between characters.** ✔ The hero `DefaultPolyart.mat` uses Standard (lit, `_EMISSION`). The enemy `PolyartDefault.mat` and every Adorable item material use built-in shader 10752 (Unlit/Texture, per two audits). Enemies and floor items ignore scene lighting. | Pick one lighting model before the diorama restyle (add a lit material variant in our own folder). | S | Medium–High |
| 10 | **Status effects have no icons.** They are 2-letter text badges (`Dungeon/Game/UI/DungeonPartyCard.cs:86-90`). Sources: `RPG_skills_and_abilities` (1,946 unused icons; `red/` fully unused) and Bamao `Equipment 2/State_*`. | Add `Sprite Icon` to status effects and show it on party cards. | M | Medium–High |

## Characters and VFX
- **Enemy animations.** `Enemy.cs:178-219` plays only Idle, Walk, Attack, GetHit and Die. Every monster also has Taunt, Victory, Dizzy, SenseSomethingStart and IdleBattle. Hooks: wake from dormant (`Enemy.cs:157`), the Taunt status, Stun, and a hero going down. (M / Med)
- **Mask-tint is unused in gameplay.** Every monster has a `*PAMaskTint.prefab` (`RPGMonsterBundlePolyart/CommonStuffs/Prefab/Wave0x`). The shader already works in the main menu.
  - Golem and StingRay are each spawned in two floor bands with the same prefab, which are natural slots for tinted elite or biome variants.
  - The hero `PolyartMaskTint.shader` could also give per-recruit colour variety; the 24 `Ally_MC*` looks are fixed. (M / Med)
- **Dead enemies.** `Enemy_MushroomSmile` and `Enemy_LargeSlime` are never spawned. LargeSlime looks like a duplicate of `Enemy_Slime_Big`. Spawn or delete them. (S / Low)
- **RPGHero placeholder ships.** `Prefabs/Dungeon/Ally.prefab` nests `RPGHeroHP` (a 2048² hand-painted texture), only for it to be destroyed by path (`Ally.cs:321`, `AllyGenerator.cs:37`).
  - Replace it with an empty anchor of the same name.
  - Delete the unused `Prefabs/Dungeon/Enemy.prefab`.
  - `DroppedItems/Sword.prefab` uses Unity's default material.
  - After Diorama Phase 6, the whole RPGHero pack (52 MB) can go. (S / Med)
- **Skill icons are reused.** 201 skills use 131 icons; the `blue/9954` fallback (`CombatEffectAuthoring.cs:211`) covers 15 skills. (S / Med)
- **Double skill audio (needs a listen test).** 120 catalog VFX prefabs contain play-on-awake `AudioSource`s, and skills also play `CastSound` (`SkillAction.cs:95`, `CastSpellAction.cs:20`). Choose one source per skill. The unused MagicArsenal Cast/Impact wavs are per element. (S–M / Med)
- **Hygiene:**
  - `AllyGenerator.RemoveAllTransitions` edited the vendor controllers in place; re-importing the pack would undo it.
  - There are two arrow visuals: `Projectile_Arrow` and `Cyclops_BigArrow`.
  - Orphan `.zip.meta` / `.unitypackage.meta` files.
  - Vendor demo scripts still compile. (S / Low)

## UI, 2D, audio
- **Music.** The dungeon uses one track for every theme and boss. `Ambush Transition.mp3` and Bamao `Guitar-Gentle.wav` are unused. Add a music field per `DungeonThemeCatalog` theme, plus a boss/ambush cue. (S / Med)
- **Music import settings.** 11 mp3s are Decompress On Load at quality 1.0. Add a Standalone override: Streaming at about 0.5. Short SFX could use ADPCM. (S / Med)
- **Player-facing IMGUI.** The autoplay panel and the "Stop autoplay?" prompt (`Common/AutoplayRunner.cs:725-790`) use `GameSkin.guiskin`, which falls back to built-in Arial. Rebuild them with `GameUISkin`. (M / Med)
- **2D item icons.** Bamao `Shop/icon_*` (about 40: swords, shields, armour, mana potions, bread, meat, cheese, keys, bombs, crystals) can fill `ItemDefinition.Icon` where Diorama Phase 6 has no 3D model. (S / Med)
- **Button-prompt glyphs.** None exist in the project. `InputPromptsTODO` needs an external CC0 set (e.g. Kenney Input Prompts) built into a TMP sprite asset. (M / Med)
- **Fonts.** The Bamao "Magical Neverland" font is applied only by an editor pass (`Editor/GameUIButtonAuthoring.cs:133`) to labels named "title"/"header", and has no fallback. Apply `GameUITheme.HeadingFont` everywhere, and add a fallback font. (S / Low–Med)
- **Sprite atlases.** There are none. Atlas the roughly 57 used Bamao sprites, `Resources/UI` and the portraits. (S / Low–Med)
- **Class icons** load by display name (`MainMenu/ProtagonistClassPicker.cs:74`); renaming a class breaks the icon. Use a Sprite field instead. (S / Low)
- **Unused audio:**
  - Battle `Claw`, `Bite`, `Block`, `Encounter` are assigned but never played.
  - All 12 Bamao UI/object sounds are unused; "Coin Pop" fits `Gold.cs:13`.
  - `LevelUp` reuses `48_Speed_up_02`.

## Environment
- **Thin dungeon decorations.** `DungeonPresentation.Decorate` (`:118-147`) uses `kit.Models` only, with a cap of 16 per chunk and 120 triangles. Several themes get only `Rock`.
  - Unused fits: `DeadTree`, `SnowRock`, `Shrine`, `Flowers`, `MountainSpires`/`Ridge`, `Willow`.
  - The palette-batched TownInteriors furniture also fits: Barrel, Crate, Sack, Candles, Books, Lantern, Sconce, Banner, Chest, ArcanePedestal, Lectern.
  - Let `Decorate` resolve ids from `TownInteriorCatalog`, raise the cap to about 250 triangles, and give each theme 4–6 ids. (M / High)
- **Town dressing already exists.** Diorama Phase 3 plans to author barrel, crate, bench and signboard in Blender, but TownInteriors already has Barrel, Crate, Sack, Bench, Basket, Lantern, Plant, PerchSign, Banner and birds (XY/−Z, palette). Reuse them in `TownEnvironmentLayer`; author only the fence, flower box and mailbox.
  - `HomeBed.cs:26-39` builds the bed from cubes plus `Shader.Find("Standard")` at runtime, although `TownInteriors/Bed.prefab` exists. (S–M / High)
- **Baked previews in build scenes.** `Town.unity` references 124 `EnvironmentKit/TownPreview/Chunk*.asset` meshes (41 MB of YAML), even though `Town.cs:301-305` rebuilds the town at runtime. `DungeonScene.unity` embeds 54 baked meshes that use TWC demo materials. Tag them `EditorOnly` or strip them in a build preprocessor. (S / Med–High)
- **TileWorldCreator samples:**
  - About 190 MB is completely unreferenced: `Version 2 Tiles/SciFi`, `CliffIsland`, `2DIsland`, `Prototype`, `Version 3/6-Tiles`.
  - `V2 Dungeon` and `V3 4-Tiles` are reached only through legacy preset slots and inactive layers, but they pull 18 realistic 1024² demo textures into the build (also used by `Art/MainMenu/*_mat_Dungeon_*`).
  - Repoint those slots, then delete. `DungeonThemeTests:86` needs updating. (S / Med)
- **WebGL texture memory.**
  - `EnvironmentKit/Textures/Buildings_{8 biomes}.png` are 2048² with no WebGL override, and `Kit.asset` loads all 8 atlases.
  - `PaintedEnvironment/BiomeDecorations.png` and `DungeonProps.png` are 2048² as well.
  - Add 1024 WebGL overrides, or move to one atlas plus a tint (this fits Diorama's atlas-swap idea). (S–M / Med)
- **EnvironmentKit duplicates.** 476 biome prefabs (`Prefabs/<Biome>/`, everything except Grassland) are used only by `EnvironmentPlayground.unity`. 68 `Models/*.fbx` have been copied into `Meshes/*.asset`. Archive them after updating the playground and `EnvironmentKitTests`. (S / Low–Med)
- **Overworld markers** (`Overworld/{Town,Gate,Dungeon,Landmark}.prefab`) are primitives combined with kit meshes. This is evidence for Diorama Phase 5.

## Props
- **The wrong Adorable variant is used.** Every reference points at `Adorable 3D Items` (one material and texture per model: 212 materials, 143×512² + 54×1024²). The identical `Adorable 3D Item_Atlas` (5 materials, 3×2048² atlases) is unused. Repoint the dropped items, `UnifiedPresentationAuthoring.cs:18` (bag) and `Enemy_ChestMonster` to the atlas prefabs with one lit material variant in our own folder, then drop the other folder. **This changes Diorama Phase 6 step 2.** (S–M / High)
- **Adorable native scale varies widely** (dropped-item root scale ranges from 1.14 to 6.9). Use the `DioramaScale` table when it lands. (Med)
- **Quest/interaction markers.** Adorable `exclamation mark` / `question mark` as bobbing markers over service NPCs and doors; no marker system exists today. (S / High)
- **Themed shop counters.** Goods are only `Basket`/`Equipment`/`Potions` (`Core/.../TownInteriorGenerator.cs:73`). Rotate in cake, waffle and egg (bakery) and potion_red, potion_blue and jar (consumables). (M / High)
- **Results screen.** Use a trophy, star or treasure chest icon on `GameOverScreen.cs:62` (text only today). (S / Med)
- **Ambient critters.** Use species the town lacks (hen, duck, butterfly, bee, ladybug). Skip cat, dog, sheep and rabbit, which duplicate the animated NPCs. (M / Med)
- **Skip the Adorable furniture.** It duplicates the authored interiors and would break palette batching.
- **KennyNL is Castle Kit 1.2 (CC0).** It is used for the main menu castle and some town building prefabs; the Shop prefab's renderers are hidden by `TownBuildingManager.cs:34-53`. Its towers, walls, flags and siege engines fit overworld castle landmarks or boss-floor dressing (Diorama Phase 5). Clean up the fbx/obj duplicates and the orphan `.unitypackage.meta`. (M / Med)

## Safe deletions (0 references found)
**Repo/import weight only** (they don't ship today):
- `Tiles/` (237 files, 80 MB: a legacy 2D tilemap pack)
- `Prefabs/Dungeon/Dungeon.prefab`, `_TileWorldCreator_Dungeon.prefab`, `Art/Torch.mat`, `Scripts/DungeonGenerator.cs`
  - Keep `Scripts/Dungeon/Tile/*Tile.cs`; `Dungeon.cs:192-222` uses `InteractableTile`.
- `Art/DungeonTexture/` (old blueprint PNGs + PSD)
- `_Recovery/*.unity` (crash-recovery scenes)
- `unity-wave-function-collapse/` (8 scripts, unused, but compiled into Assembly-CSharp)
- `Overworld/Biome*.png` (11 swatches) and `Overworld/BiomeRoad.mat`; drop `BiomeRoad` from `PaintedEnvironmentAuthoring` too
- The unreferenced TWC samples listed above (about 190 MB)
- `RPG_skills_and_abilities` contact sheets (`All.png` 7000×4281 + six at about 3150²; 555 MB source)
- `Sounds/Farts/*` and `FartScene.unity`: **not safe.** FartScene is the splash scene loaded by `Common.cs:62` (`LoadScene(1)`); confirm with the user before touching it.

Re-run the GUID index before deleting anything, and delete in one commit per pack with EditMode and PlayMode suites green.

## Suggested order
1. Quick wins (one PR each): #1 ImportedEffects, #2 clip pools, #3 wall textures, #4 portraits, #5 UI sounds, the Ally placeholder, music import settings.
2. Build hygiene: preview meshes `EditorOnly`, TWC demo slots, then safe deletions.
3. Feel: hero/enemy animation hooks, footsteps, status icons, VFX overrides, music per theme.
4. Fold into Diorama: the lighting model (#9), Adorable atlas + markers + counters (Phase 6), TownInteriors reuse (Phase 3), Kenney landmarks (Phase 5), WebGL texture overrides.
