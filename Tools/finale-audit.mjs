// Shared production-finale audit for direct debug and full campaign reports.
export function auditFinale(report, events, check) {
  const f = report.Finale;
  const families = ['Castle Monster', 'Bone Dragon', 'Ice Golem', 'Flame Knight', 'Little Demon',
    'Skeleton Mage', 'Blue Slime', 'Crawler', 'Flower Monster', 'Star Fish'];
  check(report.Policy === 'astar-objectives-v5-mimic-finale' && f?.PlanVersion === 4, 'Current two-floor policy missing.');
  check(f?.PristineStart === true, 'Finale did not start with pristine objectives.');
  check(report.Campaign?.Finished === true && f?.FinalExitCompleted === true && f?.HomecomingCompleted === true,
    'Completion requires final stairs, the campaign flag, and wake-up with control restored.');
  check(f?.Floors?.length === 2 && f.Floors.every((floor, i) => floor.Floor === i + 1 && floor.Fingerprint &&
    floor.Regions?.length > 0 && floor.Regions.every(r => r.Planned > 0 && r.Registered >= r.Planned && r.Remaining === 0 && r.Cleared)),
    'Both floor populations must be cleared.');
  check(new Set(f?.Floors?.map(x => x.Seed)).size === 2, 'Expected two distinct deterministic floor seeds.');
  check(JSON.stringify(f?.StairTransitions) === '[1,2]', 'Every ascending staircase must be used exactly once.');
  const spawned = events.filter(e => e.Kind === 'EnemySpawned').map(e => e.TargetId);
  const deaths = events.filter(e => e.Kind === 'EnemyDied').map(e => e.TargetId);
  const reveals = events.filter(e => e.Kind === 'MimicRevealed');
  check(spawned.length > 0 && new Set(spawned).size === spawned.length && new Set(deaths).size === deaths.length &&
    spawned.length === deaths.length && spawned.every(id => deaths.includes(id) && f?.DefeatedEnemies?.includes(id)),
    'Every registered enemy, including packs and summons, needs one actual death event.');
  check(f?.Floors?.flatMap(x => x.Regions).reduce((n, r) => n + r.Registered, 0) === spawned.length,
    'Region population totals do not match spawned actors.');
  check(reveals.length === f?.RevealedMimics?.length && new Set(reveals.map(e => e.TargetId)).size === reveals.length &&
    reveals.every(e => f.RevealedMimics.includes(e.TargetId) && deaths.includes(e.TargetId)), 'Mimic revelation/death evidence mismatch.');
  check(families.every(family => reveals.some(e => e.Message === family) && f?.MimicFamilies?.includes(family)),
    'All ten reserved mimic families must be encountered.');
  const gates = events.filter(e => e.Kind === 'RegionOpened').map(e => e.TargetId);
  const expected = f?.Floors?.flatMap(floor => floor.Regions.slice(1).map(r => r.Id)) ?? [];
  check(expected.length > 0 && expected.length === gates.length && new Set(gates).size === gates.length &&
    expected.every(id => gates.includes(id) && f?.GateOpenings?.includes(id)), 'Region boundary openings missing or duplicated.');
  check(JSON.stringify(events.filter(e => e.Kind === 'StairsUsed').map(e => e.TargetId)) ===
    JSON.stringify([1,2].map(n => 'floor-' + n)), 'Production stair events incomplete.');
  check(events.filter(e => e.Kind === 'HomecomingCompleted').length === 1 && f?.MapMovementCommands > 0,
    'Expected movement and one completed homecoming event.');
  for (const event of reveals) {
    const revealIndex = events.indexOf(event), deathIndex = events.findIndex(e => e.Kind === 'EnemyDied' && e.TargetId === event.TargetId);
    check(revealIndex < deathIndex, 'A hidden mimic was killed before revelation: ' + event.TargetId);
  }
}
