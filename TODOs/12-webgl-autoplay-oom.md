# WebGL out-of-memory crash under fast debug autoplay

## CURRENT STATE (read this first) — updated 2026-11-17 night

KEY FINDING: every 32x web run so far has died at the SAME point: entering the
finale ("reverse world"). Signature in the console: the dungeon scene loads
("Unloading 1103 unused Assets ... Loaded Objects now: 60990"), then the heap
grows ~400 MB in ~2-3 s with no [Memory] lines, then OOM. The "69 floors /
8 towns" run was the whole campaign up to the finale. The finale builds a
second overworld-sized world (FinaleTerrain.GenerateFinaleTerrain) while the
cached overworld and town are still held.

Commit 60b68234 (web only): clear OverworldTerrain and TownTerrain caches and
GC before the finale build; release finale mesh CPU copies once streamed;
free each finale floor's meshes on Ascend. Also new [Memory] lines: "Dungeon
run start: <id> (finale)", finale build stages, and a per-frame "Jump since
last frame" watchdog (>64 MB).

Other results this pass:
- Town plan fix (381a7116): web town generation +5 MB instead of ~+300 MB.
- Core benchmark project (Core/.../EternalEnigma.Core.Benchmarks, not yet
  committed): `--leak` shows Core retains nothing across repeated generation.
- Batch autoplay options fix (15fb7b4d): --debug-autoplay-options was always
  rejected (JsonUtility instantiates the omitted Balance block).
- The development Windows CI job hung >57 min once; WebGL artifact is enough.
- Native memory climbs per floor inside one dungeon run (329 -> 721 MB) and
  only drops on scene change. Not the blocker yet; not explained.
- [Memory] lines and the watchdog are now OFF in release web builds. Open the
  page with `?memlog` (e.g. http://127.0.0.1:8765/?memlog) to turn them on.
  Development players always log.
- Old harness node/Chrome processes from earlier sessions can still hold ports
  8765/8766/9333: kill them first, or you test an old build.

## Previous state — 2026-11-17 evening

Goal of this pass: the 32x debug autoplay (seed 42, Fast mode, all debug
toggles On) must reach the end of the game on the web build without OOM.
Speed is not a factor; use 32x. Longer loads are fine (an overlay covers
generation), so builds may be spread across frames freely.

Work is on branch `webgl-memory-release` (pushed; no PR; not merged).
Latest commit 4264bb11. All fixes there are meant to be behaviour-identical.

Progress at 32x (web, OOM at the 2048 MB wasm cap each time):
original ~2 floors / 2 towns -> 50 floors / 5 towns -> **69 floors / 8 towns**
(build 44156181931). Managed (Boehm) heap peaked at 933 MB.

What remains: the managed memory left at the START of each floor (just
after a frame boundary) rises with scene changes: ~25 MB early, ~150 MB
after the overworld (its cache, expected), then 292 -> 357 -> 476 MB as more
towns/dungeons are visited. Not obviously per turn. Remaining bursts on top
of it: core town plan generation (~+300 MB in one call under IL2CPP vs
~54 MB in .NET) and `EnvironmentBatch.Finish` on town-district floors
(75-135 MB).

Just pushed (CI was building, not yet tested):
- CoreLayoutCache and DungeonPresentation.protectedCells no longer use
  ConditionalWeakTable (suspected to pin every floor if the web GC does not
  honour weak keys). Likely fix for the rising baseline.
- [Memory] line every 1000 autoplay actions (per-turn vs per-scene growth).
- EnvironmentBatch scratch lists grow in powers of two.
- New CI job `StandaloneWindows64-Development` (Assets/Scripts/Editor/
  DevelopmentBuild.cs), artifact `Eternal-Enigma-StandaloneWindows64-Development`,
  never published. Development players write [Memory] lines that GC first
  (live memory) and support command-line autoplay.

Next steps:
1. Find the CI run for 4264bb11 (`gh run list --branch webgl-memory-release`).
   If it failed, fix the compile error and push.
2. Run the development Windows player locally:
   `"Eternal Enigma.exe" --debug-autoplay-options <options.json> -logFile player.log`
   options.json: {"Mode":0,"Seed":42,"RunCount":1,"Speed":32,"NoAnimations":true,
   "DebugPlaythrough":true,"DebugCoverage":true,"Godmode":true,
   "InfiniteResources":true,"InfiniteStrength":true,"TimeLimitMinutes":60,
   "StallSeconds":120,"ActionLimit":100000,"ReportRoot":"<folder>"}
   Read the "live managed" [Memory] lines per floor/town and per 1000 actions.
3. Run the web build at 32x (see "How it was reproduced" below; launch Chrome
   with --mute-audio). The harness scripts (serve.js, driver3.js) are in an
   earlier session scratchpad under %TEMP%\claude\3130828a6d65a481\*\scratchpad
   (server\ or the root); index.html must be patched to expose
   `window.unityInstance`. Menu clicks at 1280x720: Developer (1029,606),
   Debug Autoplay (716,532, click twice), Fast mode (640,258), Start (728,522).
4. A Core-agent follow-up on TownPlanGenerator allocations (identical output,
   golden hash EFE8AEDA34725B4A, Core tests) may have left uncommitted changes
   under Core\ — check `git status`, re-verify, commit only Core\.

Lessons: stage explicit paths (a `commit -am` once swept in another
agent's half-done Core edits). PowerShell `R` is an alias; don't name
helpers R.

---

Original handoff written 2026-11-17 (investigation only) follows.

## Summary

The web build aborts with `abort("OOM")` when the WebAssembly heap reaches its
2048 MB cap. It was reported as "crashing on the first fight in a new biome"
during a debug autoplay in fast mode (32x, no animations).

The deferred-content (bundle) changes are **not the root cause**. The build from
just before that branch crashes the same way under the same autoplay, only later.
The new build starts play about 110 MB higher, so it reaches the cap sooner.

What consumes the memory is **not yet identified**. See "Open question".

## How it was reproduced

- Builds: CI artifacts `Eternal-Enigma-WebGL` from
  - run 43976822110, `main` at `96cdd92e` (after the branch merged)
  - run 43964684481, `main` at `08572cf5` (the commit before the branch)
- Served locally with `Content-Encoding: br` for the `.br` files, opened in
  headless Chrome 157 at 1280x720.
- Main menu > Developer > Debug Autoplay > Fast mode On (32x, no animations),
  seed 42, run count 1, invincibility / unlimited resources / try items and
  abilities all On > Start autoplay.
- Heap growth was logged by wrapping `WebAssembly.Memory.prototype.grow` before
  the page loads and printing the new size on every call.

The crash stack ends in `_emscripten_resize_heap` > `abortOnCannotGrowMemory`,
followed by `RuntimeError: memory access out of bounds`.

## Measurements

|                                      | Before branch (`08572cf5`) | Current `main` (`96cdd92e`) |
|--------------------------------------|----------------------------|-----------------------------|
| Heap at the main menu                | 166 MB                     | 412 MB                      |
| Heap once the first town has loaded  | 688 MB                     | 801 MB                      |
| Autoplay start to OOM                | about 135 s                | about 50 s                  |
| Result                               | OOM at 2048 MB             | OOM at 2048 MB              |

Each run was done once per build. The current build was also crashed once
earlier without heap logging, at a similar point.

Growth pattern (both builds):

- The heap grows in steps of roughly 96 MB. That is the configured growth cap
  (`webGLMemoryGeometricGrowthCap: 96`), so a step means "more was needed", not
  "96 MB was allocated".
- Most steps happen while the autoplay is inside dungeons, roughly one step per
  floor for the first 15 or so floors.
- The first town load adds about 300 MB in under a second. The overworld terrain
  build adds about 300 MB in about a second.
- It is not a fixed cost per floor. The old build reached 1986 MB, then ran 20
  more floors with no growth before one last 62 MB step hit the cap.
- Scene loads and Unity's "Unloading N unused Assets" passes never reduce the
  heap (a wasm heap cannot shrink) and growth resumes afterwards.
- Unity's loaded-object count stays between about 44k and 57k for the whole run.

## Ruled out

- Bundle size: all bundles total about 125 MB on disk.
- Next-floor prewarm (`Game.PrewarmNextFloor`, web-only main-thread path):
  `DungeonFloorGenerator.Generate` takes 2-300 ms per floor on desktop .NET
  across every biome, tier and variant, and the prewarm bails out when the floor
  has already changed.
- Assets accumulating: the loaded-object count is flat.
- A crash specific to biomes, fights, bosses or late content: the trigger is
  total memory, and where it lands depends on how much was used earlier.

## Open question: real leak, or per-frame garbage at 32x

Not determined. A 4x run to separate the two was started and cancelled before it
produced data. Normal play (1x, animations on) was not tested, so it is unknown
whether players are affected or only fast autoplay.

Evidence for per-frame garbage rather than a leak:

- The plateau: 20 floors with no growth is not what a per-floor leak looks like.
- `AutoplayRunner` (around line 310 of `Assets/Scripts/Common/AutoplayRunner.cs`)
  runs up to `ceil(Speed)` ticks per frame, within a 25 ms budget when animations
  are suppressed. In fast mode, whole turns and floor transitions can pile into a
  frame.
- The web player can, as far as I know, only collect garbage between frames, so
  everything allocated inside one frame has to fit at once. This was not verified
  against Unity 6000.2 documentation.

Evidence that leaves a leak possible:

- Growth continues across scene changes for the first 15 floors.
- No memory profile was taken, so managed versus native use is unknown.

## Update 2026-11-17: 4x run — not per-frame garbage

Same current `main` build (`96cdd92e`), same setup, but after starting with Fast
mode On the speed was set to 4x with `unityInstance.SendMessage('Watch demo',
'SetSpeed', 4)`. The panel showed "Playback speed 4×", animations off.

Result: **OOM at 2048 MB about 62 s after autoplay start** (32x: about 50 s).
Cutting the speed by 8x barely moved the crash, so step 2 below (cap autoplay work
per frame) will not fix it. Growth tracks content builds, not turn rate:

| Phase (time since autoplay start)          | Heap after   |
|--------------------------------------------|--------------|
| Main menu                                  | 412 MB       |
| First town built (0-4 s)                   | 883 MB       |
| Town to overworld transition (13-14 s)     | 1181 MB      |
| Overworld terrain built (24-26 s, 4.0 s)   | 1471 MB      |
| Terrain streaming, 1027 chunks over 7.7 s  | 1797 MB      |
| First dungeon floor (46 s)                 | 1904 MB      |
| Second town built (52-55 s), dungeon entry | 2048 MB, OOM |

- No dungeon floors were needed to reach the cap. About 1.4 GB went in before the
  first floor, almost all of it during town and overworld builds.
- Terrain streaming adds about 325 MB even though it is spread over 7.7 s of frames,
  so frame-boundary GC is not reclaiming it. Either it is retained, or the heap
  fragments and can never shrink back.
- Unity's loaded-object count is still only 44k-57k.

Most likely holders (step 3 should snapshot these first):
- `OverworldTerrainCache` keeps the whole TWC world, generated blueprint maps, the
  generated asset and the biome surfaces for the session, by design.
- `TownTerrainCache` and `CoreLayoutCache` keep town geometry. The second town got
  a new seed, so the first town's cached copy may not have been released first.
- Managed garbage from TWC layout and mesh generation (overworld layout 1447 ms,
  meshes 2549 ms; second town layout 1762 ms) raises the peak, and the wasm heap
  never shrinks back from it.

Revised next step: a development WebGL build with snapshots at the menu, after
town 1, after the overworld terrain finishes streaming, and after town 2. Normal
play should hit the same path (town, overworld, second town) in a few minutes, so
players are probably affected too. This has not been tested at 1x with animations.

## Update 2026-11-17: cache release fix (branch `webgl-memory-release`)

Commit on `webgl-memory-release`, CI run 44126220622. On web, generated meshes
in the cached town and overworld drop their CPU copy once finished. A stale cache
is cleared before its replacement is built. `[Memory]` lines are logged when
towns, the overworld and dungeon floors are ready.

Same 4x test: **OOM after about 200 s instead of 62 s**. The run got through 43
dungeon floors and 5 town loads (before: about 2 floors and 2 towns). No
mesh-readability errors.

- Native memory is now flat. It is about 350 MB in town or overworld and
  400-550 MB on dungeon floors. Releasing the overworld copies alone took native
  from 645 to 354 MB (1470 meshes). A town releases about 1900 meshes.
- **The remaining growth is the managed (Boehm) heap.** Reserved size goes
  254 > 526 > 599 > 712 > 778 > 859 MB, while live managed memory stays around
  230-320 MB. It reached 512 MB live in the frame the overworld finished
  building, then fell to 179 MB once a frame boundary let GC run. The reserved
  heap never shrinks, so each new peak is permanent.
- So the next lever is managed garbage in single-frame builds: overworld layout
  and meshes (about 2.7 s in one frame), town generation, and dungeon generation.
  Options: split these builds across frames so GC can run between steps, or cut
  their allocations (TWC blueprint maps, `EnvironmentBatch` vertex arrays and the
  smoothing dictionary).

## Suggested next steps

1. Separate the two cases first. Repeat the autoplay with animations off but the
   speed set to 4x from the autoplay panel after starting.
   - Growth per floor drops sharply: it is per-frame garbage. Go to step 2.
   - Growth per floor is unchanged: it is a leak. Go to step 3.
2. If it is per-frame garbage:
   - On web, cap autoplay work per frame: fewer ticks per frame and a smaller
     time budget in the loop at `AutoplayRunner.cs:310`. This is the smallest
     change and only affects the debug tool.
   - Reduce the large single-frame bursts. Candidates, none profiled:
     `EnvironmentBatch.Build` and `ApplyNormals` copy `mesh.vertices`,
     `mesh.normals` and `mesh.triangles` per chunk, and `ApplyNormals` reruns for
     every neighbouring chunk; town and dungeon generation go through TWC
     blueprint layers.
3. If it is a leak: make a development WebGL build and take memory snapshots
   after floors 1, 5 and 10. Look first at runtime-created meshes, materials and
   textures on dungeon floors (`EnvironmentMeshOwner` is the intended owner) and
   at the cloned TWC assets in `TileWorldDungeonGenerator.PreparePresentation`.
4. Independently, win back headroom on the new build. None of these were
   measured individually; the 110 MB difference is the total.
   - `WebBootstrap.Fetch` uses `UnityWebRequestAssetBundle.GetAssetBundle(url)`
     with no version or hash argument, so each bundle is held in memory rather
     than going through Unity's cache.
   - `WebBootstrap.PrefetchMusic` loads all ten tracks and keeps every clip in
     `tracks` for the life of the page.
   - `Common.OverworldTerrain` and `Common.TownTerrain` keep generated geometry
     across scene loads.
5. Raising `webGLMaximumMemorySize` above 2048 would only delay the crash and
   depends on browser support for larger wasm heaps. Not recommended as the fix.

## Leftovers

- Local branch `webgl-deferred-content` still exists; its remote is deleted and
  its commits are all in `main`.
- This file is not listed in `00-PRIORITY-INDEX.txt`.
