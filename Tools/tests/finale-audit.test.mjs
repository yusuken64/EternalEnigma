import test from 'node:test';
import assert from 'node:assert/strict';
import { auditFinale } from '../finale-audit.mjs';

function fixture() {
  const families = ['Castle Monster', 'Bone Dragon', 'Ice Golem', 'Flame Knight', 'Little Demon',
    'Skeleton Mage', 'Blue Slime', 'Crawler', 'Flower Monster', 'Star Fish'];
  const events = [], floors = [], deaths = [], gates = [];
  for (let floor = 1; floor <= 5; floor++) {
    const regions = [];
    for (let region = 0; region < 2; region++) {
      const id = `floor-${floor}/region-${region}`, actor = `${id}/mimic`;
      const family = families[(floor - 1) * 2 + region];
      if (region) { events.push({ Kind: 'RegionOpened', TargetId: id }); gates.push(id); }
      events.push({ Kind: 'EnemySpawned', TargetId: actor }, { Kind: 'MimicRevealed', TargetId: actor, Message: family },
        { Kind: 'EnemyDied', TargetId: actor });
      deaths.push(actor); regions.push({ Id: id, Planned: 1, Registered: 1, Remaining: 0, Cleared: true });
    }
    floors.push({ Floor: floor, Seed: floor * 42, Fingerprint: `map-${floor}`, Regions: regions });
    events.push({ Kind: 'StairsUsed', TargetId: `floor-${floor}` });
  }
  events.push({ Kind: 'HomecomingCompleted' });
  return { events, report: { Policy: 'astar-objectives-v5-mimic-finale', Campaign: { Finished: true },
    Finale: { PlanVersion: 2, PristineStart: true, FinalExitCompleted: true, HomecomingCompleted: true,
      Floors: floors, StairTransitions: [1, 2, 3, 4, 5], DefeatedEnemies: deaths,
      RevealedMimics: deaths.slice(), MimicFamilies: families, GateOpenings: gates, MapMovementCommands: 10 } } };
}
function errors({ report, events }) {
  const result = []; auditFinale(report, events, (ok, message) => { if (!ok) result.push(message); }); return result;
}
test('accepts complete production evidence', () => assert.deepEqual(errors(fixture()), []));
test('completion flag alone cannot pass', () => {
  const f = fixture(); f.report.Finale = { PlanVersion: 2 };
  assert.ok(errors(f).length > 5);
});
test('one surviving enemy, missing death or duplicated gate fails', () => {
  for (const mutate of [
    f => { f.report.Finale.Floors[4].Regions[1].Remaining = 1; },
    f => { f.events.splice(f.events.findIndex(e => e.Kind === 'EnemyDied'), 1); },
    f => { f.events.push(f.events.find(e => e.Kind === 'RegionOpened')); },
    f => { f.events.push({ Kind: 'EnemySpawned', TargetId: 'floor-5/region-1/mimic/summon-1' }); }
  ]) { const f = fixture(); mutate(f); assert.ok(errors(f).length > 0); }
});
test('hidden death, skipped floor or incomplete wake fails', () => {
  for (const mutate of [
    f => { [f.events[1], f.events[2]] = [f.events[2], f.events[1]]; },
    f => { f.report.Finale.StairTransitions.splice(2, 1); },
    f => { f.report.Finale.HomecomingCompleted = false; }
  ]) { const f = fixture(); mutate(f); assert.ok(errors(f).length > 0); }
});
