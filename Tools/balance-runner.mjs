// Normal-game balance experiments. A valid defeat is data; an automation failure is not.
import { existsSync, readdirSync, readFileSync, writeFileSync, mkdirSync, appendFileSync, openSync, closeSync, statSync } from 'node:fs';
import { resolve, join, dirname, relative } from 'node:path';
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const POLICY = 'cautious-v2';
const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const write = (p, data) => { mkdirSync(dirname(p), { recursive: true }); writeFileSync(p, JSON.stringify(data, null, 2) + '\n'); };
const sha = p => createHash('sha256').update(readFileSync(p)).digest('hex');
const canonical = value => Array.isArray(value) ? value.map(canonical) : value && typeof value === 'object'
    ? Object.fromEntries(Object.keys(value).sort().map(k => [k, canonical(value[k])])) : value;
export const identity = value => createHash('sha256').update(JSON.stringify(canonical(value))).digest('hex');
const dirs = p => existsSync(p) ? readdirSync(p, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => join(p, e.name)).sort() : [];
const mean = a => a.length ? a.reduce((s, x) => s + x, 0) / a.length : null;
const gameplayOutcomes = new Set(['Victory', 'Defeat', 'ResourceLimit']);

export function wilson(success, count, z = 1.96) {
    if (!count) return [0, 1];
    const p = success / count, d = 1 + z * z / count;
    const center = (p + z * z / (2 * count)) / d;
    const margin = z * Math.sqrt(p * (1 - p) / count + z * z / (4 * count * count)) / d;
    return [Math.max(0, center - margin), Math.min(1, center + margin)];
}

export function validateConfig(c) {
    const fail = message => { throw Error(message); };
    if (!c || c.Version !== 1 || !['moderate', 'forgiving', 'demanding'].includes(c.Difficulty)) fail('Invalid balance config/version.');
    for (const key of ['TrainingSeeds', 'ValidationSeeds']) {
        if (!Array.isArray(c[key]) || !c[key].length || c[key].some(s => !Number.isInteger(s) || s < -2147483648 || s > 2147483647) || new Set(c[key]).size !== c[key].length)
            fail('Seed sets must contain distinct signed 32-bit integers.');
    }
    if (c.TrainingSeeds.some(s => c.ValidationSeeds.includes(s))) fail('Validation seeds must be held out.');
    if (!Array.isArray(c.Heroes) || c.Heroes.length < 1 || new Set(c.Heroes).size !== c.Heroes.length || c.Heroes.some(h => typeof h !== 'string' || !h)) fail('Explicit distinct protagonist IDs are required.');
    if (!Number.isInteger(c.Workers) || c.Workers < 1 || c.Workers > 2 || !Number.isInteger(c.MaxCandidates) || c.MaxCandidates < 1 || c.MaxCandidates > 30 ||
        !(c.RunMinutes > 0 && c.RunMinutes <= 120) || !(c.MaxHours > 0 && c.MaxHours <= 168)) fail('Invalid resource bounds.');
    const t = c.Targets;
    if (!t || !Number.isInteger(t.MinRuns) || t.MinRuns < 10 || !Number.isInteger(t.MinStageRuns) || t.MinStageRuns < 5 ||
        ![t.CampaignSuccess, t.StageSuccess].every(n => Number.isFinite(n) && n > 0 && n <= 1) ||
        !(t.MaxRetreats >= 0 && t.MaxRetreats <= 30) || !Number.isFinite(t.MaxMeanRetreats) || t.MaxMeanRetreats < 0 ||
        !Array.isArray(t.RequiredTiers) || !t.RequiredTiers.length || t.RequiredTiers.some(n => !Number.isInteger(n) || n < 0)) fail('Invalid fairness targets.');
    return c;
}

export function defaultConfig(catalog) {
    const representative = ['Rowan','Riley','Taylor'].map((name,i) => catalog.HeroDetails?.find(h=>h.Name===name && (i!==2 || h.Primary==='Archer'))?.Id).filter(Boolean);
    return { Version: 1, Difficulty: 'moderate', Workers: 2, MaxCandidates: 7, RunMinutes: 45, MaxHours: 24,
        TrainingSeeds: Array.from({ length: 6 }, (_, i) => 42 + i), ValidationSeeds: Array.from({ length: 10 }, (_, i) => 1042 + i),
        Heroes: representative.length === 3 ? representative : catalog.Heroes.slice(0, 3),
        Targets: { MinRuns: 18, MinStageRuns: 12, CampaignSuccess: .65, StageSuccess: .75,
            MaxRetreats: 8, MaxMeanRetreats: 3, RequiredTiers: [0, 1, 2, 3, 4] } };
}

export function makeOptions(seed, hero, candidate, cohort, directory, manifest) {
    return { Mode: 0, Seed: seed, RunCount: 1, Speed: 32, NoAnimations: true, DebugPlaythrough: false,
        DebugCoverage: false, Godmode: false, InfiniteResources: false, InfiniteStrength: false, ExploreAll: false,
        ActionLimit: 200000, TimeLimitMinutes: manifest.Config.RunMinutes, StallSeconds: 180, ReportRoot: directory,
        Balance: { ExperimentId: manifest.Id, CandidateId: candidate.Id, ContentId: manifest.ContentId,
            HeroId: hero, Cohort: cohort, ItemPrices: candidate.ItemPrices, AbilityMana: candidate.AbilityMana,
            MaxRetreats: manifest.Config.Targets.MaxRetreats } };
}

export function auditReport(report, expected) {
    const errors = [], check = (ok, why) => { if (!ok) errors.push(why); };
    const o = report?.Options, b = report?.Balance;
    check(report?.ArtifactsComplete === true, 'Artifacts incomplete.');
    check(gameplayOutcomes.has(report?.Outcome), 'Non-gameplay outcome: ' + report?.Outcome);
    check(report?.IssueCount === 0 && report?.ErrorCount === 0 && report?.WarningCount === 0, 'Runtime/policy issues require investigation.');
    check(report?.EligibleForBalance === true && report?.ValidationOnly === false, 'Not eligible for balance.');
    check(report?.Seed === expected.Seed && report?.RunNumber === 1, 'Wrong seed/run.');
    check(report?.Policy === POLICY && b?.Policy === POLICY, 'Policy version mismatch.');
    check(typeof report?.BuildId === 'string' && report.BuildId.length > 0, 'Missing build identity.');
    check(o?.Mode === 0 && o.RunCount === 1 && [o.DebugPlaythrough, o.DebugCoverage, o.Godmode, o.InfiniteResources, o.InfiniteStrength].every(v => v === false), 'Debug assistance or wrong mode.');
    check(identity(o?.Balance) === identity(expected.Balance), 'Experiment configuration mismatch.');
    check(b?.CandidateId === expected.Balance.CandidateId && b?.ExperimentId === expected.Balance.ExperimentId && b?.ContentId === expected.Balance.ContentId &&
        b?.Cohort === expected.Balance.Cohort && b?.HeroId === expected.Balance.HeroId, 'Report experiment identity mismatch.');
    check(Array.isArray(b?.Stages) && b.Stages.length > 0 && b.Stages.every(s => s.Outcome !== 'Running'), 'Missing or unfinalized progression stages.');
    check(o?.Seed === expected.Seed && o?.Speed === expected.Speed && o?.NoAnimations === expected.NoAnimations && o?.ActionLimit === expected.ActionLimit &&
        o?.TimeLimitMinutes === expected.TimeLimitMinutes, 'Execution settings mismatch.');
    if (report?.Outcome === 'Victory') check(report.Campaign?.Finished === true && report.Finale?.HomecomingCompleted === true, 'Missing real campaign completion.');
    return errors;
}

export function summarize(attempts, targets) {
    const valid = attempts.filter(a => a.Valid), errors = attempts.filter(a => !a.Valid);
    const builds = new Set(valid.map(a => a.Report.BuildId));
    const variants = new Set(valid.map(a => identity([a.Report.Balance?.CandidateId,a.Report.Balance?.ContentId,a.Report.Balance?.Cohort,a.Report.Policy])));
    const cases = new Set(attempts.map(a => `${a.Seed}/${a.Hero}`));
    const wins = valid.filter(a => a.Report.Outcome === 'Victory').length;
    const stages = new Map(), abilities = new Map(), purchases = new Map(), effects = new Map();
    for (const a of valid) {
        const perRun = new Map();
        for (const s of a.Report.Balance.Stages) {
            const key = `${s.Location}/${s.Kind}/tier-${s.Tier}/floor-${s.Floor}`;
            let row = perRun.get(key);
            if (!row) perRun.set(key, row = { Key: key, Tier: s.Tier, Cleared: false, Turns: 0, Retreats: 0, Damage: 0, Healing: 0, Downings: 0, EntryLevel: mean(s.EntryParty.map(p => p.Level)), Health: 1 });
            row.Cleared ||= ['Cleared', 'Victory'].includes(s.Outcome);
            row.Turns += s.Turns; row.Retreats += s.Retreats; row.Damage += s.DamageTaken; row.Healing += s.HealingReceived;
            row.Downings += s.Downings; row.Health = Math.min(row.Health, s.MinimumHealthFraction);
        }
        for (const [key, row] of perRun) { if (!stages.has(key)) stages.set(key, []); stages.get(key).push(row); }
        for (const s of a.Report.Balance.Abilities) {
            const key = `${s.Skill}/rank-${s.Rank}`;
            if (!abilities.has(key)) abilities.set(key, { Skill: s.Skill, Rank: s.Rank, Decisions: 0, Opportunities: 0, Unaffordable: 0, Selected: 0, Started: 0, Releases: 0, ManaSpent: 0 });
            const row = abilities.get(key); row.Decisions += s.Decisions; row.Opportunities += s.LegalOpportunities; row.Unaffordable += s.Unaffordable; row.Selected += s.Selected;
            row.Started += s.Started ?? 0; row.Releases += s.Releases ?? 0; row.ManaSpent += s.ManaSpent ?? 0;
        }
        for (const p of a.Report.Purchases ?? []) if (p.Success) {
            if (!purchases.has(p.Name)) purchases.set(p.Name, { Item: p.Name, Purchases: 0, Gold: 0 });
            const row = purchases.get(p.Name); row.Purchases++; row.Gold += p.Cost;
        }
        for(const e of a.Report.Balance.Effects ?? []) {
            const key=identity([e.Skill,e.Item,e.RootAction,e.Rank,e.TargetTeam]);
            if(!effects.has(key)) effects.set(key,{Skill:e.Skill,Item:e.Item,Action:e.RootAction,Rank:e.Rank,TargetTeam:e.TargetTeam,
                HPRemoved:0,HPRestored:0,ManaRemoved:0,ManaRestored:0,HungerRestored:0,Changes:0});
            const row=effects.get(key);for(const f of ['HPRemoved','HPRestored','ManaRemoved','ManaRestored','HungerRestored','Changes']) row[f]+=e[f];
        }
    }
    const stageRows = [...stages].map(([key, rows]) => {
        const cleared = rows.filter(s => s.Cleared).length;
        return { Stage: key, Tier: rows[0].Tier, Reached: rows.length, Cleared: cleared, Success: cleared / rows.length,
            Interval95: wilson(cleared, rows.length), MeanTurns: mean(rows.map(s => s.Turns)), MeanEntryLevel: mean(rows.map(s => s.EntryLevel)),
            MeanRetreats: mean(rows.map(s => s.Retreats)), MeanDamage: mean(rows.map(s => s.Damage)), MeanHealing: mean(rows.map(s => s.Healing)),
            MeanDownings: mean(rows.map(s => s.Downings)), MeanMinimumHealth: mean(rows.map(s => s.Health)) };
    }).sort((a, b) => a.Tier - b.Tier || a.Stage.localeCompare(b.Stage));
    const interval = wilson(wins, valid.length), retreats = mean(valid.map(a => a.Report.Balance.Retreats));
    const reasons = [];
    if (errors.length) reasons.push(`${errors.length} incomplete or invalid attempts; tuning is blocked until investigated.`);
    if (builds.size !== 1) reasons.push('Evidence must belong to one build.');
    if (variants.size !== 1) reasons.push('Evidence must belong to one candidate, cohort, policy and content version.');
    if (cases.size !== attempts.length) reasons.push('Duplicate seed/protagonist cases cannot inflate the sample.');
    if (valid.length < targets.MinRuns) reasons.push(`Need at least ${targets.MinRuns} valid campaigns.`);
    if (interval[0] < targets.CampaignSuccess) reasons.push('Campaign success lower confidence bound is below target.');
    if (retreats == null || retreats > targets.MaxMeanRetreats) reasons.push('Retreat burden exceeds target.');
    for (const tier of targets.RequiredTiers) if (!stageRows.some(s => s.Tier === tier && s.Reached >= targets.MinStageRuns)) reasons.push(`Insufficient coverage at tier ${tier}.`);
    for (const s of stageRows) if (s.Reached >= targets.MinStageRuns && s.Interval95[0] < targets.StageSuccess) reasons.push(`${s.Stage}: conditional clearance below target.`);
    const heroes = [...new Set(valid.map(a=>a.Hero))].map(hero=>{
        const rows=valid.filter(a=>a.Hero===hero), cleared=rows.filter(a=>a.Report.Outcome==='Victory').length;
        return {Hero:hero,Runs:rows.length,Wins:cleared,Success:cleared/rows.length,Interval95:wilson(cleared,rows.length)};
    });
    for (const hero of heroes) if (hero.Success < targets.CampaignSuccess) reasons.push(`${hero.Hero}: protagonist success below target.`);
    return { Requested: attempts.length, Valid: valid.length, Invalid: errors.length, Wins: wins,
        CampaignSuccess: valid.length ? wins / valid.length : null, CampaignInterval95: interval, MeanRetreats: retreats,
        Accepted: reasons.length === 0, Reasons: reasons, Heroes: heroes, Stages: stageRows, Abilities: [...abilities.values()], Items: [...purchases.values()], Effects:[...effects.values()],
        MeanClearedStages: mean(valid.map(a=>clearedStages(a.Report))),
        Score: valid.length ? wins / valid.length * 100 + mean(valid.map(a => a.Report.Campaign?.Completed?.length ?? 0)) * 2 + mean(valid.map(a=>clearedStages(a.Report))) * .5 - retreats * 2 : null,
        InvalidAttempts: errors.map(a => ({ Seed: a.Seed, Hero: a.Hero, Directory: a.Directory, Errors: a.Errors })) };
}

const clearedStages = report => new Set(report.Balance.Stages.filter(s=>['Cleared','Victory'].includes(s.Outcome)).map(s=>`${s.Location}/${s.Floor}`)).size;

export function pairedComparison(base, candidate) {
    const key = a => `${a.Seed}/${a.Hero}`;
    const original = new Map(base.filter(a => a.Valid).map(a => [key(a), a]));
    const pairs = candidate.filter(a => a.Valid && original.has(key(a))).map(a => {
        const b = original.get(key(a));
        return { Seed: a.Seed, Hero: a.Hero,
            WinDelta: Number(a.Report.Outcome === 'Victory') - Number(b.Report.Outcome === 'Victory'),
            ProgressDelta: (a.Report.Campaign?.Completed?.length ?? 0) - (b.Report.Campaign?.Completed?.length ?? 0),
            StageDelta: clearedStages(a.Report) - clearedStages(b.Report),
            RetreatDelta: a.Report.Balance.Retreats - b.Report.Balance.Retreats };
    });
    return { Pairs: pairs.length, WinDelta: mean(pairs.map(p => p.WinDelta)), ProgressDelta: mean(pairs.map(p => p.ProgressDelta)),
        StageDelta: mean(pairs.map(p => p.StageDelta)), RetreatDelta: mean(pairs.map(p => p.RetreatDelta)), Rows: pairs };
}

export function proposeCandidates(attempts, catalog, limit) {
    const skills = new Map(), items = new Map();
    for (const a of attempts.filter(a => a.Valid)) {
        for (const s of a.Report.Balance.Abilities) skills.set(s.Skill, (skills.get(s.Skill) ?? 0) + s.Unaffordable);
        for (const p of a.Report.Purchases.filter(p => p.Success)) items.set(p.Name, (items.get(p.Name) ?? 0) + p.Cost);
        for (const p of a.Report.Balance.Purchases ?? []) if (p.Unaffordable > 0) items.set(p.Item, (items.get(p.Item) ?? 0) + p.Unaffordable);
    }
    const result = [];
    for (const [name, burden] of [...items].sort((a, b) => b[1] - a[1])) {
        if (!catalog.Items.some(i => i.Name === name && (i.HP > 0 || i.Mana > 0 || i.Food > 0)) || burden <= 0) continue;
        for (const multiplier of [.85,.75]) if (catalog.Items.some(i=>i.Name===name && i.Price>0 && Math.max(1,Math.round(i.Price*multiplier))!==i.Price))
            result.push({ Id: `price-${result.length + 1}`, ItemPrices: [{ Name: name, Multiplier: multiplier }], AbilityMana: [], Reason: `Reduce recurrent supply burden: ${name}.` });
    }
    for (const [name, blocked] of [...skills].sort((a, b) => b[1] - a[1])) {
        if (blocked < 5 || !catalog.Abilities.some(s => s.Name === name && s.Mana >= 2)) continue;
        const seenCosts=new Set();
        for (const multiplier of [.85,.75]) {
            const costs=catalog.Abilities.filter(s=>s.Name===name).map(s=>Math.max(1,Math.round(s.Mana*multiplier)));
            const key=identity(costs);
            if(seenCosts.has(key) || !catalog.Abilities.some(s=>s.Name===name && Math.max(1,Math.round(s.Mana*multiplier))!==s.Mana)) continue;
            seenCosts.add(key);
            result.push({ Id: `mana-${result.length + 1}`, ItemPrices: [], AbilityMana: [{ Name: name, Multiplier: multiplier }], Reason: `Test repeated Mana shortage: ${name}.` });
        }
    }
    // Alternate economy and ability hypotheses instead of allowing one family to consume the whole budget.
    const prices = result.filter(c => c.ItemPrices.length), mana = result.filter(c => c.AbilityMana.length), ordered = [];
    while ((prices.length || mana.length) && ordered.length < limit) { if (prices.length) ordered.push(prices.shift()); if (mana.length && ordered.length < limit) ordered.push(mana.shift()); }
    return ordered;
}

export function sourceFingerprint() {
    const hash = createHash('sha256');
    function visit(directory) {
        for (const e of readdirSync(directory, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
            const p = join(directory, e.name);
            if (e.isDirectory() && !['bin','obj','.git'].includes(e.name)) visit(p);
            else if (/\.(cs|asset|prefab|unity|json|asmdef)$/.test(e.name)) hash.update(relative(root, p).replaceAll('\\', '/')).update(readFileSync(p));
        }
    }
    for (const directory of ['Assets/Scripts', 'Assets/Resources', 'Assets/Scenes', 'Assets/Prefabs', 'Assets/Data', 'Core/EternalEnigma.Core/EternalEnigma.Core']) if (existsSync(join(root, directory))) visit(join(root, directory));
    for (const name of ['ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset']) if (existsSync(join(root,name))) hash.update(name).update(readFileSync(join(root,name)));
    return hash.digest('hex');
}

function findReport(directory) {
    const candidates = dirs(join(directory, 'Playthroughs')).flatMap(dirs).map(d => join(d, 'report.json')).filter(existsSync);
    if (candidates.length !== 1) throw Error('Expected exactly one run report.');
    return candidates[0];
}

function auditArtifacts(path, report) {
    const errors = [];
    for (const name of ['report.json', 'save.json', 'actions.jsonl', 'issues.jsonl', 'decisions.jsonl', 'final.png']) {
        const p = join(dirname(path), name);
        if (!existsSync(p) || name !== 'issues.jsonl' && statSync(p).size === 0) errors.push('Missing artifact: ' + name);
    }
    if (errors.length) return errors;
    const events = readFileSync(join(dirname(path), 'actions.jsonl'), 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse);
    if (events.some(e => e.RunId !== report.RunId || e.Seed !== report.Seed)) errors.push('Action identities diverge.');
    if (events.filter(e => e.Kind === 'Action').length !== report.Actions) errors.push('Action count does not match.');
    if (readFileSync(join(dirname(path), 'issues.jsonl'), 'utf8').trim()) errors.push('Issue artifact is not empty.');
    if (!events.some(e => e.Kind === 'Action' && e.Message === 'New campaign')) errors.push('Missing real campaign start.');
    return errors;
}

function csv(path, rows) {
    if (!rows.length) { writeFileSync(path, ''); return; }
    const keys = Object.keys(rows[0]);
    const cell = x => '"' + String(typeof x === 'object' ? JSON.stringify(x) : x ?? '').replaceAll('"', '""') + '"';
    writeFileSync(path, [keys.map(cell).join(','), ...rows.map(r => keys.map(k => cell(r[k])).join(','))].join('\n') + '\n');
}

function saveSummary(directory, attempts, targets, expected = attempts.length) {
    const s = summarize(attempts, targets); s.Expected = expected;
    if(attempts.length!==expected) {s.Accepted=false;s.Reasons.push('Cohort incomplete.');}
    write(join(directory, 'summary.json'), s);
    for (const key of ['Stages', 'Abilities', 'Items', 'Effects']) csv(join(directory, key.toLowerCase() + '.csv'), s[key]);
    const escape = x => String(x ?? '').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
    const table = rows => !rows.length ? '<p>No observations.</p>' : '<table><thead><tr>' + Object.keys(rows[0]).map(k=>'<th>'+escape(k)+'</th>').join('') + '</tr></thead><tbody>' +
        rows.map(r=>'<tr>'+Object.values(r).map(v=>'<td>'+escape(typeof v==='object'?JSON.stringify(v):Number.isFinite(v)&&!Number.isInteger(v)?v.toFixed(3):v)+'</td>').join('')+'</tr>').join('')+'</tbody></table>';
    writeFileSync(join(directory,'report.html'), '<!doctype html><meta charset="utf-8"><title>Progression balance evidence</title><style>body{font:16px system-ui;margin:32px;color:#172332;background:#f5f7fa}table{border-collapse:collapse;background:white;font-size:13px}th,td{padding:8px;border:1px solid #ccd4df;text-align:left}section{overflow:auto;margin:24px 0}h1{font-size:28px}li{margin:8px}</style>' +
        `<h1>Progression balance evidence</h1><p>${s.Valid} valid / ${s.Requested} attempted campaigns. ${s.Wins} victories. Acceptance: ${s.Accepted ? 'targets met for this cohort' : 'not established'}.</p>` +
        '<p>Conditional stage success includes only campaigns that reached that stage. Intervals are 95% Wilson intervals. Ability estimates are not measured effects.</p><ul>'+s.Reasons.map(r=>'<li>'+escape(r)+'</li>').join('')+'</ul>'+
        ['Heroes','Stages','Abilities','Items','Effects','InvalidAttempts'].map(k=>'<section><h2>'+k+'</h2>'+table(s[k])+'</section>').join(''));
    return s;
}

async function runExperiment(directory, configPath) {
    const config = validateConfig(read(configPath)), catalog = read(join(root, 'Logs/Balance/catalog.json'));
    if (config.Heroes.some(id => !catalog.Heroes.includes(id))) throw Error('Unknown hero in experiment.');
    if (existsSync(join(directory, 'manifest.json'))) throw Error('Use a new experiment directory; prior evidence is preserved.');
    const exe = join(root, 'Builds/DebugAutoplay/DebugAutoplay.exe'), assembly = join(root, 'Builds/DebugAutoplay/DebugAutoplay_Data/Managed/EternalEnigma.Game.dll');
    if (!existsSync(exe) || !existsSync(assembly)) throw Error('Build the current development player first.');
    const buildStampPath = join(root, 'Builds/DebugAutoplay/balance-build.json');
    if (!existsSync(buildStampPath)) throw Error('Missing balance build identity; build through the Balance menu.');
    const contentId = sourceFingerprint(), stamp = read(buildStampPath);
    if (stamp.ContentId !== contentId || stamp.AssemblySha256 !== sha(assembly)) throw Error('Build is stale relative to the current code/content.');
    const manifest = { Version: 1, Id: identity({ config, contentId, utc: new Date().toISOString() }).slice(0, 20),
        StartedUtc: new Date().toISOString(), Config: config, ContentId: contentId, AssemblySha256: sha(assembly),
        DriverSha256:sha(fileURLToPath(import.meta.url)), Policy: POLICY, Catalog: catalog };
    write(join(directory, 'manifest.json'), manifest);
    const started = Date.now(), active = new Set(); let stopped = false;
    const stop = () => { stopped = true; };
    process.on('SIGINT', stop); process.on('SIGTERM', stop);
    const requestedStop = () => stopped || existsSync(join(directory, 'stop.request')) || Date.now() - started > config.MaxHours * 3600000;
    const status = (state, data = {}) => write(join(directory, 'status.json'), { State: state, UpdatedUtc: new Date().toISOString(), ProcessId: process.pid, ...data });
    const event = (kind, data) => appendFileSync(join(directory, 'events.jsonl'), JSON.stringify({ Utc: new Date().toISOString(), Kind: kind, ...data }) + '\n');
    async function oneCase(candidate, cohort, seed, hero, index) {
        if (requestedStop()) return null;
        if (sha(assembly) !== manifest.AssemblySha256) throw Error('Build changed during experiment.');
        const folder = join(directory, candidate.Id, cohort, `case-${String(index).padStart(4, '0')}`);
        const options = makeOptions(seed, hero, candidate, cohort, folder, manifest);
        write(join(folder, 'options.json'), options);
        const fd = openSync(join(folder, 'driver.log'), 'a');
        const child = spawn(exe, ['-batchmode', '--balance-options', join(folder, 'options.json'), '-screen-fullscreen', '0', '-screen-width', '640', '-screen-height', '360', '-logFile', join(folder, 'player.log')],
            { cwd: root, windowsHide: true, stdio: ['ignore', fd, fd] });
        closeSync(fd); active.add(child);
        event('Started', { Candidate: candidate.Id, Cohort: cohort, Seed: seed, Hero: hero, Pid: child.pid });
        let timedOut = false, timer;
        const launched = Date.now();
        const exit = await new Promise(resolveExit => {
            child.once('error', error => resolveExit({ Error: error.message }));
            child.once('exit', code => resolveExit({ ExitCode: code }));
            timer = setInterval(() => {
                if (requestedStop() || Date.now() - launched > (config.RunMinutes * 60 + 240) * 1000) {
                    timedOut = true;
                    if (child.exitCode == null && child.signalCode == null) child.kill();
                }
            }, 2000);
        });
        clearInterval(timer); active.delete(child);
        const result = { Seed: seed, Hero: hero, Directory: folder, ...exit, TimedOut: timedOut, Valid: false, Errors: [] };
        try {
            const path = findReport(folder); result.Report = read(path);
            result.Errors.push(...auditReport(result.Report, options), ...auditArtifacts(path, result.Report));
            if (exit.ExitCode !== 0 || exit.Error || timedOut) result.Errors.push('Player did not complete cleanly.');
            if (sha(assembly) !== manifest.AssemblySha256) result.Errors.push('Assembly changed during run.');
            result.Valid = result.Errors.length === 0;
        } catch (error) { result.Errors.push(error.message); }
        write(join(folder, 'result.json'), result); event('Finished', { Candidate: candidate.Id, Cohort: cohort, Seed: seed, Hero: hero, Valid: result.Valid, Outcome: result.Report?.Outcome });
        return result;
    }
    async function cohort(candidate, name) {
        const seeds = name === 'validation' ? config.ValidationSeeds : config.TrainingSeeds;
        const cases = seeds.flatMap(seed => config.Heroes.map(hero => ({ seed, hero }))), attempts = []; let next = 0;
        status('Running', { Candidate: candidate, Cohort: name, Expected: cases.length });
        await Promise.all(Array.from({ length: config.Workers }, async () => {
            while (next < cases.length && !requestedStop()) {
                const index = next++, c = cases[index], result = await oneCase(candidate, name, c.seed, c.hero, index + 1);
                if (result) attempts.push(result);
                status('Running', { Candidate: candidate, Cohort: name, Completed: attempts.length, Expected: cases.length,
                    Valid: attempts.filter(a => a.Valid).length, ActivePids: [...active].map(p => p.pid) });
                // Fix automation before collecting dozens of identical invalid runs.
                if (result && !result.Valid) { stopped = true; break; }
            }
        }));
        const folder = join(directory, candidate.Id, name); mkdirSync(folder, { recursive: true });
        const summary = saveSummary(folder, attempts, config.Targets, cases.length);
        return { attempts, summary };
    }
    try {
        let incumbent = { Id: 'baseline', ItemPrices: [], AbilityMana: [] };
        let best = await cohort(incumbent, 'training');
        const history = [{ Candidate: incumbent, Summary: best.summary }];
        const tried = new Set([identity({ ItemPrices:[],AbilityMana:[] })]);
        for (let iteration=1; iteration<config.MaxCandidates; iteration++) {
            if (requestedStop() || best.summary.Accepted || best.summary.Invalid > 0) break;
            const proposals = proposeCandidates(best.attempts,catalog,30).map(proposal=>{
                const merged={ItemPrices:structuredClone(incumbent.ItemPrices),AbilityMana:structuredClone(incumbent.AbilityMana)};
                for(const field of ['ItemPrices','AbilityMana']) for(const row of proposal[field]) {
                    const existing=merged[field].find(r=>r.Name===row.Name);
                    if(existing) existing.Multiplier=Math.min(existing.Multiplier,row.Multiplier);
                    else merged[field].push(row);
                    merged[field].sort((a,b)=>a.Name.localeCompare(b.Name));
                }
                return { ...merged, Reason:proposal.Reason };
            });
            const proposal=proposals.find(p=>!tried.has(identity({ItemPrices:p.ItemPrices,AbilityMana:p.AbilityMana})));
            if(!proposal) break;
            tried.add(identity({ItemPrices:proposal.ItemPrices,AbilityMana:proposal.AbilityMana}));
            const candidate={Id:`candidate-${String(iteration).padStart(2,'0')}`,...proposal};
            const trial = await cohort(candidate, 'training');
            const paired = pairedComparison(best.attempts, trial.attempts);
            const accepted = trial.summary.Invalid === 0 && paired.Pairs === best.attempts.length && paired.Pairs >= config.Targets.MinRuns &&
                paired.WinDelta >= 0 && paired.ProgressDelta >= 0 && paired.StageDelta >= 0 && trial.summary.Score > best.summary.Score &&
                best.summary.Heroes.every(h=>{const n=trial.summary.Heroes.find(n=>n.Hero===h.Hero);return n && n.Success>=h.Success;});
            history.push({ Candidate: candidate, Summary: trial.summary, Comparison: paired, Promoted: accepted });
            write(join(directory, 'history.json'), history);
            if (accepted) { incumbent = candidate; best = trial; }
        }
        if (requestedStop()) { status('Stopped', { Reason: 'Stop, resource bound, or invalid evidence. No balance acceptance.', History: history }); return; }
        if (!best.summary.Accepted) { status('NotConverged', { Reason: 'Bounded cost hypotheses did not meet the declared fairness targets.', BestCandidate: incumbent, History: history }); return; }
        // The holdout is used once. A failed holdout is not fed back into tuning.
        const validation = await cohort(incumbent, 'validation');
        const accepted = !requestedStop() && validation.summary.Accepted;
        status(accepted ? 'AcceptedForPolicy' : 'ValidationFailed', { BestCandidate: incumbent, Training: best.summary, Validation: validation.summary, History: history });
        write(join(directory, 'recommendation.json'), { Accepted: accepted, Scope: 'Declared cautious-player policy, sampled protagonists and seeds only.',
            Candidate: incumbent, ContentId: contentId, AssemblySha256: manifest.AssemblySha256, Targets: config.Targets,
            ApplyToAssets: false, Reason: accepted ? 'Training and held-out targets met.' : 'Held-out evidence did not meet targets.' });
    } catch(error) {
        status('AutomationError',{Reason:error.message}); throw error;
    } finally {
        for (const child of active) if (child.exitCode == null && child.signalCode == null) child.kill();
        process.off('SIGINT', stop); process.off('SIGTERM', stop);
    }
}

async function main() {
    const [mode, arg, extra] = process.argv.slice(2);
    if (mode === 'init') {
        const p = resolve(arg ?? 'Tools/balance-config.json');
        if (existsSync(p)) throw Error('Config already exists.');
        write(p, defaultConfig(read(join(root, 'Logs/Balance/catalog.json')))); console.log(p);
    } else if (mode === 'prepare-build') {
        write(join(root,'Logs/Balance/build-inputs.json'),{ContentId:sourceFingerprint(),StartedUtc:new Date().toISOString()});
    } else if (mode === 'stamp-build') {
        const assembly = join(root, 'Builds/DebugAutoplay/DebugAutoplay_Data/Managed/EternalEnigma.Game.dll');
        const result=join(root,'Builds/DebugAutoplay/balance-build-result.txt');
        if(!existsSync(result)||!readFileSync(result,'utf8').startsWith('Succeeded')) throw Error('A successful current development build is required.');
        const inputs=read(join(root,'Logs/Balance/build-inputs.json'));
        if(inputs.ContentId!==sourceFingerprint() || statSync(result).mtimeMs<Date.parse(inputs.StartedUtc)) throw Error('Prepare the build inputs, build without changing gameplay content, then stamp the completed build.');
        write(join(root, 'Builds/DebugAutoplay/balance-build.json'), { ContentId: sourceFingerprint(), AssemblySha256: sha(assembly), BuiltUtc: new Date().toISOString() });
    } else if (mode === 'run') await runExperiment(resolve(arg), resolve(extra ?? 'Tools/balance-config.json'));
    else if (mode === 'status') console.log(JSON.stringify(read(join(resolve(arg), 'status.json')), null, 2));
    else if (mode === 'stop') write(join(resolve(arg), 'stop.request'), { RequestedUtc: new Date().toISOString() });
    else if (mode === 'inspect') {
        const report = read(resolve(arg));
        console.log(JSON.stringify({ Outcome: report.Outcome, Reason: report.Reason, Seed: report.Seed, Actions: report.Actions, Balance: report.Balance, Purchases: report.Purchases, Training: report.Training }, null, 2));
    } else throw Error('Usage: balance-runner.mjs init [config] | prepare-build | stamp-build | run <directory> [config] | status|stop <directory> | inspect <report.json>');
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) main().catch(error => { console.error(error.stack); process.exitCode = 1; });
