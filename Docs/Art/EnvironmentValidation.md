# Environment validation

The project uses Unity 6000.2.7f2 and the built-in render pipeline. The environment kit,
production templates, playground and preview assets are committed.

`EnvironmentKitTests` checks imported bounds/budgets, wall connections, mountain/summit seams,
protected paths, deterministic placement, tree picks and facade UVs. `EnvironmentPlaygroundTests`
checks production-town rendering, biome cycling, output ownership, authored controls and ocean/shore behavior.
These tests need Unity; Core success alone does not verify them.

The current audit ran the full Core suite: **340 passed, 0 failed**. Earlier documents' three
Core failures are no longer current. Unity results for this audit are recorded in the project
test documentation when available; the old September counts are not presented as fresh results.

Saved [preview images](EnvironmentPlayground.md) and [mesh measurements](EnvironmentKitSpecs.md)
describe their captured assets. Re-run the Unity fixtures after geometry or template edits.
Geometry counts and editor timings are not frame-rate guarantees on target hardware.
