# Terminal UI audit — October 15, 2026

This follows the [regular UI audit](UIAudit.md), covering the terminal presentation
of the main menu, campaign slots and hero selection, Settings, town services,
party panels, overworld, dungeon commands and targeting, event history, level-up,
results, scene fades and floor transitions.

## Corrections

| Area | Findings and corrections |
| --- | --- |
| Controls | Dark image tints multiplied the terminal button colors, obscuring selection and press feedback. Buttons now have readable normal, selected, pressed and disabled states. Slider handles, checkmarks, dropdown arrows, close symbols and active tabs remain visible. Warning text retains a distinct color. |
| Modal panels | Transparent backgrounds let the ASCII title and map show through dialog text. Terminal panels now have opaque dark backings; party docks cover only their own panel. Styling restores captured sprites/fonts and tracks live warning/tab color changes when terminal mode ends. |
| Main menu | Button fonts are bounded and the selection marker remains visible. The overlapping developer toggle is suspended during terminal presentation. Input hints hide while a modal owns focus. |
| HUD and map | Persistent party/resource/action controls use the terminal style. The ASCII frame reserves space for these controls, the target prompt and an open party dock. Font size follows display height, with grid dimensions derived from the available panel. |
| Map content | Town and region titles, location/facing context, service glyphs and legends describe the current scene. Recruited town followers are included. Empty sidebars and duplicate dungeon party statistics are removed. Titles, context and messages wrap within the frame, with a continuation marker when space is exhausted. |
| Targeting | The runtime now finds the initially inactive target dialog, so live target markers appear. Mouse hover/click maps ASCII cells to production target selection, alongside keyboard and controller input. Pointer aiming keeps the viewport stable. Opening clicks, HUD clicks and stale/invalid targets retain the existing guards. |
| Input | The menu bridge drains pointer events even during held keyboard/controller navigation, without double submission. F11 and controller View/Share switch presentation. History remains accessible by mouse and H, or through Options with a controller. |
| Travel and fades | Presentation changes wait until scene/floor covers and active gameplay actions finish. Destination HUD visibility binds under the cover. The terminal canvas remains beneath transition covers and titles. |

The terminal style uses dark surfaces, monospace text and persistent selection
indicators. Existing portraits and informative bar fills remain legible. Learn
Ability rows retain their separate name, rank/availability and cost layout from
the regular UI fixes, restyled for terminal contrast. Native pointer behavior and
the operating system cursor are retained.

The ASCII presentation does not add character tweens. Underlying gameplay actors,
walk/action clips and animation timing remain authoritative; the animation
findings in the regular UI audit still apply.

## Follow-up findings

The first fresh interaction run passed 12 checks and failed five. Source review
and screenshot inspection identified additional gaps in the previous coverage:

- The shared dungeon close button uses its cross sprite as its tint target.
  Applying a dark panel tint made the cross nearly invisible. It now receives
  readable symbol colors, with a regression using the actual prefab.
- Opening Options disables the main-menu owner but leaves its sibling canvas
  active. The terminal footer now hides on owner disable and returns on resume.
- Dungeon HUD styling traversed the whole Game hierarchy, overlapping dialog
  styling and caching terminal fonts as the originals. Styling now targets the
  HUD canvas and independently styles the production targeting prompt.
- Party tabs and Options belong to the party focus scope but live outside the
  dialog hierarchy. The terminal menu session now includes these allowed
  controls, preserving row-to-tab navigation, submit, and focus restoration.
- The scene-unload gap could clear the active terminal presentation, causing the
  destination to defer restoring it until after its cover. The applied mode now
  survives that covered gap; Continue checks inspect the ready destination while
  it is still covered.
- Ordinary enemies whose initial is `i`, such as Ice Golem, inherited item color
  and draw priority. Actor kind now follows actual disguise state independently
  of its glyph; real mimics still appear as items until revealed.
- Mouse aiming tested the glyph's painted bounds instead of the character cell.
  Dot and blank terrain could miss even at the cell center. Hit testing now uses
  logical character advances and line bounds, with roundtrip checks for sparse
  glyphs and a real pointer-confirm flow. The proportional fallback font also
  uses its full fixed-width cells, including both halves of narrow glyphs.
- Closing Options could test the opener before party HUD controls had refreshed
  their interactability, sending focus to a row instead. The exposed party HUD
  now refreshes before focus restoration; both presentation modes are checked.
- Styling converted the live portrait turn outline into a solid rectangle.
  Border-only sliced images now retain their shape and live tint, including when
  terminal mode ends.
- A toggle requested during travel could defer destination binding as well as
  the mode change, leaving a returning main menu without its terminal view.
  Destination controls now bind to the applied presentation while the requested
  switch waits for the cover to finish.
- Recruitment and companion-talk sheets preserved their parchment artwork while
  their text changed to pale terminal ink. Aspect-preserving images that back
  text now receive dark surfaces; portraits and other informative artwork stay
  intact. Closing the dialog restores the original sheets.
- The dungeon entrance title and large choice card overlapped, and Home's
  preparation instructions ran into Save. Entrance controls now occupy separate
  regions with bounded fonts. Home uses the intended larger authored panel and
  a separate title region. Capture checks verify these bounds at every size.

## Verification

Use the following Unity menu entries:

- **Tools > Eternal Enigma > Tests > Run Terminal Audit Edit Mode**
- **Tools > Eternal Enigma > Tests > Run Terminal Audit Interactions**
- **Tools > Eternal Enigma > Tests > Run Terminal Audit Captures**
- **Tools > Eternal Enigma > Tests > Run Terminal Audit Followup**

Capture sizes are 960×600, 1280×720, 1920×1080 and 2560×1080. Capture tests use
the real dialogs and controls; their text diagnostics supplement visual review.
Input checks inject Unity Input System keyboard, mouse and gamepad events and use
isolated saves/preferences. Core tests cover bounded layout, fog/knowledge,
wrapping and bidirectional map coordinates.

Final results and capture review are pending verification. Artifacts are under
`Temp/TerminalAudit`; the initial capture pass is preserved in `Before` and NUnit
reports are preserved under `Reports`.

Physical controllers, touch/XR/generic joystick interfaces and platform builds
are outside this pass. The shared-save regression checks real purchase/save and
Continue in both presentation directions using a progressed fixture; it does not
represent a complete campaign playthrough. The remaining acceptance scope is
tracked in [the terminal checklist](../TODOs/11-terminal-mode.md).
