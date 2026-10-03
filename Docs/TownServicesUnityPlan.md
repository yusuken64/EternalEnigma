# Remaining town service integration

Status: partial implementation. Retained because the Core service-layout bridge is not yet
used by Unity. The former ten-trainer proposal is obsolete.

## Implemented

Core `TownServiceCatalog` offers bakery, consumables, items, inn and one shared trainer.
`TownLayout` sizes detailed towns, shuffles services into slots, reserves authored/residential
buildings, and exposes rooms, roads, props and walkability. CLI/explorer use these plans.

Unity has configurable building definitions, shop catalogs, vendor interiors, shared class
training, an inn with free rest/checkpoints, biome presentation and saved stock. Its generator
still takes `TownConfiguration.Buildings` and `PartySpawn`; `CampaignTravelService.PrepareTown`
clones the default configuration. It does not consume `TownLayout.SlotServices`.

## Remaining work

1. Bind campaign towns to Core's complete `TownLayout.Options` and slot service order, preserving
   authored entrance/statue slots and residential buildings. Use the plan's centered spawn/exit.
2. Author/resolve definitions for the five current service IDs and ensure each service has a
   carved room and interactable vendor. Keep one trainer showing the visiting hero's kit.
3. Connect detailed road, courtyard and prop layers to the town TWC template without changing
   Core walkability. Handle runtime dimensions, camera limits and preview rebuilding together.
4. Exercise service coverage, slot order, interior interaction, class skill points, inn restore,
   stock persistence and autoplay navigation in Unity. Core layout tests alone do not verify this.

Do not rerun removed one-time asset installers. Extend the committed configuration/prefab/TWC
assets in Unity and keep preview/verification tools. Current gameplay is documented in [Town](Town.md).
