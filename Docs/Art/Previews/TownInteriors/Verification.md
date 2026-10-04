# Town interior delivery verification

Delivered: 54 Blender-authored models, seven character NPCs, three spherical birds, 44
furniture/decorative/composite models, eight furniture palettes, shared character palette,
NPC definitions, service prefabs, reusable six-bone rigs and animation controllers. Sources,
FBX, lossless transports and manifests are retained. Generation version is 2.

## Passing checks

- Full Core suite: **345 passed**, including the deterministic 100-seed interior/NPC sweep.
- Final Unity interior EditMode suite: **4 passed**, including 100 seeds of published
  Core/Unity occupancy agreement, cache metadata, asset budgets/skinning, wall mounts,
  regeneration cleanup, and 97 sampled frames per bird checking fixed roots and perch clearance.
- Focused Unity town PlayMode suite: **6 passed**, covering greetings, blocked movement,
  full rebuild with stable actor counts, explicit save/revisit, campaign service access,
  inn checkpoint restoration, class-aware training and party movement.
- Eight biome captures: each has 12 furnished rooms, 95 logical prop placements, three
  birds, two ambient townsfolk and 24 supported wall decorations. All mask and repeated-build
  comparisons passed. Service actors are included in the room captures.
- Native TWC gallery exercises all four rules on straight, L, U and island masks. The
  visible carpet borders and counter surfaces join without gaps. Bird close-ups verify
  two exterior sign supports and one interior shelf support.
- Release Core DLL and Unity's imported DLL have matching SHA-256 hashes. Changed Core,
  generation and rendering code passed `git diff --check`.

See `UnityEditMode.json`, `UnityEditMode.xml`, `UnityPlayMode.json`, `UnityPlayMode.xml` and
`UnityVerification.csv`. `index.html` presents the reference board, palette, turnarounds,
idle/greeting animation previews, props, perches, four interiors and eight biome captures.

## Broader regression failures still present

The existing **Run Town** suite produced **13 passes and 11 failures**; its summary is
retained as `UnityPlayModeInitial.json`. This delivery does not claim that broader suite is
green. Six assertions expect automatic storage writes after shopping, training, donations,
recruitment or equipment changes. `SaveSystem.Capture` explicitly performs no storage write
in both HEAD and the current checkout; this work does not alter that save contract.

Two other tests time out on Continue/configured town return, and three time out waiting for
dungeon turn/floor animation completion (defeat, settings exit, victory). Those broader
transition failures are unresolved here. The dedicated furnished-town rebuild/revisit test
and the campaign inn-checkpoint return test pass with the current explicit-save contract.

Existing UI/scene work in the shared checkout has been retained; this task does not replace
it to make the older regression expectations pass.
