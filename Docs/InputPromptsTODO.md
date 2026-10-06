# TODO: Device-aware control prompts

## Goal
On-screen control hints show only the current device's keys:
- Keyboard: "Enter: interact"
- Controller: "A / Cross: interact"
- Never both at once, as in today's "Enter / A".

When the player switches devices mid-session, every visible hint updates.

## Current state
- **Detection:** `MenuUIInputModule.UsingGamepad` (`Assets/Scripts/MainMenu/MenuUIInputModule.cs:20`, set in `Process()` ~L172-185).
  - Flips to gamepad on a deliberate pad press or stick/d-pad change.
  - Flips back on any keyboard key, mouse move, scroll or click.
- **Already device-aware:**
  - `Dungeon/Game/Menu/MenuControlHints.cs`
  - `Dungeon/Game/Menu/PartyMenuLauncher.cs:38-41`
  - Both poll `UsingGamepad` and cache `lastPad`.
- **Duplicate detectors:**
  - `Common/CursorManager.cs` has its own pad/mouse heuristic for cursor visibility.
  - `PlayerInputHandler.OnControlsChanged` (`Dungeon/Game/Menu/PlayerInputHandler.cs:88`) only logs.
- **Hardcoded mixed strings:**
  - `Overworld/CampaignHUD.cs:93`: the fallback "Move: WASD / arrows / left stick | Interact: Enter / A".
  - `Overworld/OverworldScene.cs:184`: "Enter / A: claim location rewards…"
  - `Overworld/OverworldScene.cs:285`: "| Enter / A: interact"
  - `Overworld/OverworldScene.cs:289`: "| Enter / A: Collect …"
  - `Overworld/OverworldScene.cs:543`: IMGUI label "WASD / arrows / left stick: move • Enter / A: interact"
  - `MainMenu/DungeonOptions.cs:52`: "Press F or the controller's right stick to toggle it."
  - `Common/AutoplayRunner.cs:745-746`: "(Enter / A)" and "(Esc / B)"

## Phase 1: Single source of truth
- [ ] Add `Assets/Scripts/Common/InputDevice.cs`, a static class:
  - `public static bool Gamepad { get; private set; }`
  - `public static event Action Changed`, raised only when the value flips.
  - `internal static void Set(bool gamepad)`
- [ ] Move the detection heuristic out of `MenuUIInputModule.Process()` into an `InputDevice.Poll()`. Call it once per frame from a component that lives as long as the game: `CursorManager` (Common scene) or a new `InputDeviceTracker` MonoBehaviour there.
  - Reason: the module only processes while an EventSystem is active. Check that it runs in the overworld/town/dungeon; if it does, simply calling `Set` from it is fine.
- [ ] Keep `MenuUIInputModule.UsingGamepad` as a pass-through (`=> InputDevice.Gamepad`) so existing callers keep compiling.
- [ ] Make `CursorManager` use `InputDevice` instead of its own heuristic:
  - Keep its rule that the mouse wins when both are active in the same frame.
  - Keep its stick deadzone (0.35 / 0.15).
  - Pick one set of thresholds and use it for both cursor hiding and hints, so the two can never disagree.
- [ ] Delete the logging `OnControlsChanged` handler from `PlayerInputHandler`, or have it call `InputDevice.Set(scheme == "Gamepad")`. Prefer deleting it: `PlayerInput` auto-switch reacts to stick noise.

## Phase 2: Prompt helper
- [ ] Add `Assets/Scripts/Common/InputPrompts.cs`:
  - `static string Pick(string keyboard, string pad) => InputDevice.Gamepad ? pad : keyboard;`
  - Named prompts so wording stays consistent:
    - `Confirm`: "Enter / Space" or "A / Cross"
    - `Back`: "Escape" or "B / Circle"
    - `Move`: "WASD / arrows" or "Left stick / D-pad"
    - `Interact`: "Enter" or "A / Cross"
    - `ToggleFullControl`: "F" or "Right stick (press)"
  - `static string Format(string template)` replaces tokens like `{Confirm}` and `{Back}`. Messages that are stored, rather than drawn fresh each frame, keep the tokens and are formatted at draw time.
- [ ] Rebuild `MenuControlHints` and `PartyMenuLauncher` text from `InputPrompts` so all wording lives in one place. Keep their `lastPad` caching, or switch them to `InputDevice.Changed`.

## Phase 3: Convert call sites
- [ ] `OverworldScene.cs:184, 285, 289`: store `{Interact}` in `Message` instead of "Enter / A".
- [ ] `CampaignHUD.cs:93`:
  - Wrap the text in `InputPrompts.Format(...)`.
  - Change the fallback to "Move: {Move} | Interact: {Interact}".
  - It assigns `message.text` every frame, so no change event is needed.
- [ ] `OverworldScene.cs:543` (IMGUI): use `InputPrompts.Format` inline. IMGUI redraws every frame.
- [ ] `DungeonOptions.cs:52`: build the explanation with `InputPrompts.Pick`. It's set once, so either:
  - refresh it in `Update` while the dialog is open, or
  - subscribe to `InputDevice.Changed` in `OnEnable` and unsubscribe in `OnDisable`.
- [ ] `AutoplayRunner.cs:745-746`: `InputPrompts.Pick("Enter", "A")` and `InputPrompts.Pick("Esc", "B")`. IMGUI, so it updates every frame.
- [ ] Grep again for leftover mixed hints:
  - `rg -n '"[^"]*(Enter / A|Esc / B|/ left stick|or the controller)' Assets/Scripts`
  - Also check authored TMP text in prefabs/scenes: `rg -n 'Enter / A|Esc / B' Assets --glob '*.prefab' --glob '*.unity'`

## Phase 4: Tests
- [ ] EditMode test for `InputPrompts.Format` with `InputDevice.Set(true/false)`: tokens resolve, unknown tokens pass through unchanged.
- [ ] EditMode test that `InputDevice.Changed` fires only when the value actually flips.
- [ ] PlayMode test (pattern of `Assets/Tests/PlayMode/HeldWalkingTests.cs`) using `InputTestFixture`:
  - Add a virtual `Gamepad`, press `buttonSouth`, and assert the CampaignHUD message text contains "A / Cross" and not "Enter".
  - Then press a keyboard key and assert it flips back.
- [ ] Check stick drift: a resting stick (small values that don't change) must not flip to gamepad.

## Phase 5: Docs
- [ ] Add a "Control prompts" section to `Docs/DungeonControls.md`:
  - New hints must use `InputPrompts`, not literal "Enter / A".
  - Say where the device state comes from.

## Later (optional)
- [ ] Generate prompt text from the bindings so rebinding stays correct:
  - `action.GetBindingDisplayString(group: "Gamepad" | "Keyboard&Mouse")` on `DungeonControls.inputactions`.
  - Add a small map from control paths to friendly names (`buttonSouth` → "A / Cross").
  - Only matters once rebinding exists.
- [ ] Glyph icons:
  - A TMP sprite asset with keyboard, Xbox and PlayStation icons; prompts emit `<sprite name=...>`.
  - Pick Xbox or PS glyphs from `Gamepad.current` (`DualShockGamepad` / `XInputController`).
  - Needs art.

## Acceptance
- Overworld, town, dungeon HUD, party/shop/trainer menus, options and autoplay dialogs each show a single device's prompts.
- Switching device mid-screen updates visible hints within a frame.
- Cursor visibility and prompts always agree on the current device.
- A resting or drifting stick never flips prompts to gamepad.
