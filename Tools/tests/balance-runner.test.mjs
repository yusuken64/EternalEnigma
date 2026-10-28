import test from 'node:test';
import assert from 'node:assert/strict';
import { auditReport, defaultConfig, identity, makeOptions, pairedComparison, POLICY, proposeCandidates, summarize, validateConfig, wilson } from '../balance-runner.mjs';

const catalog = { Heroes: ['warrior','healer','scout'], Items: [{Name:'Potion',HP:40,Price:100}], Abilities:[{Name:'Heal',Mana:4}] };
const config = () => defaultConfig(catalog);
const manifest = () => ({Id:'experiment',ContentId:'content',Config:config()});
const candidate = {Id:'baseline',ItemPrices:[],AbilityMana:[]};
function attempt(seed, outcome='Victory', hero='warrior') {
    const options = makeOptions(seed,hero,candidate,'training','report',manifest());
    const report = {Options:options,Seed:seed,RunNumber:1,Policy:POLICY,BuildId:'build',ArtifactsComplete:true,EligibleForBalance:true,ValidationOnly:false,
        IssueCount:0,ErrorCount:0,WarningCount:0,Outcome:outcome,Campaign:{Finished:outcome==='Victory',Completed:['story-0']},Finale:{HomecomingCompleted:outcome==='Victory'},
        Purchases:[{Name:'Potion',Cost:100,Success:true}],
        Balance:{...options.Balance,Policy:POLICY,Retreats:0,Abilities:[{ActorId:hero,Skill:'Heal',Rank:1,Decisions:20,LegalOpportunities:10,Unaffordable:5,Selected:8}],
            Stages:[{Kind:'StoryDungeon',Location:'story-0',Tier:0,Floor:1,Outcome:outcome==='Victory'?'Cleared':'Defeat',Turns:40,Retreats:0,
                DamageTaken:20,HealingReceived:10,Downings:0,MinimumHealthFraction:.4,EntryParty:[{Level:1}]}]}};
    return {Seed:seed,Hero:hero,Valid:true,Report:report,Errors:[]};
}

test('normal options are explicit and report audit rejects debug contamination', () => {
    const a=attempt(42), expected=structuredClone(a.Report.Options);
    assert.deepEqual(auditReport(a.Report,expected),[]);
    for (const field of ['DebugPlaythrough','DebugCoverage','Godmode','InfiniteResources','InfiniteStrength']) {
        const r=structuredClone(a.Report);r.Options[field]=true;assert.ok(auditReport(r,expected).length,field);
    }
    for (const field of ['CandidateId','ContentId','Cohort','HeroId']) {
        const r=structuredClone(a.Report);r.Options.Balance[field]='wrong';assert.ok(auditReport(r,expected).length,field);
    }
});

test('defeat is valid data and stalled/time-limited runs never become balance losses', () => {
    const a=attempt(42,'Defeat');assert.deepEqual(auditReport(a.Report,a.Report.Options),[]);
    for (const outcome of ['Stalled','TimeLimit','Unsupported','GameError','AutomationError','Stopped','Running']) {
        const r=structuredClone(a.Report);r.Outcome=outcome;assert.ok(auditReport(r,r.Options).length,outcome);
    }
});

test('holdout seeds are disjoint and resource/acceptance bounds cannot be disabled', () => {
    assert.equal(validateConfig(config()).Version,1);
    for (const mutate of [c=>c.ValidationSeeds.push(42), c=>c.Workers=8, c=>c.MaxCandidates=Infinity,
        c=>c.Targets.MinRuns=0, c=>c.Targets.RequiredTiers=[], c=>c.Heroes=['warrior','warrior']]) {
        const c=config();mutate(c);assert.throws(()=>validateConfig(c));
    }
});

test('confidence bounds and late-stage coverage prevent tiny perfect cohorts from passing', () => {
    assert.deepEqual(wilson(0,0),[0,1]);
    assert.ok(wilson(18,18)[0]>.8);
    assert.equal(summarize([attempt(42)],config().Targets).Accepted,false);
    const all=Array.from({length:30},(_,i)=>attempt(i));
    const targets={...config().Targets,RequiredTiers:[0]};
    assert.equal(summarize(all,targets).Accepted,true);
    assert.equal(summarize(all,config().Targets).Accepted,false);
    all.push({...attempt(31),Valid:false,Errors:['automation error']});
    assert.equal(summarize(all,targets).Accepted,false);
});

test('stage clearance counts unique campaigns, preserving retreat burden without inflating samples', () => {
    const a=attempt(42);const stage=a.Report.Balance.Stages[0];
    a.Report.Balance.Stages.unshift({...stage,Outcome:'Retreated',Retreats:1});
    const s=summarize([a],config().Targets).Stages[0];
    assert.equal(s.Reached,1);assert.equal(s.Cleared,1);assert.equal(s.MeanTurns,80);assert.equal(s.MeanRetreats,1);
});

test('paired comparisons match both seed and hero and never compare missing or invalid cases', () => {
    const base=[attempt(42,'Defeat'),attempt(42,'Victory','healer')];
    const changed=[attempt(42),{...attempt(42,'Defeat','healer'),Valid:false},attempt(43)];
    const p=pairedComparison(base,changed);assert.equal(p.Pairs,1);assert.equal(p.WinDelta,1);
});

test('candidate changes are named, bounded and drawn from observed burden, with unchanged baseline', () => {
    const a=attempt(42), before=identity(a);
    const candidates=proposeCandidates([a],catalog,2);
    assert.equal(candidates.length,2);assert.equal(identity(a),before);
    assert.deepEqual(candidates[0].ItemPrices,[{Name:'Potion',Multiplier:.85}]);
    assert.deepEqual(candidates[1].AbilityMana,[{Name:'Heal',Multiplier:.85}]);
    assert.deepEqual(proposeCandidates([{...a,Valid:false}],catalog,5),[]);
});

test('a forged victory without real homecoming fails audit', () => {
    const a=attempt(42);a.Report.Finale.HomecomingCompleted=false;
    assert.ok(auditReport(a.Report,a.Report.Options).some(e=>e.includes('completion')));
});

test('rounding-equivalent Mana trials are skipped while a deeper price trial remains available',()=>{
    const a=attempt(42); const tiny={...catalog,Abilities:[{Name:'Heal',Mana:2}]};
    const proposals=proposeCandidates([a],tiny,20);
    assert.equal(proposals.some(c=>c.AbilityMana.length),false);
    assert.ok(proposals.some(c=>c.ItemPrices[0]?.Multiplier===.75));
});

test('duplicate cases and mixed identities cannot establish acceptance',()=>{
    const targets={...config().Targets,RequiredTiers:[0]};
    const all=Array.from({length:30},(_,i)=>attempt(i));
    all[1]=structuredClone(all[0]);assert.equal(summarize(all,targets).Accepted,false);
    all[1]=attempt(1);all[1].Report.Balance.ContentId='different';assert.equal(summarize(all,targets).Accepted,false);
});

test('actual effect totals stay distinct from estimated values and targeting teams',()=>{
    const a=attempt(42);a.Report.Balance.Effects=[{Skill:'Heal',Rank:1,RootAction:'Skill',TargetTeam:'Ally',HPRemoved:0,HPRestored:7,ManaRemoved:1,ManaRestored:0,HungerRestored:0,Changes:2}];
    const s=summarize([a],config().Targets);assert.equal(s.Effects[0].HPRestored,7);assert.equal(s.Effects[0].ManaRemoved,1);
});
