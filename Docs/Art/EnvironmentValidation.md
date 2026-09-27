# Environment validation — 2026-09-27

Authored through the live Blender and Unity MCP connections. Blender 5.2.2 LTS; Unity 6000.2.7f2, built-in render pipeline.

## Completed checks

- Imported environment asset tests: **9 passed, 0 failed**. Covers all 68 meshes, eight palettes, 300-triangle individual model ceiling, 16 wall connection patterns, native cliff and summit seam heights, cosmetic protection/budgets, deterministic noise, bounded summit patches, biome tree picks, detailed facade UVs, and removal of legacy baked town clusters.
- Playground and production-town integration: **2 passed, 0 failed**. Generates both production TWC templates, cycles eight town palettes, repeats generation without duplicate owned meshes, checks seven authored button bindings, generates the smart-rule laboratory, checks ocean/shore layers, compares rendered frames to verify ocean animation, and confirms the production town uses detailed smart houses and contains no legacy baked clusters.
- Visual inspection: Blender source geometry and Unity smart-rule/coast renders. Road curbs follow exposed borders; joined summit patches sit on the third cliff plateau; shoreline quads remain inside water cells.

Seed 42 measured generated geometry (excluding the existing biome ground renderer and gameplay markers):

| Layer | Triangles |
|---|---:|
| Biome cosmetics (792 props) | 36,850 |
| Mountain base | 107,296 |
| Mountain tier 2 | 15,152 |
| Mountain tier 3 | 4,744 |
| Summit noise (72 quarters) | 576 |
| Roads | 78,310 |
| Houses | 7,776 |
| Walls | 6,192 |
| Ocean + noise overlay | 16 |
| Smart shoreline | 10,736 |

These are geometry measurements, not a target-hardware frame-rate benchmark.

Production overworld movement and return-trip regression: **5 passed, 0 failed** (before the final tree-variant addition; current variants are covered by the targeted environment tests).

## Broader suite limitations

Town gameplay/navigation regression run: **15 passed, 4 failed**. Three failures are equipment assertions (`TownInventoryUsesSharedActionMenuAndSavesEquipment`, `VictoryReturnsRemainingStacksAndEquipment`, `DefeatLosesItemsButKeepsGold`); the fourth is a dialog cast in `TrainerDialogShowsRanksAndRefreshesAfterPurchase`. No runtime exceptions were logged by the generated town. These failures are outside the changed rendering paths; unlike the core failures below, a clean-HEAD Unity baseline was not run.

The earlier full Edit Mode run passed 113 tests and failed seven outside the environment work: `AllySkillIntentTests.EmergencyBelowThirtyPercent`, `ClassContentTests.BossesAndResistancesAreAuthored`, `StatusEffectTests.StealthEndsWhenOwnerAttacks`, and four `TargetingAndArrowTests` cases (`ExpiredTauntIgnored`, `NearestFirstWithoutTaunt`, `StealthedAndDeadAreSkipped`, `TauntPullsToTaunterWhenVisible`). Failures concern combat/content data and null combat state.

The full core suite passed 223 tests and failed three overworld-generator cases: biome-pool sampling, default-dimension seed sweep, and the seed-42/variation-6 territory/pass-width case. Running those cases against a copy with the changed core source restored from HEAD reproduced the same three failures (four other selected cases passed). The only core change in this task exposes the existing biome selection through `BiomeForRegion`; it does not alter generation.

Local detailed results are in `Temp/HarnessResults`, and the isolated core baseline result is `Temp/CoreBaseline/EternalEnigma.Core.Tests/TestResults/baseline.trx`.



