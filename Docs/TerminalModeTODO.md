# TODO: Terminal mode (play the full game as a simulated console)

## Goal
A "Terminal mode" option in the Unity client plays the **whole game** as colored ASCII instead of 3D:
- Covers overworld, towns, dungeon combat, items and allies.
- Looks like a full-screen TMP text block on a Canvas, in a monospace font, with `<color>` tags per glyph.
- Saves are shared with 3D mode, so the same campaign can be continued in either mode.

## Approach
Terminal mode is **a different presentation over the normal scenes**, not a separate client.
- Town, Overworld and DungeonScene load exactly as they do today, with all their MonoBehaviours.
- Terminal mode turns off world rendering and draws a TMP console from the live state.
- Input keeps going through the existing handlers.

## Current state (why this approach)
- **The console Explorer can't be the base.**
  - It is net10.0 and has no combat.
  - It keeps its own progression state (`ExplorerSession`), which doesn't match `GameSaveData`.
  - Its **renderers** are Console-free `string[]` producers and are worth reusing for glyph rules: `MapRenderer`, `TownRenderer`, `DungeonRenderer`, `TownServiceGlyphs`.
- **Dungeon rules can't run outside the scenes.**
  - Most of the ~19k LOC in `Assets/Scripts/Dungeon` sits on MonoBehaviours and ScriptableObjects: `Character`, `StatusEffect`, `Inventory`/`Equipment`, `Skill`, `ItemDefinition`, plus `Game.Instance`.
  - The rules also use `UnityEngine.Random` (48 uses), `Mathf` and `Vector3Int`.
- **Town services need live objects.** `TownServices` depends on `Town`, `TownPlayer` and instantiated `TownAlly` prefabs.
- **The rules already resolve on a logical grid before any animation.**
  - `TurnManager.ProcessTurnRoutine` runs `ExecuteActionImmediate` and then `Replay`.
  - Characters carry `TilemapPosition`, and `TileWorldDungeon` exposes a floor mask.
  - Progression lives in Core's `CampaignContext` (`Common.Instance.CampaignContext`).
- **Something other than the player can already drive the game.** `AutoplayRunner` uses `TurnManager.SubmitCommand`, and `DungeonAnimationMode.NoAnimations` exists.
- **No monospace TMP font exists.** The only fonts are LiberationSans SDF and Magical Neverland SDF.

## Phase 1 — Playable terminal (~1 week)
Existing keyboard input works unchanged. In this phase, uGUI dialogs and the HUD still draw on top of the terminal.

- [ ] **Font:** add an OFL monospace TTF (JetBrains Mono / DejaVu Sans Mono) under `Assets/Art/Fonts/`, plus a dynamic TMP SDF asset with ASCII and box-drawing glyphs.
- [ ] **`Assets/Scripts/Terminal/TerminalMode.cs`:** a static `Enabled` flag saved to PlayerPrefs (same style as `DisplayPreferences`/`DungeonPreferences`), with a `Changed` event.
- [ ] **`TerminalFrame.cs`:** a cell buffer (`char` + `Color32`) with `Put`, `Write`, `Box` and `Fill`.
  - `ToRichText()` merges same-color runs into one `<color=#rrggbb>` tag.
  - It reuses a `StringBuilder` and returns a cached string when nothing changed.
- [ ] **`TerminalPalette.cs`:** one place for glyph colors (water blue, trees green, gates red/green, warps cyan, enemies red, …).
- [ ] **`ITerminalView` + `TerminalScreen.cs`:** lives in `Common.unity` as a child of the persistent `Common`.
  - A Screen Space Overlay canvas with a black background and one `TextMeshProUGUI`.
  - Its sort order sits **below** the game UI canvases, so Phase-1 dialogs show on top.
  - Columns and rows come from the font's advance and line height at the current resolution.
  - It asks the active view to fill the frame and updates the text only when it changed.
- [ ] **`TerminalCameraGuard.cs`:** on `SceneManager.sceneLoaded`, turn off world cameras (keep UI cameras) while terminal mode is on, and restore them when it is turned off. Duck positional VFX audio if needed; music stays.
- [ ] **`DungeonTerminalView`**
  - Map: `Game.Instance.CurrentDungeon` (floor mask / `IsWalkable`) plus `Interactables` (stairs, traps, items, chests).
  - Characters: `AllCharacters[].TilemapPosition`. The leader is `@`, allies `1`–`3`, and enemies get one letter per type.
  - Fog: `PartyVisibleTiles`; tiles seen earlier are drawn dim.
  - Sidebar: party HP/SP/level from `Vitals`.
  - Log: the tail of the message log, fed by `GameMessages`.
- [ ] **`OverworldTerminalView`:** draws `OverworldScene` and `CampaignContext` (grid, `OverworldGates`, warps, keys, locations) plus the player's tile. Port the glyph rules from Explorer `MapRenderer.Glyph`.
- [ ] **`TownTerminalView`:** draws the Core `TownPlan` (`CoreLayoutCache.TryGetTown` / `CampaignContext.Town`) using the `TownRenderer` + `TownServiceGlyphs` rules, plus `TownPlayer`, ally and NPC positions.
  - [ ] Check how `TownPlayer` stores its grid cell versus its world position.
- [ ] **Speed:** when terminal mode is on, force `DungeonAnimationMode.NoAnimations` (`Common/DungeonPreferences.cs`; honored in `TurnManager` ~L549–562) and skip `WaitForSecondsRealtime(2.0f)` in `Game.AdvanceFloorRoutine` (`Game.cs` ~L308).
- [ ] **Entry point:**
  - A "Terminal mode" toggle on the main menu (`MainMenu.cs` + `MainMenu.unity`) and in `DisplayOptions.cs`.
  - New Journey and Continue use the normal `CampaignSlots` → `Travel` flow.
- [ ] **Debug hotkey** (e.g. F11) to switch modes in play.

## Phase 2 — Terminal-native menus (~1–2 weeks)
Replace uGUI dialogs with terminal menus (the Explorer's `> option` list style) that call the **existing** services. This is safe because the scenes are live.

- [ ] **Town:** shop buy/sell, trainer, recruit, rest/save and equipment. These go through `TownServices`, `TrainerOffers` and `EquipmentTransferService`, the same calls `TownMenu.OpenBuilding` and `AllyRecruitDialog` make.
- [ ] **Dungeon:** skills, items, inventory and the pause menu, sent through `TurnManager.SubmitCommand` / `ally.SetAction` (the `AutoplayRunner` path). This is the biggest piece: `Dungeon/Game/Menu` is ~2.7k LOC.
- [ ] **Overworld:** party swap (`CampaignHUD`) and the warp list.
- [ ] **Popups:** `MessageDialog` / `CampaignChoice` as a terminal modal box.
- [ ] Hide each uGUI dialog in terminal mode once its terminal version exists. Until then, keep the uGUI version as the fallback.

## Out of scope
- **A standalone Core-side terminal game.** It needs the ~19k LOC of combat rules pulled out of UnityEngine first. That is a separate refactor. The `ExecuteImmediate`/`ExecuteRoutine` split is the natural seam if it ever happens.
- **Skipping TileWorldCreator mesh builds in terminal mode.** A possible later optimization, but floor generation currently waits on TWC callbacks.

## Verification
- [ ] **EditMode:** `TerminalFrame.ToRichText` merges color runs correctly, and each row's visible width equals the number of columns.
- [ ] **PlayMode** (`Assets/Tests/PlayMode/Support/GameTestHarness.cs`):
  - Load DungeonScene with terminal mode on.
  - Assert the text contains `@`, enemy glyphs and the stairs glyph, and that world cameras are off.
  - Submit a move and assert `@` moves.
- [ ] **Manual:**
  1. Turn terminal mode on → New Journey.
  2. Walk town-0 → overworld → story-0. Fight through a floor and descend.
  3. Return to town and save.
  4. Turn terminal mode off → Continue in 3D, and confirm the save works.
