# Remaining animation, audio and feedback checks

Audited 2026-10-08 against `6555a9ba` and the later diorama integration.

The original implementation checklist is complete in source: filtered hero clip
pools, one-shot and status-loop playback, enemy wake/battle/taunt/dizzy/victory
hooks, status and selected-skill VFX overrides, status icons, surface footsteps,
dash sounds, theme/boss music, ambush cue and Standalone streaming imports.
Dungeon decoration resolves both EnvironmentKit and TownInteriorCatalog models,
uses source-appropriate materials and caps each piece at 250 triangles.
Town dressing and the authored home bed are also implemented.

`FeelAssetTests` passed 2/2 in the retained
[2026-10-07 EditMode report](../Docs/Art/Previews/Diorama/Verification/PostCleanupEditMode.xml).
It checks the Player/TownPlayer clip pools, icon uniqueness, theme music and
decoration lists; it does not prove every runtime event hook or visual/audio result.

## Remaining acceptance

- [ ] Verify every recruited hero's serialized clip pools and fallback behavior,
  beyond the two templates explicitly checked by `FeelAssetTests`.
- [ ] Cover one playback per level-up/victory/skill/status event, return to the
  correct idle stance, missing clips and animation-skipping behavior with focused
  PlayMode checks. Preserve action timing and gameplay random state.
- [ ] Review status icons, duration overlays, overhead effects and text fallbacks
  on party and enemy UI at gameplay scale.
- [ ] Listen to footsteps, dash, ambush and representative casts in normal and
  no-animation modes. Owned VFX authoring strips AudioSources and runtime cast
  stages suppress embedded audio; verify the resulting mix before closing the
  old double-audio report.
- [ ] Measure VFX build inclusion after the overrides. Do not reuse the obsolete
  517-prefab estimate from the removed `ImportedEffects` catalog field.

Optional polish retained from the art audit: hero Greeting playback, distinct
Claw/Bite/Block and item-use audio, alternate town music, and more distinctive
skill icons. Input glyphs remain in [task 03](03-input-prompts.md).
See [combat effects](../Docs/CombatEffects.md) and [dungeon themes](../Docs/DungeonThemes.md)
for the implemented authoring/runtime behavior.
