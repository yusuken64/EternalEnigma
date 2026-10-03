# Campaign presentation

New Journey uses `ProtagonistHeroPicker`: a paged roster of authored heroes with their
animated model and fixed class combination. Begin Journey creates a save with that hero as
the sole starting protagonist; companions are recruited later. Class choice does not alter
campaign topology, and the normal menu no longer offers an arbitrary class picker.

Hero class definitions supply starting bonuses, growth, weapon access and ranked skill trees.
Secondary classes add eligible non-mastery skills through tier 2 and rank 3. New heroes
receive starting mastery skills. Training uses skill points, not gold, for class heroes.

Buttons and UI references are stored directly in scenes/prefabs. **UI > Bake Button Styles**
is a reusable maintenance tool; **Validate Saved Button Styles** checks authored assets.
Generated menus instantiate `Resources/UI/GameButton.prefab`. Autoplay/sandbox IMGUI uses
`Resources/UI/GameSkin.guiskin`. Both reference the Bamao theme.

`CampaignHUD` supplies normal travel messages, interactions, warp buttons and town companion
management. Diagnostic seed/gate/biome controls belong to the sandbox. The town exit uses
normal movement interaction. [Animated main menu](Art/MainMenu.md) describes the saved stage.

Checks: `node Tools/unity-mcp.mjs harness Presentation` and `harness ButtonStyles`.
See [campaign flow](CampaignFlow.md) for saves and [classes](Classes.md) for learning rules.

## Unified gameplay presentation

`Resources/UI/PartyMenu.prefab` supplies Inventory/Skills, hero browsing, shared rows,
scrolling details, action/target pickers, and device-aware hints. Scene adapters implement
`IPartyMenuContext`; dungeon actions retain the existing command/targeting pipeline.
The panel uses safe-area anchors (0.08, 0.12)–(0.82, 0.86). Shops and trainers remain
separate dialogs and share the button skin, selection behavior, and detail-scroll control.

`Resources/UI/ResourceHUD.prefab` owns the upper-right money/bag widget: 1920×1080
reference, 280×96, offset (-24, -100), 12-pixel padding and 8-pixel spacing. Town and
overworld show item count; dungeon shows actual capacity, amber at one remaining slot,
and rust-red “Full” at capacity. Equipped items are excluded. It stays beside dialogs
and hides during loading/settings. Rebuild both prefabs and the existing-model icon
assets with **Tools > Eternal Enigma > UI > Build Unified Presentation Prefabs**.

`GamePresentationProfile` supplies shared icons, silhouette color/opacity, and the
20-minute outdoor sun period. `SilhouetteParticipant` preserves explicit caster/receiver
roles through environment batching; `SilhouetteRenderer` projects actual meshes into a
directional depth texture, draws explored ground receivers with scene-depth testing,
and composites one charcoal mask. Native shadows are disabled for participants. Animated
models/equipment retain renderer visibility; floors, water, UI, markers, and small clutter
do not cast. Receivers include bridge decks at their own height. Runtime render resources
are released when the scene/camera is destroyed.

`ScenePresentation` shares outdoor sun direction with silhouettes, varies elevation
between 45° and 65°, and freezes phase during settings, transitions, pause, and focus loss.
Phase survives scene transitions but resets each application session. Floor variants use
three deterministic material treatments without consuming gameplay randomness. Existing
biome placement bounds remain intact. Accent lights are pooled, limited to four nearest
visible fixtures, and never cast native shadows. Selection markers use transparent-center
jade rings while existing gold turn outlines and state dimming remain in control.

Main-menu title size is reduced 15%, primary buttons occupy a separate right column,
and test/autoplay buttons require the Developer toggle in editor/development builds.
Town/dungeon follow offset is (0, -12, -14), with orthographic size compensated for ground
coverage. Collapsed event logs show three messages and retain history access.

## Verification — 2026-10-03

The [verification artifacts](Art/Verification/UnifiedPresentation) include fixed-seed scene
captures, four menu resolutions (960×600, 1280×720, 1920×1080, 2560×1080), and test reports.
The isolated menu captures include four heroes and scrolling long descriptions. The town
silhouette pair uses seed 12345 and the same camera, with the silhouette component disabled
and enabled. These are feature comparisons, not a complete pre-change visual baseline.

| Check | Result |
| --- | --- |
| Unified production-scene PlayMode suite | 9 passed: menu state, recovery/cost validation, inventory filters, equipment/save/roster identity, layouts, developer controls, silhouette pixels and allocation checks |
| Focused equipment/painted-surface EditMode suite | 5 passed, including continuous UVs across variant materials |
| Controller/focus regressions | 20 passed; cursor-visibility assertion remains failing |
| Windows development build | Succeeded, 0 errors, 11 warnings |
| WebGL development build | Succeeded, 0 errors, 11 warnings |
| Headless Edge / software WebGL 2 smoke | New journey reached town; Q opened Inventory and R switched to Skills; captures reviewed |

The browser console reported unsupported `VR/SpatialMapping/Wireframe`, an existing unused
VR shader, plus an audio-load diagnostic. No new silhouette shader or framebuffer errors
were reported. See `WebGLConsole.txt` for the complete output. This software-rendered smoke
test is not a hardware performance benchmark. The Editor was restored to its Windows target.

The broad EditMode run passed 201 of 211 tests. Its painted-surface expectation was updated
for the three compatible materials and passed the focused rerun. Nine other failures remain
in ally emergency intent, authored boss flags, imported Core DLL hash, legacy material
instantiation, stealth, and targeting/vitals fixtures; see the full XML report. A clean
pre-change run was not available, so this does not establish that every remaining failure
predates this change. The controller cursor test also needs a focused Editor/hardware check.

The renderer pixel test checks actual projected geometry, union opacity for overlapping
casters, hidden dynamic casters, receiver exclusion, and scene-depth occlusion. Warm
silhouette command construction allocated zero managed bytes in the measured loop. This
does not measure every presentation component or prove the full animated/fog/bridge matrix.

The latest short Editor sample measured median frames of 7.371 ms without silhouettes and
8.164 ms with them (+10.8%). This is a noisy Editor proxy, not a matched desktop/WebGL player
benchmark. The ≤10% player-performance target and full platform visual matrix remain
unverified; the recorded proxy does not meet that target. No complete before/after player
baseline was captured.

Run **Tools > Eternal Enigma > Tests > Run Unified Presentation**, **Run Presentation
EditMode**, and **Run Controller Flows** to reproduce the targeted checks. Build commands
are **Build Windows Player** and **Build Presentation WebGL**. All gameplay tests use an
isolated in-memory save store.
