#!/usr/bin/env node
import { readFileSync, writeFileSync, mkdirSync, readdirSync, existsSync, statSync } from 'node:fs';
import { resolve, join, dirname, relative, isAbsolute } from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const json = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const lines = path => readFileSync(path, 'utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse);
const write = (path, value) => { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, JSON.stringify(value, null, 2) + '\n'); };
const defaultRoot = join(root, 'Logs/Chaos');

export function contained(base, path) {
    const target = resolve(base, path), rel = relative(resolve(base), target);
    if (rel.startsWith('..') || isAbsolute(rel)) throw Error('Artifact escapes report directory: ' + path);
    return target;
}
export function heartbeatFailure(heartbeat, now, launchedAt, timeoutSeconds, startupSeconds, expectedPid) {
    if (!heartbeat) return now - launchedAt > startupSeconds * 1000 ? 'No heartbeat before startup deadline' : null;
    if (heartbeat.ProcessId !== expectedPid) return 'Heartbeat belongs to a different process';
    const stamp = Date.parse(heartbeat.Utc);
    if (!Number.isFinite(stamp) || stamp > now + 5000) return 'Invalid heartbeat timestamp';
    return now - stamp > timeoutSeconds * 1000 ? 'Player heartbeat stopped' : null;
}
export function heartbeatMonitor(launchedAt, timeoutSeconds, startupSeconds, expectedPid) {
    let lastHeartbeat;
    return (readHeartbeat, now) => {
        let heartbeat, readError;
        try { heartbeat = readHeartbeat(); }
        catch (error) { readError = error.message; }
        // A Windows sharing violation or an interrupted read is not a stalled player.
        // Keep the last validated timestamp, so repeated read failures still time out.
        const reason = heartbeatFailure(heartbeat ?? lastHeartbeat, now, launchedAt,
            timeoutSeconds, startupSeconds, expectedPid);
        if (heartbeat && !reason) lastHeartbeat = heartbeat;
        return { reason, readError };
    };
}
export function summarize(report) {
    const cases = report.Coverage?.Cases ?? [], required = cases.filter(c => !c.Demo);
    return { state: report.State, reason: report.Reason, seconds: report.Seconds, inputs: report.Inputs,
        artifactsComplete: report.ArtifactsComplete, smokePassed: report.SmokePassed, fullPassed: report.FullPassed,
        passedCases: required.filter(c => c.Passed).length, requiredCases: required.length,
        demoCases: cases.filter(c => c.Demo).length, unexercisedParticipants: report.Coverage?.Participation?.filter(p => p.Exercised < 1) ?? [],
        issues: (report.Issues ?? []).map(({ Kind, Message, Seconds, Screenshot }) => ({ Kind, Message, Seconds, Screenshot })) };
}
export function verify(directory, full = false) {
    const report = json(join(directory, 'report.json')), errors = [];
    if (report.Schema !== 1 || report.FixtureVersion !== '1') errors.push('Unsupported report/fixture version');
    if (report.State !== 'Completed' || !report.ArtifactsComplete || report.ReplayDiverged) errors.push('Incomplete or divergent run');
    if (!report.BuildId || !report.ContentId) errors.push('Missing build/content identity');
    if (!report.Artifacts?.length) errors.push('Missing artifact hashes');
    for (const artifact of report.Artifacts ?? []) {
        try {
            const path = contained(directory, artifact.Path), bytes = readFileSync(path);
            if (bytes.length !== artifact.Bytes || createHash('sha256').update(bytes).digest('hex') !== artifact.Sha256) errors.push('Artifact changed: ' + artifact.Path);
        } catch (error) { errors.push(error.message); }
    }
    if ((report.Issues ?? []).length) errors.push('Run has recorded issues');
    for (const name of ['final.png', 'save.json', 'logs.jsonl'])
        if (!existsSync(join(directory, name)) || (name !== 'logs.jsonl' && statSync(join(directory, name)).size === 0)) errors.push('Missing artifact: ' + name);
    if (existsSync(join(directory, 'logs.jsonl')) && lines(join(directory, 'logs.jsonl')).some(l => ['Error', 'Exception', 'Assert'].includes(l.Kind))) errors.push('Unity errors in log');
    const segments = readdirSync(directory, { withFileTypes: true }).filter(e => e.isDirectory() && e.name.startsWith('scenario-')).map(e => join(directory, e.name));
    const resumedRoot = join(directory, 'resumed');
    const resumed = existsSync(resumedRoot) ? readdirSync(resumedRoot, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => join(resumedRoot, e.name)) : [];
    if (!segments.length && !resumed.length) errors.push('No executed or resumed fixtures');
    for (const source of resumed) {
        try {
            const completed = json(join(source, 'case-result.json'));
            if (completed.State !== 'Completed' || !completed.ArtifactsComplete || completed.Schema !== report.Schema ||
                completed.BuildId !== report.BuildId || completed.ContentId !== report.ContentId ||
                completed.FixtureVersion !== report.FixtureVersion || completed.ScenarioSetId !== report.ScenarioSetId)
                errors.push('Invalid resumed case identity: ' + source);
            for (const artifact of completed.Artifacts ?? []) {
                const bytes = readFileSync(contained(source, artifact.Path));
                if (bytes.length !== artifact.Bytes || createHash('sha256').update(bytes).digest('hex') !== artifact.Sha256)
                    errors.push('Resumed artifact changed: ' + artifact.Path);
            }
            if (!completed.Artifacts?.length) errors.push('Unsealed resumed case: ' + source);
        } catch (error) { errors.push(error.message); }
    }
    let inputCount = 0;
    const hashes = [];
    for (const segment of segments) {
        try {
            const fixture = json(join(segment, 'fixture.json')); hashes.push(fixture.Hash);
            if (fixture.Schema !== 1 || !fixture.Hash || !fixture.SaveJson) errors.push('Invalid fixture: ' + segment);
            const save = JSON.parse(fixture.SaveJson);
            if (JSON.stringify(save.TownSaveData.RecruitedAlliesData) !== JSON.stringify(fixture.Party) ||
                JSON.stringify(save.TownSaveData.InventoryItems) !== JSON.stringify(fixture.Inventory)) errors.push('Fixture/save disagreement: ' + segment);
            if (new Set(fixture.Party.map(h => h.AllyId)).size !== fixture.Party.length || fixture.Party.length < 1 || fixture.Inventory.length > 10) errors.push('Illegal party/inventory: ' + segment);
            const trace = lines(join(segment, 'inputs.jsonl')), checkpoints = lines(join(segment, 'checkpoints.jsonl'));
            inputCount += trace.length;
            if (trace.length !== checkpoints.length) errors.push('Unfinished input checkpoint: ' + segment);
            for (let i = 0; i < trace.length; i++)
                if (trace[i].Index !== i || checkpoints[i]?.Index !== i || !trace[i].Before || !checkpoints[i]?.Hash) errors.push('Invalid trace index/hash: ' + segment);
        } catch (error) { errors.push(error.message); }
    }
    if (inputCount !== report.Inputs || inputCount < 1 && !resumed.length) errors.push('Input accounting mismatch');
    if (JSON.stringify(hashes) !== JSON.stringify(report.FixtureHashes)) errors.push('Fixture manifest mismatch');
    const cases = report.Coverage?.Cases ?? [];
    if (!cases.length || new Set(cases.map(c => c.Id)).size !== cases.length) errors.push('Missing/duplicate coverage checklist');
    if (full) {
        if (!report.FullPassed || cases.some(c => !c.Demo && !c.Passed)) errors.push('Required feature/ability cases remain unverified');
        const participants = report.Coverage?.Participation ?? [];
        if (!participants.length || participants.some(p => p.Exercised < 1)) errors.push('Hero/class participation incomplete');
    } else {
        if (!report.SmokePassed) errors.push('Smoke did not pass');
        if (report.Options.DurationSeconds < 300 || report.Seconds < 300) errors.push('Smoke shorter than five minutes');
    }
    return { passed: errors.length === 0, errors, summary: summarize(report) };
}
function findReport(path = defaultRoot) {
    path = resolve(path);
    if (existsSync(join(path, 'report.json'))) return path;
    const location = json(join(path, 'latest.json'));
    return contained(path, location.Directory);
}
function mcp(tool, args) {
    const path = join(root, 'Temp/Chaos/mcp-cli.json'); write(path, args);
    const result = spawnSync(process.execPath, [join(root, 'Tools/unity-mcp.mjs'), tool, '@' + path], { cwd: root, encoding: 'utf8', windowsHide: true });
    if (result.status !== 0) throw Error(result.stderr || result.stdout);
    process.stdout.write(result.stdout);
}
export function matchesLaunch(report, options, startedAt) {
    return Number.isFinite(Date.parse(report.StartedUtc)) && Date.parse(report.StartedUtc) >= startedAt - 1000 &&
        Object.entries(options).every(([key, value]) => JSON.stringify(report.Options?.[key]) === JSON.stringify(value));
}
async function editor(options) {
    const reportRoot = resolve(options.ReportRoot || defaultRoot);
    let previous;
    try { previous = findReport(reportRoot); } catch { }
    write(join(root, 'Temp/Chaos/options.json'), options);
    const startedAt = Date.now();
    let connectionError;
    try { mcp('execute_menu_item', { menuPath: 'Tools/Eternal Enigma/Chaos/Run From File' }); }
    catch (error) { connectionError = error; }
    // Entering Play Mode reloads the MCP socket. A menu receipt alone does not prove startup succeeded.
    for (let attempt = 0; attempt < 30; attempt++) {
        try {
            const directory = findReport(reportRoot);
            if (directory !== previous && matchesLaunch(json(join(directory, 'report.json')), options, startedAt)) {
                console.log('Editor launch confirmed from fresh report' + (connectionError ? ' after MCP reconnect' : '') + ': ' + directory); return;
            }
        } catch { }
        await new Promise(resolveWait => setTimeout(resolveWait, 1000));
    }
    throw connectionError ?? Error('Unity did not create a fresh chaos report. Check its console and Play Mode state before retrying.');
}
async function player(executable, optionsPath, timeoutSeconds = 75) {
    const options = json(optionsPath);
    if (!options.ReportRoot) throw Error('Development-player options require an explicit ReportRoot.');
    options.ReportRoot = resolve(options.ReportRoot);
    options.QuitWhenFinished = true;
    // One launch root per player prevents a stale or concurrent latest.json from being mistaken for this process.
    options.ReportRoot = join(options.ReportRoot, 'player-' + Date.now());
    mkdirSync(options.ReportRoot, { recursive: true });
    const launchOptions = join(options.ReportRoot, 'options.json'); write(launchOptions, options);
    const launchedAt = Date.now();
    const child = spawn(resolve(executable), ['--chaos-options', launchOptions, '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
        '-logFile', join(options.ReportRoot, 'player.log')], { cwd: root, windowsHide: true, stdio: 'ignore' });
    console.log('Launched chaos player ' + child.pid + ': ' + options.ReportRoot);
    let directory, finished = false, failed = false;
    const monitor = heartbeatMonitor(launchedAt, timeoutSeconds, 180, child.pid);
    const timer = setInterval(() => {
        if (finished) return;
        const { reason, readError } = monitor(() => {
            if (existsSync(join(options.ReportRoot, 'latest.json'))) {
                directory = findReport(options.ReportRoot);
                if (existsSync(join(directory, 'heartbeat.json'))) return json(join(directory, 'heartbeat.json'));
            }
        }, Date.now());
        if (reason) {
            failed = true;
            try {
                write(join(options.ReportRoot, 'watchdog.json'), { State: 'WatchdogFailure', Reason: reason,
                    ReadError: readError, ProcessId: child.pid, Utc: new Date().toISOString(), Directory: directory });
            } catch (error) { console.error('Unable to write watchdog report: ' + error.message); }
            child.kill(); console.error(reason + (readError ? ': ' + readError : '')); clearInterval(timer);
        }
    }, 1000);
    const exit = await new Promise((resolveExit, reject) => { child.on('error', reject); child.on('exit', code => resolveExit(code)); }).finally(() => { finished = true; clearInterval(timer); });
    if (!directory && existsSync(join(options.ReportRoot, 'latest.json'))) directory = findReport(options.ReportRoot);
    const reportPath = directory && join(directory, 'report.json');
    if (reportPath && existsSync(reportPath)) {
        const report = json(reportPath);
        console.log(JSON.stringify(summarize(report), null, 2));
        if (report.State !== 'Completed' || !report.ArtifactsComplete || !(report.Options.Mode === 1 ? report.FullPassed : report.SmokePassed)) failed = true;
    } else {
        failed = true;
        write(join(options.ReportRoot, 'watchdog.json'), { State: 'PlayerExitedWithoutReport', ExitCode: exit, ProcessId: child.pid, Utc: new Date().toISOString() });
        console.error('Player exited without a finalized chaos report.');
    }
    if (failed || exit !== 0) process.exitCode = 1;
}
async function main(args) {
    const [command, path, extra] = args;
    if (command === 'init') {
        const file = resolve(path ?? 'Temp/Chaos/options.json');
        if (existsSync(file)) throw Error('Options already exist: ' + file);
        write(file, { Seed: 42, RunCount: 1, DurationSeconds: 300, ActionTimeoutSeconds: 10, LoadTimeoutSeconds: 60, RecoverySeconds: 10,
            Mode: 0, Device: 0, ReportRoot: defaultRoot, ScenarioDirectory: '', ScenarioFilter: '', AbilityFilter: '', QuitWhenFinished: true }); console.log(file);
    } else if (command === 'editor') {
        await editor(json(resolve(path ?? 'Temp/Chaos/options.json')));
    } else if (command === 'build') mcp('execute_menu_item', { menuPath: 'Tools/Eternal Enigma/Chaos/Build Windows Development Player' });
    else if (command === 'stop') mcp('execute_menu_item', { menuPath: 'Tools/Eternal Enigma/Chaos/Stop and Report' });
    else if (command === 'player') await player(path ?? 'Builds/Chaos/EternalEnigma-Chaos.exe', resolve(extra ?? 'Temp/Chaos/options.json'), Number(args[3] ?? 75));
    else if (command === 'status') {
        const directory = findReport(path), report = json(join(directory, 'report.json'));
        if (report.State === 'Running' && existsSync(join(directory, 'heartbeat.json'))) {
            const heartbeat = json(join(directory, 'heartbeat.json'));
            report.Seconds = heartbeat.Seconds; report.Inputs = heartbeat.Inputs;
        }
        console.log(directory); console.log(JSON.stringify(summarize(report), null, 2));
    }
    else if (command === 'verify') { const result = verify(findReport(path), args.includes('--full')); console.log(JSON.stringify(result, null, 2)); if (!result.passed) process.exitCode = 1; }
    else if (command === 'checklist') {
        const report = json(join(findReport(path), 'report.json'));
        console.log(JSON.stringify(extra ? report.Coverage.Cases.filter(c => c.Id.includes(extra)) : report.Coverage, null, 2));
    } else if (command === 'replay' || command === 'resume') {
        const directory = findReport(path), previous = json(join(directory, 'report.json'));
        const options = { ...previous.Options, ReportRoot: defaultRoot, ReplayDirectory: '', ResumeDirectory: '', [command === 'replay' ? 'ReplayDirectory' : 'ResumeDirectory']: directory };
        const output = resolve(extra ?? `Temp/Chaos/${command}-options.json`); write(output, options); console.log(output);
        if (args.includes('--editor')) await editor(options);
    } else throw Error('Usage: chaos-runner.mjs init [options] | editor [options] | build | player [exe] [options] [watchdog-seconds] | stop | status [report/root] | verify [report/root] [--full] | checklist [report/root] | replay|resume <report> [output-options] [--editor]');
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
    main(process.argv.slice(2)).catch(error => { console.error(error.stack ?? error.message); process.exitCode = 1; });
