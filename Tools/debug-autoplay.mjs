// Read-only campaign evidence audit; never infers success from starting a run.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

const mode = process.argv[2] ?? 'status';
if (!['status', 'verify', 'inspect'].includes(mode)) throw Error('Usage: node Tools/debug-autoplay.mjs status|verify [batch directory], or inspect <report.json>');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
if (mode === 'inspect') {
  const r = read(resolve(process.argv[3]));
  console.log(JSON.stringify({ seed: r.Seed, outcome: r.Outcome, location: r.Location, floor: r.Floor,
    party: r.Party.map(p => ({ id: p.Id, name: p.Name, x: p.X, y: p.Y, hp: p.HP })),
    enemies: r.Enemies, stairs: r.Stairs,
    nearbyFloor: r.WalkableTiles.filter(p => r.Party.some(a => Math.max(Math.abs(p.x - a.X), Math.abs(p.y - a.Y)) <= 2)) }, null, 2));
  process.exit(0);
}
const logs = resolve(import.meta.dirname, '../Logs/Playthroughs');
const directory = process.argv[3] ? resolve(process.argv[3]) : readdirSync(logs).sort().reverse()
  .map(name => join(logs, name)).find(path => {
    try { const batch = read(join(path, 'batch.json')); return batch.Mode === 0 && batch.RequestedRuns >= 100; }
    catch { return false; }
  });
if (!directory) throw Error('No campaign batch requesting at least 100 runs found.');
const suite = existsSync(join(directory, 'campaign-suite.json')) ? read(join(directory, 'campaign-suite.json')) : null;
const batchDirectories = suite ? suite.Workers.map(worker => {
  const root = join(directory, worker, 'Playthroughs');
  return existsSync(root) ? readdirSync(root).sort().reverse().map(name => join(root, name))
    .find(path => existsSync(join(path, 'batch.json'))) : null;
}).filter(Boolean) : [directory];
const batches = batchDirectories.map(path => ({ directory: path, batch: read(join(path, 'batch.json')) }));
const requested = suite?.RequestedRuns ?? batches[0].batch.RequestedRuns;
const startingSeed = suite?.StartingSeed ?? batches[0].batch.StartingSeed;
const attempts = batches.flatMap(({ directory: path, batch }) => readdirSync(path, { withFileTypes: true }).filter(e => e.isDirectory())
  .map(entry => {
    const folder = join(path, entry.name);
    try { return { folder, batch, report: read(join(folder, 'report.json')) }; }
    catch { return null; } // A live writer can briefly hold a partial report.
  }).filter(Boolean)).sort((a, b) => a.report.Seed - b.report.Seed);
const finalized = attempts.filter(a => a.report.ArtifactsComplete);
const victories = finalized.filter(a => a.report.Outcome === 'Victory');
const active = attempts.filter(a => a.report.Outcome === 'Running');
const summary = {
  directory, state: batches.length !== (suite?.Workers.length ?? 1) ? 'Starting' :
    batches.every(b => b.batch.State === 'Completed') ? 'Completed' :
    batches.some(b => ['Stopped', 'Error'].includes(b.batch.State)) ? 'Stopped' : 'Running',
  requested, startingSeed,
  batches: batches.map(({ batch: b }) => ({ seed: b.StartingSeed, requested: b.RequestedRuns, state: b.State,
    completed: b.Runs.filter(r => r.ArtifactsComplete && r.Outcome !== 'Running').length })),
  finalized: finalized.length, victories: victories.length,
  distinctSeeds: new Set(victories.map(a => a.report.Seed)).size,
  builds: [...new Set(attempts.map(a => a.report.BuildId))],
  actions: attempts.reduce((n, a) => n + a.report.Actions, 0),
  seconds: attempts.reduce((n, a) => n + a.report.Seconds, 0),
  recent: finalized.slice(-5).map(({ report: r }) => ({ seed: r.Seed, outcome: r.Outcome, actions: r.Actions, seconds: r.Seconds })),
  active: active.map(({ report: r }) => ({ seed: r.Seed, actions: r.Actions, seconds: r.Seconds, location: r.Location, floor: r.Floor })),
  issues: attempts.filter(a => a.report.IssueCount || !['Running', 'Victory'].includes(a.report.Outcome))
    .map(({ folder, report: r }) => ({ seed: r.Seed, outcome: r.Outcome, issues: r.IssueCount, reason: r.Reason, folder })),
};
if (mode === 'verify') {
  const errors = [];
  const check = (condition, message) => { if (!condition) errors.push(message); };
  check(requested >= 100 && batches.every(b => b.batch.Mode === 0), 'Requires at least 100 campaign runs.');
  check(summary.state === 'Completed', 'Batch is not complete.');
  check(victories.length === requested && summary.distinctSeeds === requested, 'Every distinct seed must reach Victory.');
  check(summary.builds.length === 1 && !!summary.builds[0], 'Runs must use the same build.');
  check(batches.reduce((n, b) => n + b.batch.RequestedRuns, 0) === requested &&
    batches.every(b => b.batch.Runs.length === b.batch.RequestedRuns), 'Batch aggregate is incomplete.');
  for (let index = 0; index < requested; index++) {
    const seed = startingSeed + index;
    const attempt = victories.find(a => a.report.Seed === seed);
    check(!!attempt, `Seed ${seed}: missing finalized Victory.`);
    if (!attempt) continue;
    const { folder, batch, report: r } = attempt;
    const row = batch.Runs.find(row => row.Seed === seed);
    check(r.RunNumber === seed - batch.StartingSeed + 1 && r.Options.Seed === batch.StartingSeed &&
      r.Options.RunCount === batch.RequestedRuns && r.Options.Mode === 0 && r.Options.DebugPlaythrough && !r.EligibleForBalance,
      `Seed ${seed}: campaign debug identity mismatch.`);
    check(r.IssueCount === 0 && r.ErrorCount === 0 && r.WarningCount === 0, `Seed ${seed}: unresolved issues.`);
    check(r.Campaign?.Finished === true, `Seed ${seed}: campaign Finished flag missing.`);
    check(row?.Outcome === 'Victory' && row.ArtifactsComplete && row.RunId === r.RunId && row.BuildId === r.BuildId,
      `Seed ${seed}: aggregate/report mismatch.`);
    for (const name of ['report.json', 'save.json', 'actions.jsonl', 'issues.jsonl', 'final.png'])
      check(existsSync(join(folder, name)) && (name === 'issues.jsonl' || statSync(join(folder, name)).size > 0),
        `Seed ${seed}: missing artifact ${name}.`);
    if (!existsSync(join(folder, 'actions.jsonl'))) continue;
    const events = readFileSync(join(folder, 'actions.jsonl'), 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse);
    check(events.length > 0 && events.every(e => e.Seed === seed && e.RunId === r.RunId), `Seed ${seed}: action identity mismatch.`);
    check(events.some(e => e.Kind === 'Action' && e.Message === 'New campaign'), `Seed ${seed}: campaign start missing.`);
    check(events.filter(e => e.Kind === 'Action').length === r.Actions, `Seed ${seed}: action count mismatch.`);
    check(readFileSync(join(folder, 'issues.jsonl'), 'utf8').trim() === '', `Seed ${seed}: issue log is not empty.`);
  }
  summary.accepted = errors.length === 0; summary.errors = errors;
  if (errors.length) process.exitCode = 1;
}
console.log(JSON.stringify(summary, null, 2));
