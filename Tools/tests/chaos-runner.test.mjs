import test from 'node:test';
import assert from 'node:assert/strict';
import { heartbeatFailure, summarize, contained, verify, matchesLaunch } from '../chaos-runner.mjs';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

test('watchdog distinguishes startup, frozen heartbeat, wrong process, and fresh heartbeat', () => {
    const now = 1000000;
    assert.equal(heartbeatFailure(null, now, now - 1000, 75, 180, 7), null);
    assert.match(heartbeatFailure(null, now, now - 181000, 75, 180, 7), /startup/);
    assert.equal(heartbeatFailure({ Utc: new Date(now - 1000).toISOString(), ProcessId: 7 }, now, 0, 75, 180, 7), null);
    assert.match(heartbeatFailure({ Utc: new Date(now - 76000).toISOString(), ProcessId: 7 }, now, 0, 75, 180, 7), /stopped/);
    assert.match(heartbeatFailure({ Utc: new Date(now).toISOString(), ProcessId: 8 }, now, 0, 75, 180, 7), /different process/);
});
test('artifact paths cannot escape the launch directory', () => {
    assert.throws(() => contained(tmpdir(), '../escape'), /escapes/);
    assert.equal(contained(tmpdir(), 'valid'), join(tmpdir(), 'valid'));
});
test('MCP reconnect recovery requires a fresh report with exactly the requested options', () => {
    const options = { Seed: 42, ReportRoot: 'Logs/Chaos', Mode: 0 };
    const report = { StartedUtc: new Date(100000).toISOString(), Options: { ...options, Device: 0 } };
    assert.equal(matchesLaunch(report, options, 100000), true);
    assert.equal(matchesLaunch(report, options, 102000), false);
    assert.equal(matchesLaunch(report, { ...options, Seed: 43 }, 100000), false);
    assert.equal(matchesLaunch({ ...report, StartedUtc: '' }, options, 100000), false);
});
test('summary keeps demo, participation, and ability coverage independent', () => {
    const summary = summarize({ Coverage: { Cases: [{ Demo: false, Passed: false }, { Demo: true, Passed: true }], Participation: [{ Id: 'hero', Exercised: 0 }] } });
    assert.equal(summary.requiredCases, 1); assert.equal(summary.passedCases, 0); assert.equal(summary.demoCases, 1);
    assert.equal(summary.unexercisedParticipants.length, 1);
});
test('verifier rejects declared passes without artifacts or actual coverage', () => {
    const directory = mkdtempSync(join(tmpdir(), 'chaos-verifier-'));
    try {
        writeFileSync(join(directory, 'report.json'), JSON.stringify({ Schema: 1, FixtureVersion: '1', State: 'Completed', ArtifactsComplete: true,
            FullPassed: true, SmokePassed: true, Inputs: 0, Options: { DurationSeconds: 300 }, FixtureHashes: [], Coverage: { Cases: [], Participation: [] } }));
        const result = verify(directory, true);
        assert.equal(result.passed, false); assert.ok(result.errors.some(e => e.includes('Missing artifact')));
        assert.ok(result.errors.some(e => e.includes('participation incomplete')));
    } finally { rmSync(contained(tmpdir(), directory), { recursive: true, force: true }); }
});
