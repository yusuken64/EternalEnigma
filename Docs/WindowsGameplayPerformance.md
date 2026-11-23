# Windows gameplay frame times

Measured on November 23, 2026 with a Core i7-10750H, GeForce RTX 2060, Windows x64, Unity 6000.2.7f2 and Direct3D 11 at 1920 × 1080, Ultra. The budget for 60 FPS is 16.67 ms per rendered gameplay frame. All samples begin after the player has control, the transition and floor title have finished, and terrain streaming has drained.

## Follow-up at zero volume

Both sides of the comparison ran with VSync on and used the existing Windows Mono toolchain. Every player report verified zero on both the master mixer and the audio listener. Each run started in a fresh process with its own in-memory settings and campaign. The walking route took 96 steps. The other routes used 24 accepted commands each. The party-menu route included four Stats opens, four hero switches and four closes interleaved with 12 steps.

| Playable route | Previous average FPS | New average FPS | New p95 | New p99 | Previous worst | New worst | New frames over 16.67 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Overworld walking | 135.5 | 137.0 | 11.75 ms | 14.25 ms | 25.6 ms | 25.8 ms | 15 / 4,651 |
| Overworld with Stats | 62.4 | 109.9 | 16.78 ms | 23.49 ms | 776.1 ms | 146.6 ms | 50 / 936 |
| Dungeon traversal | 129.9 | 132.8 | 12.63 ms | 15.02 ms | 117.2 ms | 56.6 ms | 4 / 1,127 |
| Dungeon boss fight | 134.0 | 127.4 | 14.22 ms | 18.04 ms | 145.6 ms | 77.4 ms | 42 / 2,059 |
| Seven-enemy ambush | 135.8 | 131.6 | 12.80 ms | 16.34 ms | 186.1 ms | 112.9 ms | 58 / 6,887 |

The matched runs produced the same number of moves and attacks, the same ending cells, HP, mana and hunger for every party member, and the same enemy outcomes. Both versions defeated the 390-HP boss and cleared all seven ambush enemies. The mean GPU time in the ambush was 2.4–2.5 ms. The slowest combat frames improved, although the averages in both combat runs were slightly lower. Idle frame rate across these five packaged-player runs ranged from 98.7 to 140.9 FPS.

In the new package the first Stats open took 146.6 ms. The next three took 28.5–34.6 ms, switching heroes took 35.0–39.4 ms, and closing took 7.4–10.3 ms. The corresponding previous ranges were 681.5–776.1 ms opening, 586.2–628.1 ms switching and 115.5–146.5 ms closing. The previous code also stalled with audio on, reaching 747.8 ms in Stats and 184.2 ms in the ambush. A separate zero-volume run of that code reached 780.2 and 192.4 ms respectively.

An additional paired run of each slow route used a fresh process for each version, launched back to back. The Stats route went from 44.1 to 114.8 FPS. Its first open went from 1,159.4 to 218.4 ms, and subsequent opens in the new player took 26.4–47.2 ms. The ambush went from 136.8 to 135.3 FPS, with the first turn dropping from 203.5 to 109.0 ms. All ending positions, vitals and enemy outcomes matched again. See `Logs/WindowsFrameProfile/muted-repeat-*.json`.

### Where the time went

- Opening Stats used to compute the same hero's stat breakdown for each of 14 rows. Learned-skill lookup repeatedly searched the entire class catalog. The menu now builds the hero's breakdown once per view, reuses the authored skill index, and skips a second layout pass when the visible rows already fit. Earned dungeon-clear abilities still resolve against the current campaign.
- A separate main-thread Mono trace counted 617 methods compiled during the first traversal turn and 1,001 during the first ambush turn. Compilation occupied 84 ms and 180 ms respectively in those traced runs. On Windows, the game now prepares the common turn, AI, and playback methods behind the first dungeon entrance cover. Subsequent dungeon entries reuse the compiled code.
- Companion AI now skips route construction when it can already wait next to the leader. It checks whether a melee attack can reach an enemy or a firing direction intersects an enemy's footprint before performing visibility and collision work. Projectile checks visit the existing character lists directly and still check intervening walls, props and actors.
- The existing formatter processed 2,904 labels during the 24-command traversal. It now reuses the result for recurring short strings after resolving the current input bindings. The measured time for those calls fell from 65.5 ms to 1.3 ms over the route.
- In the ambush, 80 companion skill decisions took 65.6 ms in total, down from 155.7 ms. Companion route work fell from 97.8 to 36.1 ms. The same 8,981 text-format calls fell from 209.9 to 15.6 ms. Some scopes overlap, and playback also spans animation waits.

The first playable dungeon command remains the slowest in all three dungeon routes. In the ambush it spent 90.0 ms submitting the turn, and the next two submissions still took 39.8 and 46.3 ms. Stats also exceeds the 16.67 ms budget when creating and laying out the menu. The raw reports are `Logs/WindowsFrameProfile/muted-reference-*.json` for the previous gameplay code and `muted-final-*.json` for the packaged player.

A separate muted six-command trace of the optimized code still counted 605 methods and 77.5 ms of main-thread compilation during the first ambush turn, including the sorted set and dictionaries used by companion pathfinding. The first Stats open recorded 205 methods and 68.7 ms, including regex and TextMesh Pro rich-text handling. The first hero switch recorded only 13 methods and 3.5 ms of compilation while the input itself took 44.4 ms. Reusing the menu's existing rows when browsing heroes is a remaining opportunity. These diagnostic captures are in `Logs/WindowsFrameProfile/muted-final-trace-*.jit.tsv`.

Seventeen distinct PlayMode checks and 23 EditMode checks passed. They cover stats and passive descriptions, live input prompts, HUD updates, companion pursuit, blocked and off-axis shots, and a shot hitting the edge of a three-cell-wide enemy. One combined equipment and HUD test stopped at an assertion that the History button text should fit. The previous gameplay code reproduced the same assertion at 1280 × 720, and the updated code reproduced it at 1280 × 720 and 1920 × 1080. That test stopped before its final equipment reload check.

The follow-up Windows build GUID is `43ef0a13a062454ba2abe5c2d56f03f3`. All 183 archived files were verified against the unpacked player in `PCBuild/`. The new `PCBuild.zip` SHA-256 is `F58B52DD3A7B1281D946AEAB53799D5DCB785C158F5A594E6F96180999688445`. The preceding package is in `Builds/PreviousPCBuild-20261123-120141/`.

## First investigation

| Build and route | Average FPS | 95th-percentile frame | 99th-percentile frame | Worst frame | Frames over 16.67 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Previous package · overworld | 18.6 | 149.06 ms | 164.91 ms | 188.4 ms | 116 / 160 |
| Matched baseline · overworld | 29.2 | 121.86 ms | 130.93 ms | 146.5 ms | 73 / 251 |
| Updated · overworld | 246.7 | 6.30 ms | 8.41 ms | 32.8 ms | 1 / 2,088 |
| Previous package · dungeon traversal | 201.1 | 7.70 ms | 10.09 ms | 332.4 ms | 1 / 1,699 |
| Matched baseline · dungeon traversal | 213.4 | 6.88 ms | 8.86 ms | 137.4 ms | 1 / 1,801 |
| Updated · dungeon traversal | 214.0 | 6.94 ms | 9.40 ms | 105.6 ms | 2 / 1,811 |
| Before effect preload · boss fight | 196.1 | 8.07 ms | 10.96 ms | 162.9 ms | 11 / 3,170 |
| Updated · boss fight | 226.6 | 6.80 ms | 8.72 ms | 163.2 ms | 4 / 3,645 |

The previous PC package was a development build. The source-matched baseline and updated package were both non-development Windows players. Comparing those two isolates the changes from the difference in build type. The uncapped runs measure frame-time headroom. Running the packaged executable again on the same display with its normal graphics selection and VSync enabled produced the following active results.

| Packaged player with VSync | Average FPS | 95th-percentile frame | 99th-percentile frame | Worst frame | Frames over 16.67 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Overworld walking | 143.3 | 7.41 ms | 8.18 ms | 20.8 ms | 2 / 1,218 |
| Dungeon traversal | 140.5 | 7.78 ms | 10.67 ms | 128.1 ms | 1 / 1,194 |
| Dungeon boss fight | 140.3 | 7.95 ms | 11.54 ms | 132.9 ms | 5 / 2,270 |

The packaged player selected Direct3D 11 in all three runs. Idle presentation measured approximately 144 FPS.

## What cost time

- **Overworld party cards.** The previous package rebuilt all four cards when a step started and when it ended. Across 24 steps the party HUD callback ran 48 times and used 6.00 seconds in a separate instrumented run. It repeatedly resolved learned skills, recalculated party stats and formatted the same labels. The previous package averaged 53.0 ms on the main thread and 8.1 ms on the GPU while walking. Movement now announces a travel change, collecting an item announces a resource change, and party and visibility events still update the cards.
- **Per-frame scene searches.** Shared input and HUD code repeatedly looked for a dungeon controller while the overworld was active. Those callers now read the instance registered by the dungeon. Overworld idle performance rose from 77 FPS in the matched baseline to 251 FPS in the final run.
- **Dungeon combat effects.** The Windows player now resolves the shared effect catalog behind the entrance cover. With a fresh process in each test, the same early combat turn dropped from 163 ms to 79 and 82 ms. The peak measured time for resolving an individual action went from 94 ms to 28 and 17 ms. All three runs began with one enemy at 390 HP and ended with it defeated by the party.
- **First dungeon command.** Selecting and submitting the first command took 98 ms of the 106 ms frame in this traversal run. In two boss runs, that first frame took 163 and 180 ms. The playback timer spans animation waits, so its elapsed time includes waiting across frames.

## Reproduce

Build with **Tools → Eternal Enigma → Performance → Build Windows Player**. The executable is created in `Builds/WindowsPerformance`. From the repository root, run a fresh player process for each route. The route names are `overworld`, `overworld-party`, `dungeon`, `dungeon-combat` (boss), and `dungeon-ambush` (seven enemies).

```powershell
. 'C:\Users\yusuk\.local\bin\Enable-CodexHeadless.ps1'
$player = '.\Builds\WindowsPerformance\Eternal Enigma.exe'
$report = Join-Path (Get-Location) 'Logs\WindowsFrameProfile\local-overworld.json'
$arguments = @('--pc-frame-profile', 'overworld', ('"' + $report + '"'),
    '--frame-paced', '--frame-muted', '-screen-fullscreen', '0', '-popupwindow',
    '-screen-width', '1920', '-screen-height', '1080')
$run = Start-Process -FilePath $player -ArgumentList $arguments -Wait -PassThru
if ($run.ExitCode -ne 0) { throw "Windows player exited with $($run.ExitCode)" }
```

For a manual run, let the player render its own window. The automated captures used a private Windows desktop with a normal rendered backbuffer.

The profile uses seed 42, four equipped heroes, the production camera, HUD and normal turn animation, and an isolated campaign and preferences. Each route includes five seconds of idle sampling followed by 24 ordinary gameplay commands by default. Add `--frame-commands 96` to reproduce the sustained walk. The overworld route starts at `town-4`, traversal on floor 5 of `story-1`, the boss in `region-5-landmark`, and the ambush in `region-0-landmark`. The party approaches enemies using normal movement and combat rules. The report verifies gameplay camera renders, records each accepted input, and captures final character positions and vitals after timing ends.

`--frame-paced` keeps the player's VSync and frame-rate settings. `--frame-muted` sets the isolated master mixer level and the audio listener to zero and verifies both in the running player. Sound cues and playback logic still execute. `--frame-breakdown` adds idle samples with silhouettes disabled and the world camera mask cleared. The normal camera settings are restored for the active route.

Six focused Unity PlayMode tests passed. They cover walking onto a pickup without rebuilding party cards, updating health and inventory, unlocking and using a warp, dungeon HUD updates, fast walking, and input blocking. The new pickup regression failed against the original gameplay code at the expected card-rebuild assertion. A separate, existing warp-picker layout test reported the same 5.4-pixel overlap on the original and optimized code.

The per-run JSON and player logs are in `Logs/WindowsFrameProfile/`. The `packaged-*` runs use a benchmark-instrumented copy of the previous package. The `before-*` runs use the source-matched baseline. The `final-*` and `effectswarm-*` runs use the updated release. The `paced-*` runs use the verified `PCBuild` folder. These short fixed routes cover one PC and one seed.

The first investigation used Windows build GUID `f16869fccb9248edb510aea457479bfb` and archive SHA-256 `596560D3AA66507F9D7AAEBA00002538AA872986AD2E75B454CF60934876133E`.
