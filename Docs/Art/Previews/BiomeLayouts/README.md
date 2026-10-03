# Biome layout previews

Seed 42, tier 4. Regular previews explicitly exercise the maximum 56x56 size; entry/exit previews are 24x24. These are generated Unity geometry renders, not concept art.

| Biome | Regular | Entry | Exit |
|---|---|---|---|
| Grassland | [56x56](Grassland_Regular.png) | [Entry](Grassland_Entry.png) | [Exit](Grassland_Exit.png) |
| Forest | [56x56](Forest_Regular.png) | [Entry](Forest_Entry.png) | [Exit](Forest_Exit.png) |
| Desert | [56x56](Desert_Regular.png) | [Entry](Desert_Entry.png) | [Exit](Desert_Exit.png) |
| Water | [56x56](Water_Regular.png) | [Entry](Water_Entry.png) | [Exit](Water_Exit.png) |
| Mountain | [56x56](Mountain_Regular.png) | [Entry](Mountain_Entry.png) | [Exit](Mountain_Exit.png) |
| Tundra | [56x56](Tundra_Regular.png) | [Entry](Tundra_Entry.png) | [Exit](Tundra_Exit.png) |
| Marsh | [56x56](Marsh_Regular.png) | [Entry](Marsh_Entry.png) | [Exit](Marsh_Exit.png) |
| Volcanic | [56x56](Volcanic_Regular.png) | [Entry](Volcanic_Entry.png) | [Exit](Volcanic_Exit.png) |

CPU timings, triangles and renderer counts: [costs.csv](costs.csv). Core timing measures one generation including validation. Geometry timing includes the blueprint generation, build layers and scenery. Export reruns through `Tools/Eternal Enigma/Tests/Run Biome Layouts`.

[Mean walkable area by biome and tier](area-by-tier.csv), sampled over seeds 0-23, shows increasing average area at every tier for all eight biomes.

Recorded 56x56 measurements: **84-157 ms Core generation**, **207-302 ms blueprint/geometry/scenery construction**, and **39,176-353,948 triangles**. Forest is the high-triangle outlier from its existing tile assets. These are editor CPU measurements, not runtime frame rates.
