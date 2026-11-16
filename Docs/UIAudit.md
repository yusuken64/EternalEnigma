# UI audit

## Dialog fonts and layouts - November 15, 2026

Graphical dialog titles, body copy and controls use LiberationSans SDF, matching
the menus. Fixed the remaining decorative-font references in the town scene,
shared button/skill templates, and dungeon history, inventory and party assets.
The authoring tools now preserve that choice when regenerating those views.
Terminal binding capture also refreshes its style while normal menu input is
suspended. The hero chooser has enough description space for all 24 backstories
and playstyles in both presentations.

The saved-asset audit loaded 595 scene/prefab assets and checked 773 menu labels,
including inactive templates and all 22 dialog component types found in those
assets. All passed; 47 labels inherit the configured TMP default. Branding and
scene-loading artwork retain their decorative font.

The font audit consolidates 12 unique passing Play Mode checks from the full
text-containment run and its focused follow-up. Its 989 screenshot/JSON pairs
contain 25,848 measured labels and 12,513 active menu-font checks,
with no font mismatches or rendered-text containment failures.
Coverage includes graphical and terminal menus, all 24 heroes, settings/binding
capture, slots, shops and transaction confirmations, NPC/recruitment dialogs,
training, home/inn/book entry, party docks, overworld, dungeon and results.
Captures use 960x600, 1280x720, 1920x1080 and 2560x1080; dungeon docks also use
1280x800. Visual review covered each active dialog family at the smallest size,
with affected bread and hero layouts also checked ultrawide. Dormant legacy
templates were checked as saved assets rather than presented as reachable flows.

A further seven Play Mode checks pass for the subsequent dialog, header and prop
changes (19 unique checks across both audits). The Learn dialog now has a wider
paper sheet with a separate title, description and action row. Its old horizontal
layout group is disabled so it cannot collapse the anchored Learn/Cancel buttons.
All 195 authored skills were checked at all four sizes in graphical and terminal
mode: 1,560 confirmations, including single-line action labels and visible button
faces. Representative longest-text captures were reviewed at 960x600 and
2560x1080.

The shared top menu buttons have wider faces, taller panels and lettering inset
from the carved edges. The resource strip clears the buttons below them.
The header follow-up covers town, overworld and dungeon with keyboard and
controller prompts at all four sizes (24 captures). Decorative chests were
removed from dungeon themes and replaced by luggage in town furniture rendering.
The Black Knight-shaped statue remains a blocking prop but cannot receive damage;
the regression exercises direct, melee, ranged and skill targeting.

Reproduce with **Tools > Eternal Enigma > UI > Validate Saved Menu Fonts**,
then **Tests > Run Text Containment Audit**. The follow-up used
**Tests > Run Terminal Audit Captures**. Use **Tests > Run Dialog and Prop Followups**
for the skill/header/prop checks. Runtime assertions check the active
terminal focus hierarchy; covered parent dialogs restore their authored styling.
The autoplay playback overlay retains its graphical font in terminal mode.

Evidence: `Temp/DialogFontVerification/authored-fonts.txt`, `runtime.json`,
`full-run.json/xml`, `followup.json/xml`, `dialog-prop-followup.json/xml`, and
`dialog-prop-summary.json` (plus the final alignment recheck in `header-followup.json/xml`). Skill captures are in `Temp/LearnSkillDialogAudit`;
header captures have the `header-` prefix. Other captures are in
`Temp/UIScreenAudit`, `Temp/TerminalAudit`, and `Temp/DungeonDock`.
Tests isolate saves and preferences. These are Editor Play Mode checks.

## October 14, 2026 audit

Scope: main menu and campaign selection, hero selection, all Settings tabs, town
services and recruitment, shared party menus, overworld travel and gates, dungeon
actions and targeting, event history, level-up, victory/defeat and Hall of Fame
screens, scene fades, and dungeon floor transitions.

### Corrections

| Area | Findings and corrections |
| --- | --- |
| Main menu | Labels fit their button rectangles; Options naming is consistent. The selection arrow uses the correct focus root and clears button edges. Back restores the opener. Escape/Start opens Options; developer controls are limited to development builds and can be cancelled. |
| Settings | Main-menu settings show Close without a redundant Return to Main Menu action. The Controls guide scrolls using keyboard, controller, and mouse wheel at a readable size. Dropdown arrows fit their controls. Options renders above party panels and their input shields; quit confirmation remains above Options. Resume closes on its first pointer click. |
| Input and feedback | Accepted button activation plays confirmation feedback; selection-only clicks and disabled actions do not. Prompts and native cursor follow deliberate input, including gradual stick motion and controller disconnection. Wait uses left-stick press; Full Control uses right-stick press, avoiding an accidental spent turn. |
| Shared party UI | Active tabs retain a gold/underline indicator. Each panel has one reachable close control, a visible hero name, and wrapped rows sized for their text. Stats/equipment summaries show explanations. Actions report their outcomes. Options is reachable by mouse above the party dock in town, overworld, and dungeon; the dungeon reuses its existing button. Resume restores the same panel. |
| Town | Full service dialogs suppress overlapping gameplay HUDs. Resource counts sit above party docks. Recruitment and lesson text fit their panels; unused decorative space is removed. Impossible recruit/remove operations explain their disabled state. Number entry connects to Cancel. |
| Shops | Buy/Sell indicators persist. Footers fit. Confirmation validates affordability, stock, and exact sale entries, explains unavailable actions, focuses Cancel, and rejects stale/cancelled transactions. |
| Overworld | The missing contextual mouse action is restored. The same interaction flow offers available travel routes and specific nearby gates to keyboard/controller users. Dungeon entry explains unmet requirements and prevents unavailable actions. |
| Dungeon | Target selection supports mouse preview, confirm, and right-click cancellation; opening clicks and HUD clicks cannot cast. A transparent targeting shield drains blocked pointer clicks so canceling cannot replay them into Options or the restored menu. Visible character bodies take priority over ground cells behind them. Target prompts no longer overlap party shortcuts. Full Control shortcuts are blocked during scene and floor covers. |
| Results | Back keeps defeat/victory visible and focuses the explicit recovery/save action instead of resuming defeated gameplay or leaving completion unsaved. Empty deserialized completion records no longer replace standalone victory with a Hall of Fame summary full of zeroes. |
| Scene transitions | Fades measure unscaled time from their own start, so an earlier slow loading frame cannot skip them. They preserve alpha when interrupted or reversed, reject duplicate pending loads, and cancel superseded fade drivers. Loading always presents an opaque frame, including No Animations. The destination renders under an opaque cover before its reveal clock starts. Destination readiness controls reveal; keyboard, controller, and pointer input remain blocked until the cover is released. Covered pointer events are drained so they cannot replay after reveal. Ready HUD elements reveal with the world while their actions remain disabled. Dungeon entry hands the centered title to the floor overlay to avoid overlapping labels. |
| Floor transitions | Advance and Reset Floor wait for a rendered opaque cover before removing actors or replacing terrain, stay covered through generation and party restoration, and reject repeated activation. Floor text and decoration fade with the cover. Explicit startup handling distinguishes the authored preview dungeon from an initialized floor. |

The final styling pass follows the requested single-parchment treatment: Options
uses one outer sheet with clear tab content, trainer details use a simple inset,
lesson confirmation sits over a dim backdrop, and Hall of Fame hero cards use
contrasting slate surfaces. Existing ribbon artwork backs Options, hero/slot
selection, Hall of Fame, and trainer headings. Learn Ability rows separate the
ability name, rank and availability from the price. Level-up recommendations use
supported text rather than a missing star glyph. Recruitment and entrance dialogs
use a compact pointer outside their selected button; unavailable dungeon entries
are disabled and focus returns to Cancel.

### Character animation audit

The source and asset review found no runtime character jump, shake, punch,
rotation, or scale tweens replacing skeletal animation. Town, dungeon, and
overworld travel pair positional interpolation with real walk clips. Bed scenes
use the imported sleeping clip. Split-spawn movement and trap displacement are
intentional positioning; dropped-loot jumps, projectiles, and selected-tab scaling
serve their respective effects.

All 11 main-menu motion components have real animation clips and Animators.
`MenuSceneMotion` adds small hover offsets to flying creatures and 1.8% squash to
the red slime over their clip playback. Town NPC Idle/Greeting assets are authored
skeletal clips, including head, limb, and blink motion. No compatible unused
replacement clips were confirmed for these NPCs. These presentation choices were
retained.

Two clip-routing issues were identified:

- Fixed: armed potion use searched empty armed-stance pools even though
  `DrinkPotion_NoWeapon` was available. `HeroAnimator` now borrows that clip and
  restores the equipped idle without changing equipment. The focused regression
  checks all seven armed stances and natural completion with a bow equipped.
- Fixed: Lunge and Shadow Step requested a real dash clip, but their movement
  helper immediately replaced it with walking. These two skills now request dash
  playback during their travel; other movement retains walking. The focused
  regression checks both authored skill effects during movement, landing and idle,
  skipped playback, and another skill's ordinary walking displacement.

All 13 animation regression checks passed, including armed potion playback,
movement skills, and the actual home-bed sleep flow.

### Validation

Unity Play Mode interaction checks and visual captures are run through:

- `Tools > Eternal Enigma > Tests > Run UI Audit Controls`
- `Tools > Eternal Enigma > Tests > Run UI Audit Captures`
- `Tools > Eternal Enigma > Tests > Run UI Audit Fades`
- `Tools > Eternal Enigma > Tests > Run UI Audit Animations`
- `Tools > Eternal Enigma > Tests > Run UI Audit Devices`

The capture fixture records PNGs and text diagnostics at 960×600, 1280×720,
1920×1080, and 2560×1080. Dungeon dock coverage also includes its existing
additional aspect-ratio case. Captures are inspected visually; a successful
capture test alone is not a claim that every label fits. Interaction tests add
focused assertions for fit, cursor clearance, scrolling, selection restoration,
input consumption, and transaction guardrails.

The completed controls, fades, animations, and captures runs cover 93 unique
passing tests, consolidating final runs and focused rechecks. All nine focused
device/controller checks passed, including takeover prevention. The capture run
produced 209 PNGs: 164 screen captures and 45 dungeon dock captures. Main menu,
Options, slots, hero selection, services, party panels, targeting, and results
were reviewed at the smallest and widest sizes, with the prior full-resolution
review retained. The final recruitment caption fits inside its button at both
sizes, including the disabled state.

Thirty automated text diagnostics describe the masked event-feed viewport or
ultrawide HUD line-height metrics; the visual review found no missing fonts or
unintended visible clipping in those cases. Intentionally scrollable descriptions
and history remain clipped to their viewports.

All 10 transition checks passed. Frame traces for eight animated travel routes
showed 1.004–1.019 second reveals, with no exposed unready destination, input
leak, missing gameplay HUD, or overlapping loading titles. Floor advance/reset
presented an opaque cover before any mutation and revealed over approximately
three seconds. No Animations also presented the opaque cover before mutation.

Artifacts are in `Temp/UIScreenAudit`, `Temp/DungeonDock`, and
`Temp/HarnessResults`. The first capture pass is preserved in each capture
directory's `Before` folder. Tests use an isolated save store. The consolidated
latest result for each test is recorded in
`Temp/UIScreenAudit/Reports/FinalVerification.json`; preserved run directories
retain the underlying NUnit XML and runner JSON. The final capture inventory also
records the text diagnostics for inspection.

Input coverage uses Unity Input System keyboard, mouse, and gamepad events.
Physical controllers, touch devices, and platform-specific builds were not
exercised. The existing theme and native OS mouse cursor are retained. The
[terminal follow-up audit](TerminalUIAudit.md) also covers ASCII mouse targeting
alongside keyboard/controller input.

The input asset also contained incomplete Touch, Joystick, and XR template
schemes. They lacked essential gameplay actions, and PlayerInput automatic
switching could select them and unpair working keyboard/gamepad controls.
Selectable schemes are limited to Keyboard & Mouse and Gamepad; their
automatic switching remains enabled. Touch, generic joystick, and XR are not
complete supported gameplay interfaces. Virtual unsupported-device activity is
checked specifically for takeover prevention, not gameplay support. Those checks
passed, including subsequent keyboard and gamepad actions and automatic switching.
