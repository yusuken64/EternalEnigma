import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { join, dirname, relative, isAbsolute } from 'node:path';
import { tmpdir } from 'node:os';
import { scheduleRandom, nextChaosAt, campaignOptions, chaosOptions, validateRunConfig,
    campaignPassed, suitePassed, fileSha256, ownedProcessId, campaignReport, chaosReport } from '../fast-validation.mjs';

test('seeded schedule is reproducible, bounded, and rejects invalid intervals', () => {
    const first = scheduleRandom(42), second = scheduleRandom(42);
    assert.deepEqual(Array.from({ length: 5 }, () => first(0, 0xffffffff)),
        [11355432, 2836018348, 476557059, 3648046016, 3759983556]);
    assert.deepEqual(Array.from({ length: 5 }, () => second(0, 0xffffffff)),
        [11355432, 2836018348, 476557059, 3648046016, 3759983556]);
    const draws = Array.from({ length: 2000 }, () => first(120, 360));
    assert.ok(draws.every(draw => Number.isInteger(draw) && draw >= 120 && draw <= 360));
    assert.ok(draws.includes(120) && draws.includes(360));
    assert.equal(first(7, 7), 7);
    for (const interval of [[3, 2], [0.5, 1], [0, Infinity], [0, 0x100000000]])
        assert.throws(() => first(...interval), /interval/);
});

test('first chaos launch and later gaps use distinct ranges measured from the supplied time', () => {
    const calls = [], now = 123456;
    const random = (min, max) => { calls.push([min, max]); return max; };
    assert.equal(nextChaosAt(random, now, true), now + 120000);
    assert.equal(nextChaosAt(random, now + 300000), now + 300000 + 360000);
    assert.deepEqual(calls, [[60, 120], [120, 360]]);
});

test('campaign options select one accelerated production campaign in its own report root', () => {
    const options = campaignOptions(51, 'isolated-worker');
    assert.equal(options.Seed, 51); assert.equal(options.RunCount, 1); assert.equal(options.Mode, 0);
    assert.equal(options.ReportRoot, 'isolated-worker');
    assert.equal(options.Speed, 32); assert.equal(options.NoAnimations, true);
    for (const key of ['DebugPlaythrough', 'Godmode', 'InfiniteStrength', 'InfiniteResources', 'DebugCoverage'])
        assert.equal(options[key], true, key);
    assert.equal(options.ActionLimit, 100000); assert.equal(options.TimeLimitMinutes, 60);
    assert.equal(options.StallSeconds, 300); assert.equal(options.ExploreAll, false);
    options.Speed = 1;
    assert.equal(campaignOptions(52, 'other-worker').Speed, 32);
});

test('chaos options retain five-minute broad smoke and requested device without autoplay cheats', () => {
    for (let device = 0; device < 4; device++) {
        const options = chaosOptions(123 + device, device);
        assert.equal(options.Seed, 123 + device); assert.equal(options.Device, device);
        assert.equal(options.Mode, 0); assert.equal(options.RunCount, 1);
        assert.equal(options.DurationSeconds, 300); assert.equal(options.QuitWhenFinished, true);
        assert.equal(options.ActionTimeoutSeconds, 15); assert.equal(options.LoadTimeoutSeconds, 120);
        assert.equal(options.RecoverySeconds, 10);
        for (const key of ['ScenarioFilter', 'AbilityFilter', 'ScenarioDirectory']) assert.equal(options[key], '');
        for (const key of ['Speed', 'Godmode', 'InfiniteStrength', 'InfiniteResources', 'DebugPlaythrough'])
            assert.equal(key in options, false, key);
    }
});

test('configuration rejects invalid concurrency and seeds outside the Unity int range', () => {
    assert.doesNotThrow(() => validateRunConfig(20, 2, 42));
    assert.doesNotThrow(() => validateRunConfig(1, 1, -2147483648));
    assert.doesNotThrow(() => validateRunConfig(1, 1, 2147483647));
    for (const args of [[0, 2, 42], [1.5, 2, 42], [1, 0, 42], [1, 3, 42],
        [1, 1, -2147483649], [2, 1, 2147483647], [1, 1, 1.5]])
        assert.throws(() => validateRunConfig(...args), /Invalid/);
});

const victory = () => ({ Outcome: 'Victory', ArtifactsComplete: true, IssueCount: 0,
    WarningCount: 0, ErrorCount: 0, Campaign: { Finished: true }, Seed: 42,
    Options: { Mode: 0, Speed: 32, NoAnimations: true, DebugPlaythrough: true, Godmode: true,
        InfiniteStrength: true, InfiniteResources: true, DebugCoverage: true, ExploreAll: false,
        Seed: 42, RunCount: 1 } });

test('campaign acceptance needs finalized clean victory, matching seed and successful independent audit', () => {
    assert.equal(campaignPassed(victory(), 0, 42), true);
    assert.equal(campaignPassed(victory(), 0, 43), false);
    assert.equal(campaignPassed(victory(), 0), false);
    for (const audit of [1, null, undefined, '0']) assert.equal(campaignPassed(victory(), audit, 42), false);
    for (const change of [{ Outcome: 'Running' }, { Outcome: 'TimeLimit' }, { ArtifactsComplete: false },
        { IssueCount: 1 }, { WarningCount: 1 }, { ErrorCount: 1 }, { Campaign: { Finished: false } },
        { Options: { DebugPlaythrough: false, Seed: 42, RunCount: 1 } },
        { Options: { DebugPlaythrough: true, Seed: 43, RunCount: 1 } },
        { Options: { DebugPlaythrough: true, Seed: 42, RunCount: 2 } }])
        assert.equal(campaignPassed({ ...victory(), ...change }, 0, 42), false, JSON.stringify(change));
    assert.equal(campaignPassed(null, 0, 42), false);
});

test('a clean victory cannot pass fast validation after playback or assistance options change', () => {
    for (const key of ['NoAnimations', 'DebugPlaythrough', 'Godmode', 'InfiniteStrength',
        'InfiniteResources', 'DebugCoverage']) {
        for (const value of [false, undefined, 1, 'true']) {
            const report = victory(); report.Options[key] = value;
            assert.equal(campaignPassed(report, 0, 42), false, key + '=' + value);
        }
    }
    for (const change of [{ Mode: 1 }, { Mode: 2 }, { Mode: undefined },
        { Speed: 1 }, { Speed: 16 }, { Speed: '32' }, { Speed: undefined },
        { ExploreAll: true }, { ExploreAll: undefined }]) {
        const report = victory(); Object.assign(report.Options, change);
        assert.equal(campaignPassed(report, 0, 42), false, JSON.stringify(change));
    }
    const report = victory(); delete report.Options;
    assert.equal(campaignPassed(report, 0, 42), false);
});

test('suite accepts distinct runner build IDs only with stable family IDs and common verified assembly hashes', () => {
    const hash = 'a'.repeat(64), identity = { AssemblySha256: hash, FinishedAssemblySha256: hash };
    const campaigns = [42, 43].map(Seed => ({ Seed, BuildId: 'assembly-mvid', Passed: true, ...identity }));
    const chaos = [123, 124].map(Seed => ({ Seed, BuildId: 'unity-build-guid', Passed: true, ...identity }));
    assert.equal(suitePassed(campaigns, chaos, 2, 42, hash), true);
    assert.equal(suitePassed([...campaigns].reverse(), chaos, 2, 42, hash), true);
    assert.equal(suitePassed(campaigns, chaos, 2, 42, hash, true), false);
    assert.equal(suitePassed(campaigns, [], 2, 42, hash), false);
    assert.equal(suitePassed(campaigns.slice(0, 1), chaos, 2, 42, hash), false);
    assert.equal(suitePassed([campaigns[0], campaigns[0]], chaos, 2, 42, hash), false);
    assert.equal(suitePassed(campaigns, chaos, 2, 41, hash), false);
    assert.equal(suitePassed(campaigns, chaos, 2, 42, 'b'.repeat(64)), false);
    assert.equal(suitePassed(campaigns, chaos, 2, 42, undefined), false);
    for (const change of [{ Passed: false }, { TimedOut: true }, { BuildId: 'other' }, { BuildId: '' },
        { AssemblySha256: 'b'.repeat(64) }, { FinishedAssemblySha256: 'b'.repeat(64) },
        { AssemblySha256: undefined }, { FinishedAssemblySha256: undefined }]) {
        assert.equal(suitePassed([{ ...campaigns[0], ...change }, campaigns[1]], chaos, 2, 42, hash), false);
        assert.equal(suitePassed(campaigns, [{ ...chaos[0], ...change }, chaos[1]], 2, 42, hash), false);
    }
});

test('termination eligibility excludes failed spawns and children that already exited', () => {
    const live = { child: { pid: 123, exitCode: null, signalCode: null }, finished: false };
    assert.equal(ownedProcessId(live), 123);
    assert.equal(ownedProcessId({ ...live, finished: true }), null);
    assert.equal(ownedProcessId({ child: {} }), null);
    for (const change of [{ pid: undefined }, { pid: -1 }, { pid: 0 }, { exitCode: 0 }, { signalCode: 'SIGTERM' }])
        assert.equal(ownedProcessId({ ...live, child: { ...live.child, ...change } }), null);
});

function fixture(t) {
    const directory = mkdtempSync(join(tmpdir(), 'fast-validation-test-'));
    t.after(() => {
        const rel = relative(tmpdir(), directory);
        assert.ok(rel && !rel.startsWith('..') && !isAbsolute(rel));
        rmSync(directory, { recursive: true, force: true });
    });
    return directory;
}
function json(path, value) { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, JSON.stringify(value)); }

test('assembly identity is read from current bytes on each check and cannot reuse a stale hash', t => {
    const directory = fixture(t), assembly = join(directory, 'Assembly-CSharp.dll');
    writeFileSync(assembly, 'abc');
    const before = fileSha256(assembly);
    assert.equal(before, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    assert.equal(fileSha256(assembly), before);
    writeFileSync(assembly, 'new build');
    assert.notEqual(fileSha256(assembly), before);
    assert.throws(() => fileSha256(join(directory, 'missing.dll')), /ENOENT/);
});

test('campaign discovery tolerates ordinary files and unfinished report writes', t => {
    const directory = fixture(t), batches = join(directory, 'Playthroughs');
    assert.equal(campaignReport(directory), null);
    json(join(batches, 'latest.json'), {});
    json(join(batches, '20261020-000000', 'run-1', 'report.json'), victory());
    writeFileSync(join(batches, '20261020-000000', 'run-not-a-directory'), 'ignore');
    json(join(batches, '20261020-000001', 'run-1', 'report.json'), victory());
    writeFileSync(join(batches, '20261020-000001', 'run-1', 'report.json'), '{partial');
    const found = campaignReport(directory);
    assert.equal(found.report.Seed, 42);
    assert.equal(found.batch, join(batches, '20261020-000000'));
});

test('chaos discovery rejects latest pointers outside their exact owned player launch', t => {
    const directory = fixture(t), owned = join(directory, 'player-1'), foreign = join(directory, 'foreign');
    json(join(foreign, 'report.json'), { Seed: 999 });
    json(join(owned, 'latest.json'), { Directory: foreign });
    assert.equal(chaosReport(directory), null);
    const run = join(owned, 'chaos-own');
    json(join(run, 'report.json'), { Seed: 123 });
    json(join(owned, 'latest.json'), { Directory: run });
    assert.equal(chaosReport(directory).report.Seed, 123);
    assert.equal(chaosReport(directory).directory, run);
});
