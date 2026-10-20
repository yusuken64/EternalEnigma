# Book dungeons and zoom travel

The starting town contains **The Other Country**, an optional adventure book on a
reachable table in a residential house. It is separate from the opening home's
tutorial book. Clearing `story-0` enables entry; the book's card shows its biome,
five-floor length, and clear status. The card reuses the campaign save browser's
artwork frame without any save-slot actions.

The book uses normal dungeon combat and a deterministic biome different from its
host town. Both logical terrain and the Outdoor theme use that biome. It remains
replayable after completion, which adds a visible bookmark. Ordinary loot, retreat,
defeat and explicit-save rules apply. Success and retreat restore the entering
leader, facing, and legal party formation beside the book. No progression key or
traversal capability is awarded by this optional dungeon.

`BookDungeonDefinition` belongs to a separate optional catalog. Base campaign
generation, fingerprint, overworld topology and finale stay authoritative and
unchanged. Supported saves acquire `BookContentVersion = 1` in memory on Continue
after their original fingerprint has been checked. Only explicit saving persists
the upgrade; browsing slots does not write. See [campaign saves](CampaignSaves.md).

## Transition presentation

Town/dungeon, town/overworld and book/dungeon travel share `SceneTravelPresentation`.
Departure takes 0.45 seconds with exponential acceleration; arrival takes 0.65
seconds with exponential deceleration. The book first opens over 0.25 seconds.
Inward travel approaches the entrance or miniature; outward travel pulls away.
The near and wide camera sizes are 0.15 and 2.4 times the normal orthographic view.

`ScreenTransition` owns camera motion, HUD suppression and input blocking. It
covers the scene during the final third of departure, presents an opaque frame,
then performs the existing single-scene load. The destination resolves its anchor
after generation and starts a fresh arrival clock. Scene startup may hold the
cover without discarding the travel request. Camera follow resumes at the exact
gameplay pose when the reveal finishes.

No Animations and terminal presentation retain their ordinary covered-load
behavior. Other transitions, including menus, floor replacement, defeat recovery
and the finale homecoming, retain their existing behavior. Book entry never
changes the terminal preference.

## Authoring and checks

Use **Tools > Eternal Enigma > UI > Build Book Dungeon Prompt** to regenerate the
authored prompt prefab from the existing campaign card and shared UI controls.
The book reuses town furniture materials and the chosen biome's campaign artwork.
It replaces only the selected table's decorative contents; generated room geometry,
occupancy and saved arrival coordinates remain unchanged.

Run the Core solution tests, then rebuild/import its DLL. Unity suites:

- **Tools > Eternal Enigma > Tests > Run Book Travel EditMode**: placement, authored
  prompt, and campaign-save contracts.
- **Tools > Eternal Enigma > Tests > Run Book Travel**: book travel, five-floor
  replacement, loot/party return, defeat checkpoint restore, supported-save
  upgrade, normal/terminal prompt captures and transition lifecycle/input tests.
- **Tools > Eternal Enigma > Tests > Run Campaign**: existing campaign travel,
  sleep and town-service regressions.
- **Tools > Eternal Enigma > Tests > Run Book Travel Regressions**: campaign
  travel, sleep and services, card scaling, lazy overworld generation under cover,
  and finale/continue loading regressions.

Book captures are written to `Temp/BookTravel`; test summaries are written to
`Temp/HarnessResults`. The five-floor integration fixture advances floors directly
to check lifecycle and state transfer; it is not a combat-balance playthrough.
