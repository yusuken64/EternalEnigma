# Hero prefab checks

Run `node Tools/unity-mcp.mjs harness Heroes`, or use **Tools > Eternal Enigma > Tests > Run Heroes**.

The audit covers all 24 playable `Ally_MC` prefabs and their conversion into the shared dungeon `Ally` prefab. It validates:

- Unique stable IDs, names, animator/model/weapon references, root-motion settings and equipment components.
- Every catalog weapon's visible hand model, including left-hand bows and unequipping.
- Idle, forward movement, attack, hit and death clips in all eight weapon stances (192 hero/stance combinations).
- Town animation playback, model ownership after transfer and destruction of the town instance, player-team membership, health, enemy targeting, melee damage and dungeon animation playback.

All 24 passed. The unnamed `TownAlly.prefab` is an unfinished authoring template, not a playable recruit; the test checks that every hero in the default recruit catalog is included. Duplicate display names use distinct stable IDs.

Corrections made during the audit:

- Avery (`Ally_MC01`) and Morgan (`Ally_MC02`) had unset `HeroAnimator` references despite having animation components. Their prefab references are now assigned, restoring town/overworld animation and the intended root-motion setting.
- The shared bow controller's normal forward movement state had an extra `0` suffix, which did not match the clip name used for playback. The state name is corrected.
- Bow stance selection now recognizes the catalog's left-hand bow, rather than selecting the unarmed or main-hand weapon stance.

This is a prefab wiring and baseline combat audit, not an exhaustive skill or balance certification.

`AssignedHeroClassesAreValidAndStartingGearFits` checks every hero with a class: the class (and secondary) are in `Resources/Classes/ClassCatalog`, secondary differs from primary, and starting equipment fits the class. Unassigned heroes are skipped until Phase 7 assigns classes.
