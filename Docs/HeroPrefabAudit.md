# Hero prefab checks

Run `node Tools/unity-mcp.mjs harness Heroes`, or use **Tools > Eternal Enigma > Tests > Run Heroes**.

The audit covers all 24 playable `Ally_MC` prefabs and their conversion into the shared dungeon `Ally` prefab. It validates:

- Unique stable IDs, names, animator/model/weapon references, root-motion settings and equipment components.
- Every catalog weapon's visible hand model, including each bow/arrow variant and unequipping. Bows occupy the logical main-hand
  equipment slot even though their models attach to the rig's left hand.
- Idle, forward movement, attack, hit and death clips in all eight weapon stances (192 hero/stance combinations).
- Town animation playback, model ownership after transfer and destruction of the town instance, player-team membership, health, enemy targeting, melee damage and dungeon animation playback.

The roster contains 24 playable prefabs; prior audit results are historical. The unnamed `TownAlly.prefab` is an unfinished authoring template, not a playable recruit; the test checks that every hero in the default recruit catalog is included. Duplicate display names use distinct stable IDs.

Corrections made during the audit:

- Avery (`Ally_MC01`) and Morgan (`Ally_MC02`) had unset `HeroAnimator` references despite having animation components. Their prefab references are now assigned, restoring town/overworld animation and the intended root-motion setting.
- The shared bow controller's normal forward movement state had an extra `0` suffix, which did not match the clip name used for playback. The state name is corrected.
- Bow stance selection recognizes main-hand bow equipment; the rig's left-hand bow
  model and right-hand arrow model resolve by variant, with Bow01/Arrow01 fallbacks.

This is a prefab wiring and baseline combat audit, not an exhaustive skill or balance certification.

`AssignedHeroClassesAreValidAndStartingGearFits` checks every hero with a class: the class (and secondary) are in `Resources/Classes/ClassCatalog`, secondary differs from primary, and starting equipment fits the class. Class assignments are implemented; new heroes must receive valid class/gear data.
