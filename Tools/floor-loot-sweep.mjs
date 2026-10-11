// Read-only evidence audit. No Unity calls, replay, regeneration, or inferred passes.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';

const mode = process.argv[2] ?? 'status';
if (!['status', 'verify', 'samples'].includes(mode)) throw Error('Usage: node Tools/floor-loot-sweep.mjs status|verify|samples [batch directory]');
const root = resolve(import.meta.dirname, '..');
const logs = join(root, 'Logs/Playthroughs');
const read = path => JSON.parse(readFileSync(path, 'utf8'));
const directories = existsSync(logs) ? readdirSync(logs).sort().reverse() : [];
const directory = process.argv[3] ? resolve(process.argv[3]) : directories.map(d => join(logs, d))
  .find(d => existsSync(join(d, 'manifest.json')));
if (!directory) throw Error('No floor sweep manifest found.');
const manifest = read(join(directory, 'manifest.json'));
const batch = read(join(directory, 'batch.json'));
const latest = new Map();
const invalid = [];
for (const entry of readdirSync(directory, { withFileTypes: true }).filter(e => e.isDirectory())) {
  const folder = join(directory, entry.name);
  if (!existsSync(join(folder, 'report.json'))) continue;
  let report;
  try { report = read(join(folder, 'report.json')); } catch { invalid.push(entry.name + ': interrupted report'); continue; }
  if (report.BuildId !== batch.BuildId || report.ContentId !== batch.ContentId || report.ManifestId !== manifest.Id) continue;
  const previous = latest.get(report.CaseId);
  if (!previous || report.StartedUtc > previous.report.StartedUtc) latest.set(report.CaseId, { report, folder });
}
const finalized = [...latest.values()].filter(a => a.report.ArtifactsComplete && ['Passed', 'Failed'].includes(a.report.Outcome));
const passed = finalized.filter(a => a.report.Outcome === 'Passed');
const failed = finalized.filter(a => a.report.Outcome === 'Failed');
const summary = {
  directory, state: batch.State, build: batch.BuildId, content: batch.ContentId, manifest: manifest.Id,
  requested: manifest.Cases.length, finalized: finalized.length, passed: passed.length, failed: failed.length,
  actions: finalized.reduce((n, a) => n + a.report.Actions, 0),
  collectedLoot: finalized.reduce((n, a) => n + a.report.CollectedLoot, 0),
  activeSeconds: finalized.reduce((n, a) => n + a.report.Seconds, 0),
  interruptedAttempts: invalid,
  failures: failed.map(a => ({ case: a.report.RunNumber, seed: a.report.Seed, profile: a.report.Profile,
    reason: a.report.Reason, unresolved: a.report.UnresolvedLoot, placement: a.report.PlacementIssues,
    folder: a.folder })),
};
if (mode === 'samples') {
  const presentations = new Map();
  for (const attempt of passed.sort((a, b) => a.report.RunNumber - b.report.RunNumber)) {
    const c = manifest.Cases[attempt.report.RunNumber - 1];
    if (!c || c.Throne) continue;
    const key = `${c.LayoutVersion}/${c.Biome}/${c.Environment}`;
    if (!presentations.has(key)) presentations.set(key, { case: c.Index + 1, profile: c.Profile,
      entrance: join(attempt.folder, 'entrance.png'), final: join(attempt.folder, 'gameplay-final.png') });
  }
  summary.samples = [...presentations.values()];
}
if (mode === 'verify') {
  // Preserved interrupted attempts do not invalidate a subsequent finalized pass.
  // A missing current pass is independently rejected for every manifest case.
  const errors = [];
  const require = (condition, reason) => { if (!condition) errors.push(reason); };
  require(manifest.Cases.length === 10000, 'Acceptance requires exactly 10,000 manifest cases.');
  require(passed.length === manifest.Cases.length, 'Every manifest case must have a finalized pass on the final version.');
  require(batch.State === 'Completed' && batch.AllFloorsPassed, 'Batch has not completed with every floor passing.');
  require(new Set(manifest.Cases.map(c => c.Id)).size === manifest.Cases.length, 'Duplicate case IDs.');
  require(new Set(manifest.Cases.map(c => c.Seed)).size === manifest.Cases.length, 'Duplicate seeds.');
  require(new Set(passed.map(a => a.report.GeneratedFloor)).size === passed.length, 'Missing or duplicate generated-floor fingerprints.');
  for (const [index, c] of manifest.Cases.entries()) {
    require(c.Index === index && c.Seed === manifest.StartingSeed + index, 'Invalid sequential case/seed: ' + index);
    const attempt = latest.get(c.Id);
    if (!attempt || attempt.report.Outcome !== 'Passed' || !attempt.report.ArtifactsComplete) continue;
    const { report: r, folder } = attempt;
    const prefix = `Case ${index + 1}: `;
    require(r.ValidationOnly && !r.EligibleForBalance && r.Options.Mode === 1, prefix + 'Not marked validation-only.');
    require(r.Seed === c.Seed && r.RunNumber === index + 1 && r.Profile === c.Profile, prefix + 'Identity mismatch.');
    require(r.GeneratedSeed === c.Seed && r.GeneratedWidth === c.Width && r.GeneratedHeight === c.Height &&
      r.GeneratedThrone === c.Throne, prefix + 'Generated floor differs from manifest.');
    require(r.GeneratedFloor && r.ExpectedLoot === r.CollectedLoot && r.ExpectedLoot === r.Loot.length, prefix + 'Loot totals mismatch.');
    require(r.RecoveryEvents === 0 && !r.PlacementIssues.length && !r.UnresolvedLoot.length && !r.ErrorCount, prefix + 'Unresolved verification issues.');
    for (const name of ['save.json', 'actions.jsonl', 'issues.jsonl', 'final.png', 'entrance.png', 'gameplay-final.png'])
      require(existsSync(join(folder, name)) && (name.endsWith('.jsonl') || statSync(join(folder, name)).size > 0), prefix + 'Missing artifact: ' + name);
    if (!existsSync(join(folder, 'actions.jsonl'))) continue;
    const receipts = readFileSync(join(folder, 'actions.jsonl'), 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse)
      .filter(e => e.Kind === 'LootCollection' && e.Success);
    require(receipts.length === r.CollectedLoot, prefix + 'Receipt count mismatch.');
    for (const loot of r.Loot) {
      const matching = receipts.filter(e => e.LootId === loot.Id);
      require(loot.Collected && loot.Removed && !loot.Failure && loot.ExpectedAmount > 0 &&
        loot.After - loot.Before === loot.ExpectedAmount && matching.length === 1, prefix + 'Invalid/duplicate receipt: ' + loot.Id);
      require(matching.every(e => e.CaseId === c.Id && e.Removed && e.StockAfter - e.StockBefore === e.ExpectedAmount &&
        e.ExpectedAmount === loot.ExpectedAmount), prefix + 'Action evidence mismatch: ' + loot.Id);
    }
  }
  summary.accepted = errors.length === 0;
  summary.errors = errors;
  if (errors.length) process.exitCode = 1;
}
console.log(JSON.stringify(summary, null, 2));
