# Progression balance harness

This harness asks whether a cautious player can make steady progress under normal
rules, and how ability and item costs affect that progress. It reuses the isolated
campaign runner and production movement, combat, shopping, training and travel.
The debug victory verifier is deliberately not used: a clean gameplay defeat is
valid data. Crashes, stalls, unsupported decisions, time limits, missing artifacts,
takeover and incomplete cohorts cannot establish balance and block tuning.

## Player policy and scope

`cautious-v2` explores discovered dungeon terrain, remembers stairs only after
seeing them, and honors actual layout-reveal abilities. It estimates danger from
visible enemies without inspecting future random rolls. It values effective
damage, kills before retaliation, emergency healing, emergency Mana, favorable
positions and attainable supplies. It holds position for approaching enemies,
avoids revealed hazards, uses legal Retreat when available, and visits the inn
before shopping. Shopping compares useful reserves and equipment against gold;
training prioritizes missing recovery and combat capabilities within real points.

This is an explainable heuristic player, not proof of human-equivalent skill.
Companions use the production navigation/attack AI and the cautious skill evaluator.
The overworld planner still knows the campaign travel graph. Encounter puzzle
helpers are scripted. Unknown traversal and encounter cases stop explicitly rather
than being counted as defeats. Review representative decisions and unsupported
cases before drawing conclusions. A skill that the policy cannot use well is not
automatically weak. Passive and indirect effect attribution is not exhaustive.

All cheats and deliberate ability coverage must be false. Balance options cannot
be used for loot sweeps or direct finale fixtures. Each case starts a new campaign
with normal supplies and a specified protagonist. No levels, gold, healing, enemy
deaths, map reveals or victories are granted by the harness. Experiments may change
named authored prices or Mana costs by 0.75–1.25, using ordinary costs thereafter.
Loaded asset values are restored on cleanup; experiment execution never saves
changes to production assets.

## Evidence

Every report carries experiment, candidate, cohort, content and build identities,
the complete options and the policy version. `Balance.Stages` records actual floor
fingerprints, entry/exit party levels/classes/equipment/skills/vitals, gold, turns,
damage, healing, downings, item uses, retreats and outcomes. Tier/floor summaries
show conditional clearance among campaigns reaching that stage; retreats do not
create extra independent samples. Campaign success includes all valid starts.

`decisions.jsonl` explains selected actions and purchases. Ability observations
distinguish decisions, legal opportunities, unaffordable choices, selections,
Mana-paid starts, releases and interruptions. A release is not a guaranteed hit.
`Balance.Effects` records clamped HP/Mana/hunger changes from real gameplay actions,
with skill/item provenance through their immediate effect chains. Overkill and
overhealing do not inflate those deltas. Passive responses and later status ticks
may be attributed to an action type rather than their originating skill. These
observations do not measure counterfactual damage prevention; that needs paired
encounters or campaign comparisons.

The cohort produces `summary.json`, `report.html`, and CSV files for stages,
abilities, items and observed effects. Each failed/invalid attempt retains its
save, screenshot, action log, issues and player log.

## Running

1. In Unity, execute **Tools > Eternal Enigma > Balance > Export Catalog**. This
   writes `Logs/Balance/catalog.json`, including legal hero IDs, shop prices and
   production class ability costs.
2. Create and review a configuration:

   ```powershell
   node Tools/balance-runner.mjs init Logs/Balance/custom-config.json
   ```

   `Tools/balance-config.json` is the checked-in moderate configuration. Specify
   representative protagonist IDs in `Heroes`. The generated default
   selects Rowan (Warrior/Healer), Riley (Elementalist/Healer), and Taylor
   (Archer/Warrior) when available; review their class coverage. Configure distinct
   training and validation seeds and resource limits before starting.
3. Record build inputs, build **Tools > Eternal Enigma > Balance > Build Windows
   Player**, then seal the completed build. The stamp requires unchanged gameplay
   content and a successful build newer than the recorded inputs:

   ```powershell
   node Tools/balance-runner.mjs prepare-build
   # Execute the Unity build menu here, then:
   node Tools/balance-runner.mjs stamp-build
   node Tools/balance-runner.mjs run Logs/Balance/my-experiment Tools/balance-config.json
   ```

   Use a new output directory for every experiment. At most two development players
   run concurrently in background batch mode, with separate saves and reports.
   Graphics remain enabled for screenshots; the runner does not pass `-nographics`.
   The supervisor watches its own children,
   enforces per-case and whole-experiment time limits, and stops on invalid evidence.

   With the editor closed, the equivalent build is a Unity command-line invocation
   using `-batchmode -quit -projectPath <repository> -executeMethod
   BalanceHarnessEditor.Build -logFile <persistent log path>`. Run `prepare-build`
   first and `stamp-build` only after the build process exits successfully. Build
   results and inputs survive editor shutdown; only disposable editor pilots use Temp.
4. Inspect or stop it from another terminal:

   ```powershell
   node Tools/balance-runner.mjs status Logs/Balance/my-experiment
   node Tools/balance-runner.mjs stop Logs/Balance/my-experiment
   node Tools/balance-runner.mjs inspect <path-to-report.json>
   ```

For an editor pilot, write normal `AutoplayOptions` plus `BalanceExperimentOptions`
to `Temp/Balance/options.json`, then execute **Balance > Run From File** through
Unity MCP. Label short probes `Cohort: "pilot"`; they do not satisfy cohort acceptance.
Stop Play Mode after final artifacts are written to restore the original session.

## Autonomous tuning and stopping

The provisional moderate profile uses an aggregate campaign success **lower 95%
Wilson bound of 0.65**, conditional floor clearance lower bound of **0.75** where
enough cases reached the floor, at least 18 valid campaigns, at least 12 observations
for covered stage bands, evidence at tiers 0–4, and at most three mean retreats.
Each represented protagonist must also meet the raw campaign success target.
These are explicit design assumptions, not universal definitions of fairness.
Change the numeric targets when choosing forgiving or demanding difficulty; the
`Difficulty` label alone does not change them.

The runner establishes a baseline, proposes individual cost reductions from observed
recurring supply expense or Mana shortages, then compares matched seed/protagonist
pairs with the policy held fixed. A change needs no reduction in wins or completed
objectives, cleared floors, or protagonist win rates, and an improvement in the
declared score (100 × win fraction + 2 × mean completed objectives + 0.5 × mean
cleared floors − 2 × mean retreats). Promoted changes can accumulate, but
remain within 25% of authored costs. All candidates and rejected trials remain in
the experiment directory. No-data, rounding-equivalent and unsupported effects
must not be interpreted as proof of value.

The default budget is seven candidates, 45 minutes per case and 24 hours total;
choose a smaller pilot before an overnight experiment. The separate validation
seeds are used once, after a candidate passes training. A failed holdout is not fed
back into tuning. Final states distinguish `AcceptedForPolicy`, `NotConverged`,
`ValidationFailed` and `Stopped`. A resource limit never means success. Acceptance
applies only to the declared player, sampled protagonists, content and criteria.
It does not establish that every ability is balanced or that the game is fun.

The final `recommendation.json` contains the accepted experiment settings when
available. Costs are not silently written into assets. Applying an accepted change
to assets, rebuilding and verifying the resulting production behavior is a separate
finalization step. If bounded cost changes cannot fix a progression cliff, investigate
the encounter, available preparation and policy decisions rather than relaxing the
acceptance targets until they pass.

## Verification

```powershell
node --test Tools/tests/balance-runner.test.mjs
node Tools/unity-mcp.mjs harness Balance
```

`EternalEnigma.Tests.BalanceHarnessTests` covers cheat rejection, bounded targets,
cost rounding, unknown targets and cautious overkill/cast-time decisions. The
explicit PlayMode suite runs real opening progression, artifact/save isolation,
experimental cost restoration, and the basic-attack replay regression discovered
during the initial pilot. Integration fixtures are validation-only evidence.
