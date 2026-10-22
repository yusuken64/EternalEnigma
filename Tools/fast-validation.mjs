// Repeated production autoplay with independently isolated, randomly scheduled chaos players.
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync, appendFileSync, openSync, closeSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { verify as verifyChaos, contained } from './chaos-runner.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const read = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const write = (path, value) => { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, JSON.stringify(value, null, 2) + '\n'); };
export function scheduleRandom(seed) {
    let state = seed >>> 0 || 0x9e3779b9;
    return (min, max) => {
        if (!Number.isSafeInteger(min) || !Number.isSafeInteger(max) || max < min || max - min > 0xffffffff)
            throw Error('Invalid random interval.');
        state ^= state << 13; state ^= state >>> 17; state ^= state << 5;
        return min + (state >>> 0) % (max - min + 1);
    };
}
export function nextChaosAt(random, now, initial = false) {
    return now + (initial ? random(60, 120) : random(120, 360)) * 1000;
}
export function campaignPassed(report, audit, expectedSeed) {
    const options = report?.Options;
    return report?.Outcome === 'Victory' && report.ArtifactsComplete === true && report.IssueCount === 0 &&
        report.WarningCount === 0 && report.ErrorCount === 0 && report.Campaign?.Finished === true &&
        options?.Mode === 0 && options.Speed === 32 && options.NoAnimations === true &&
        options.DebugPlaythrough === true && options.Godmode === true && options.InfiniteStrength === true &&
        options.InfiniteResources === true && options.DebugCoverage === true && options.ExploreAll === false &&
        options.RunCount === 1 && Number.isInteger(expectedSeed) &&
        report.Seed === expectedSeed && options.Seed === expectedSeed && audit === 0;
}
export function campaignOptions(seed, reportRoot) {
    return { Mode: 0, Seed: seed, RunCount: 1, Speed: 32, NoAnimations: true,
        DebugPlaythrough: true, Godmode: true, InfiniteStrength: true, InfiniteResources: true,
        DebugCoverage: true, ExploreAll: false, ActionLimit: 100000, TimeLimitMinutes: 60,
        StallSeconds: 300, ReportRoot: reportRoot };
}
export function chaosOptions(seed, device = 0) {
    return { Seed: seed, RunCount: 1, DurationSeconds: 300, ActionTimeoutSeconds: 15,
        LoadTimeoutSeconds: 120, RecoverySeconds: 10, Mode: 0, Device: device,
        ScenarioDirectory: '', ScenarioFilter: '', AbilityFilter: '', QuitWhenFinished: true };
}
export function validateRunConfig(count, workers, startingSeed) {
    if (!Number.isSafeInteger(count) || count < 1 || !Number.isInteger(workers) || workers < 1 || workers > 2 ||
        !Number.isInteger(startingSeed) || startingSeed < -2147483648 || startingSeed + count - 1 > 2147483647)
        throw Error('Invalid count, workers, or seed.');
}
export const fileSha256 = path => createHash('sha256').update(readFileSync(path)).digest('hex');
export function suitePassed(results, chaos, count, startingSeed, expectedAssemblySha256, stopped = false) {
    const seeds = new Set(results.map(result => result.Seed));
    // Autoplay uses the assembly MVID; chaos uses Application.buildGUID in players.
    // Their identities are stable within each family but are deliberately not comparable across families.
    const oneBuild = runs => runs.length > 0 && typeof runs[0].BuildId === 'string' && runs[0].BuildId.length > 0 &&
        runs.every(result => result.BuildId === runs[0].BuildId);
    const oneAssembly = typeof expectedAssemblySha256 === 'string' && /^[0-9a-f]{64}$/.test(expectedAssemblySha256) &&
        [...results, ...chaos].every(result => result.AssemblySha256 === expectedAssemblySha256 &&
            result.FinishedAssemblySha256 === expectedAssemblySha256);
    return !stopped && results.length === count && seeds.size === count &&
        results.every(result => result.Passed && !result.TimedOut && Number.isInteger(result.Seed) &&
            result.Seed >= startingSeed && result.Seed < startingSeed + count) &&
        chaos.length > 0 && chaos.every(result => result.Passed && !result.TimedOut) &&
        oneBuild(results) && oneBuild(chaos) && oneAssembly;
}
export function ownedProcessId(job) {
    return !job.finished && Number.isInteger(job.child?.pid) && job.child.pid > 0 &&
        job.child.exitCode == null && job.child.signalCode == null ? job.child.pid : null;
}
const directories = directory => existsSync(directory) ? readdirSync(directory, { withFileTypes: true })
    .filter(entry => entry.isDirectory()).map(entry => entry.name).sort().reverse() : [];
export function campaignReport(directory) {
    const batches = join(directory, 'Playthroughs');
    for (const batch of directories(batches)) {
        const batchPath = join(batches, batch);
        for (const run of directories(batchPath).filter(x => x.startsWith('run-'))) {
            const path = join(batchPath, run, 'report.json');
            try { return { path, batch: batchPath, report: read(path) }; } catch { }
        }
    }
    return null;
}
export function chaosReport(directory) {
    for (const launch of directories(directory).filter(x => x.startsWith('player-'))) {
        try {
            const launchRoot = join(directory, launch), latest = read(join(launchRoot, 'latest.json'));
            const reportRoot = contained(launchRoot, latest.Directory), path = join(reportRoot, 'report.json');
            const report = read(path);
            let heartbeat = null;
            if (report.State === 'Running') {
                try { heartbeat = read(join(reportRoot, 'heartbeat.json')); } catch { }
            }
            return { directory: reportRoot, report, heartbeat };
        } catch { }
    }
    return null;
}

async function run(directory, count = 20, workers = 2, startingSeed = 42) {
    validateRunConfig(count, workers, startingSeed);
    const executable = join(root, 'Builds/DebugAutoplay/DebugAutoplay.exe');
    const assembly = join(root, 'Builds/DebugAutoplay/DebugAutoplay_Data/Managed/EternalEnigma.Game.dll');
    if (!existsSync(executable) || !existsSync(assembly)) throw Error('Build the current development player first.');
    if (existsSync(join(directory, 'manifest.json'))) throw Error('Use a new directory; existing attempts are preserved.');
    mkdirSync(directory, { recursive: true });
    const seed = randomBytes(4).readUInt32LE(), random = scheduleRandom(seed);
    const manifest = { StartedUtc: new Date().toISOString(), Count: count, Workers: workers, StartingSeed: startingSeed,
        ScheduleSeed: seed, Executable: executable, Assembly: assembly, AssemblySha256: fileSha256(assembly),
        ChaosDurationSeconds: 300, ChaosGapSeconds: [120, 360], ProcessId: process.pid };
    writeFileSync(join(directory, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', { flag: 'wx' });
    const active = new Set(), results = [], chaos = [];
    let nextSeed = startingSeed, chaosIndex = 0, nextChaos = nextChaosAt(random, Date.now(), true);
    let stop = false, stopping = false;
    const stopRequested = () => { stop = true; };
    process.on('SIGINT', stopRequested);
    process.on('SIGTERM', stopRequested);
    const event = (kind, data) => appendFileSync(join(directory, 'events.jsonl'), JSON.stringify({ Utc: new Date().toISOString(), Kind: kind, ...data }) + '\n');
    function launch(kind, id, options) {
        const assemblySha256 = fileSha256(assembly);
        if (assemblySha256 !== manifest.AssemblySha256) {
            event('BuildChanged', { KindOfRun: kind, Id: id, ExpectedAssemblySha256: manifest.AssemblySha256, AssemblySha256: assemblySha256 });
            throw Error('Player assembly changed since suite creation; refusing to launch another player.');
        }
        const folder = join(directory, `${kind}-${String(id).padStart(4, '0')}`);
        mkdirSync(folder);
        options.ReportRoot = folder;
        const optionsPath = join(folder, 'options.json'); write(optionsPath, options);
        const stdout = openSync(join(folder, 'driver.log'), 'a');
        let args, command;
        if (kind === 'campaign') {
            command = executable;
            args = ['--debug-autoplay-options', optionsPath, '-screen-fullscreen', '0', '-screen-width', '640', '-screen-height', '360',
                '-logFile', join(folder, 'player.log')];
        } else {
            command = process.execPath;
            args = [join(root, 'Tools/chaos-runner.mjs'), 'player', executable, optionsPath, '180'];
        }
        let child;
        try {
            child = spawn(command, args, { cwd: root, windowsHide: true, detached: process.platform !== 'win32',
                stdio: ['ignore', stdout, stdout] });
        } finally { closeSync(stdout); }
        const job = { kind, id, seed: options.Seed, folder, child, assemblySha256, started: Date.now(), finished: false };
        active.add(job);
        const finish = (code, error) => {
            if (job.finished) return; job.finished = true;
            const result = { Id: id, Seed: job.seed, Directory: folder, ExitCode: code, Seconds: (Date.now() - job.started) / 1000,
                Passed: false, Reason: error?.message ?? '', TimedOut: !!job.timedOut,
                AssemblySha256: job.assemblySha256, FinishedAssemblySha256: null };
            try { if (kind === 'campaign') {
                const found = campaignReport(folder);
                if (found) {
                    const audit = spawnSync(process.execPath, [join(root, 'Tools/debug-autoplay.mjs'), 'verify-run', found.batch],
                        { cwd: root, windowsHide: true, encoding: 'utf8', timeout: 60000, maxBuffer: 8 * 1024 * 1024 });
                    writeFileSync(join(folder, 'audit.txt'), (audit.stdout ?? '') + (audit.stderr ?? ''));
                    result.Outcome = found.report.Outcome; result.Actions = found.report.Actions; result.Issues = found.report.IssueCount;
                    result.BuildId = found.report.BuildId;
                    result.Reason ||= audit.error?.message;
                    result.Reason ||= found.report.Reason;
                    result.Passed = code === 0 && !job.timedOut && campaignPassed(found.report, audit.status, job.seed);
                } else result.Reason ||= 'No campaign report';
            } else {
                const found = chaosReport(folder);
                if (found) {
                    try {
                        const audit = verifyChaos(found.directory); write(join(folder, 'audit.json'), audit);
                        result.BuildId = found.report.BuildId;
                        result.Passed = code === 0 && !job.timedOut && found.report.Seed === job.seed && audit.passed;
                        result.Issues = found.report.Issues?.length;
                        result.Reason ||= found.report.Reason || audit.errors.join('; ');
                    } catch (failure) { result.Reason ||= failure.message; }
                } else result.Reason ||= 'No chaos report';
            } } catch (failure) { result.Passed = false; result.Reason ||= failure.message; }
            try {
                result.FinishedAssemblySha256 = fileSha256(assembly);
                if (result.FinishedAssemblySha256 !== manifest.AssemblySha256) {
                    result.Passed = false; stop = true;
                    result.Reason = 'Player assembly changed during the run; build evidence cannot be combined.';
                }
            } catch (failure) {
                result.Passed = false; stop = true;
                result.Reason = 'Cannot verify player assembly after run: ' + failure.message;
            }
            active.delete(job);
            (kind === 'campaign' ? results : chaos).push(result);
            try {
                if (kind === 'chaos') {
                    nextChaos = nextChaosAt(random, Date.now());
                    event('ChaosScheduled', { AtUtc: new Date(nextChaos).toISOString() });
                }
                write(join(folder, 'result.json'), result); event('Finished', { KindOfRun: kind, ...result });
            } catch (failure) {
                result.Passed = false; stop = true;
                console.error('Unable to record finalized run:', failure.message);
            }
        };
        child.on('error', error => finish(null, error));
        child.on('exit', code => finish(code));
        event('Started', { KindOfRun: kind, Id: id, Seed: job.seed, Pid: child.pid, Directory: folder,
            AssemblySha256: job.assemblySha256 });
        return job;
    }
    const killJob = job => {
        // Kill only this supervisor's own child tree (chaos CLI owns its player).
        const pid = active.has(job) ? ownedProcessId(job) : null;
        if (pid === null) return;
        if (job.killRequestedAt && Date.now() - job.killRequestedAt < 10000) return;
        job.killRequestedAt = Date.now();
        if (process.platform === 'win32') {
            const killed = spawnSync('taskkill', ['/PID', String(pid), '/T', '/F'],
                { windowsHide: true, stdio: 'ignore', timeout: 10000 });
            if (killed.error) console.error('Unable to terminate owned process tree:', pid, killed.error.message);
        }
        else { try { process.kill(-pid, 'SIGKILL'); } catch (error) { if (error.code !== 'ESRCH') throw error; } }
    };
    try {
    event('ChaosScheduled', { AtUtc: new Date(nextChaos).toISOString() });
    while (true) {
        if (existsSync(join(directory, 'stop.request'))) stop = true;
        if (stop && !stopping) { stopping = true; event('StopRequested', {}); for (const job of active) killJob(job); }
        if (!stop) {
            while ([...active].filter(j => j.kind === 'campaign').length < workers && nextSeed < startingSeed + count)
                launch('campaign', nextSeed - startingSeed + 1, campaignOptions(nextSeed++, ''));
            if (![...active].some(j => j.kind === 'chaos') && Date.now() >= nextChaos && (results.length < count || chaos.length === 0))
                launch('chaos', ++chaosIndex, chaosOptions(random(1, 2147483646), (chaosIndex - 1) % 4));
        }
        for (const job of active) {
            const limit = job.kind === 'campaign' ? 70 * 60 : 20 * 60;
            if (!job.timedOut && Date.now() - job.started > limit * 1000) {
                job.timedOut = true; event('WatchdogTimeout', { KindOfRun: job.kind, Id: job.id }); killJob(job);
            }
            if (stopping || job.timedOut) killJob(job);
        }
        const live = [...active].map(job => {
            const found = job.kind === 'campaign' ? campaignReport(job.folder) : chaosReport(job.folder);
            return { Kind: job.kind, Id: job.id, Seed: job.seed, Pid: job.child.pid, Directory: job.folder,
                Seconds: (Date.now() - job.started) / 1000, Outcome: found?.report.Outcome ?? found?.report.State,
                Actions: found?.report.Actions ?? found?.heartbeat?.Inputs ?? found?.report.Inputs, Location: found?.report.Location,
                Floor: found?.report.Floor, Issues: found?.report.IssueCount ?? found?.report.Issues?.length };
        });
        const done = active.size === 0 && (stop || results.length === count && chaos.length > 0);
        const state = done ? stop ? 'Stopped' : 'Completed' : stopping ? 'Stopping' : 'Running';
        write(join(directory, 'status.json'), { State: state,
            Passed: done && suitePassed(results, chaos, count, startingSeed, manifest.AssemblySha256, stop),
            UpdatedUtc: new Date().toISOString(), ProcessId: process.pid,
            RequestedCampaigns: count, CompletedCampaigns: results.length, PassedCampaigns: results.filter(r => r.Passed).length,
            CompletedChaos: chaos.length, PassedChaos: chaos.filter(r => r.Passed).length,
            NextChaosUtc: new Date(nextChaos).toISOString(), Active: live, Campaigns: results, Chaos: chaos });
        if (done) break;
        await new Promise(resolveWait => setTimeout(resolveWait, 5000));
    }
    process.exitCode = suitePassed(results, chaos, count, startingSeed, manifest.AssemblySha256, stop) ? 0 : 1;
    } catch (failure) {
        stop = true;
        write(join(directory, 'status.json'), { State: 'Failed', Passed: false, Reason: failure.message,
            UpdatedUtc: new Date().toISOString(), ProcessId: process.pid,
            RequestedCampaigns: count, CompletedCampaigns: results.length, CompletedChaos: chaos.length,
            Campaigns: results, Chaos: chaos });
        throw failure;
    } finally {
        for (const job of active) killJob(job);
        process.off('SIGINT', stopRequested); process.off('SIGTERM', stopRequested);
    }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    const [command, path, count, workers, seed] = process.argv.slice(2);
    if (!path) throw Error('Usage: node Tools/fast-validation.mjs run|status|stop <report-directory> [campaigns=20] [workers=2] [seed=42]');
    const directory = resolve(path);
    if (command === 'run') await run(directory, Number(count ?? 20), Number(workers ?? 2), Number(seed ?? 42));
    else if (command === 'status') console.log(JSON.stringify(read(join(directory, 'status.json')), null, 2));
    else if (command === 'stop') { if (!existsSync(join(directory, 'manifest.json'))) throw Error('No suite at that path.'); writeFileSync(join(directory, 'stop.request'), new Date().toISOString()); }
    else throw Error('Unknown command: ' + command);
}
