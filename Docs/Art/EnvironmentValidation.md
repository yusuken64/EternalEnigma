# Environment validation

The project uses Unity 6000.2.7f2 and the Built-in render pipeline. Production
templates, the environment kit, diorama adapters and preview assets are committed.

`EnvironmentKitTests` checks bounds/budgets, wall connections, mountain/summit
seams, protected paths, deterministic placement, tree picks and facade UVs.
`EnvironmentPlaygroundTests` checks town rendering, biome cycling, output
ownership, authored controls and ocean/shore behavior. Core success alone does
not verify Unity rendering.

The retained post-cleanup reports record **379 Core passes**, **285 EditMode
passes**, and **323 PlayMode passes with four explicit skips**, with no failures.
Windows production-scene validation and the final WebGL build succeeded. Browser
runtime validation of the integrated diorama and isolated fit benchmark remains
open. See [diorama evidence](DioramaStyle.md#verification-and-reproduction) and
[remaining acceptance work](../../TODOs/09-diorama-restyle-plan.md).

[Painted environment verification](PaintedEnvironment.md) records the earlier
painted-art audit. Its failure counts and captures are historical, superseded by
the later reports above where the same checks were rerun. None of these retained
reports is a fresh test run for the 2026-10-08 documentation audit.

Saved [previews](EnvironmentPlayground.md) and [base kit measurements](EnvironmentKitSpecs.md)
describe their captured assets. Re-run relevant Unity fixtures after geometry or
template edits; geometry counts and editor timings do not establish target-device
frame rates.
