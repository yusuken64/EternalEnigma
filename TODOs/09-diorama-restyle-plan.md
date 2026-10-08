# Remaining diorama validation and art follow-ups

Audited 2026-10-08. Ground, terrain, foliage, houses, settlements, item visuals and
the in-scope art fixes landed in `cab69fce`. All eleven cleanup groups landed in
`02a12dc6` through `8e435a9e`. The old phase instructions, deletion candidates
and pre-fix findings have been removed from this active plan.

## Retained completion evidence

- [Style and authoring guide](../Docs/Art/DioramaStyle.md), including the measured
  hero-height ladder, source selection, palettes, Wave 4 handoff and ground ownership.
- [54-view comparison](../Docs/Art/Previews/Diorama/Review.html) and
  [item comparison](../Docs/Art/Previews/Diorama/Items/Review.html).
- [Determinism](../Docs/Art/Previews/Diorama/Verification/Determinism.txt):
  town 322 meshes / 1,703,422 triangles; overworld 1,361 / 2,352,142.
- Retained 2026-10-07 suites:
  [Core 379/0](../Docs/Art/Previews/Diorama/Verification/CoreFinal.trx),
  [EditMode 285/0](../Docs/Art/Previews/Diorama/Verification/PostCleanupEditMode.xml),
  [PlayMode 323/0, four explicit skips](../Docs/Art/Previews/Diorama/Verification/PostCleanupPlayMode.xml).
  These are recorded runs, not fresh checks of later commits.
- [Cleanup results](../Docs/Art/Previews/Diorama/CleanupCompleted.csv),
  [verification](../Docs/Art/Previews/Diorama/CleanupVerification.json) and
  [commits](../Docs/Art/Previews/Diorama/CleanupCommits.csv).
  Do not regenerate the frozen cleanup manifest or repeat completed deletions.
- [Windows build](../Docs/Art/Previews/Diorama/Verification/WindowsBuild.txt)
  and [player validation](../Docs/Art/Previews/Diorama/Verification/WindowsPlayerValidation.json)
  passed; production Town, Dungeon and Overworld captures were reviewed.
- The [integrated WebGL build](../Docs/Art/Previews/Diorama/Verification/WebGLBuild.txt)
  also **succeeded: 0 errors, 12 warnings, 233,526,160 bytes**.
  [WebGL inclusion data](../Docs/Art/Previews/Diorama/Verification/WebGLArtInclusion.csv)
  is retained. The earlier statement that this build was still pending was stale.

## Remaining acceptance

- [ ] Run the integrated WebGL player through Town, Dungeon and Overworld.
  Retain scene captures, runtime/shader errors and frame samples using the
  protocol in the style guide. A successful build is not runtime acceptance.
- [ ] Run the existing isolated WebGL fit benchmark for the environment and
  Wave 4 candidates. Compare it separately from ordinary backbuffer FPS.
- [ ] Update the review/evidence record with those runtime results. Rebuild only
  if the existing build is unavailable or source changes require it.

Wave 4 wrappers/materials and Windows fit evidence exist; production enemy
prefabs, spawn bands and bosses belong to [task 07](07-overworld-encounters-plan.txt).
Remaining animation/audio checks belong to [task 05](05-feel-improvements.md),
and prompt work to [task 03](03-input-prompts.md).

## Optional art work retained from the audit

- Evaluate tinted elite/biome enemy variants and per-recruit color variation.
- Review historical vendor-controller edits, duplicate projectile visuals and
  compiling vendor demo scripts before proposing further cleanup. The old
  zero-reference lists are not current deletion authorization.
- Quest/interaction markers over service NPCs and doors remain a separate
  presentation idea; existing gate markers and road signs serve other purposes.

Full lighting/shadow redesign, post-processing, camera zoom, a full dungeon-theme
redesign, extra interior packs and remodeling Adorable meshes remain outside
this restyle. The splash scene/audio remain in use.
