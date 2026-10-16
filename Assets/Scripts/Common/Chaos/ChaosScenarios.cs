#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class ChaosScenario
{
    public int Version = 1;
    public string CaseId, Entry = "DungeonScene", FixturePath, PairedFixturePath;
    public ChaosPrerequisites Prerequisites;
    public float DeadlineSeconds = 10;
    public List<ChaosScenarioStep> Steps = new();
    public List<ChaosAssertion> Assertions = new();
}
[Serializable]
public sealed class ChaosScenarioStep
{
    public string TargetLabel, WaitScene;
    public ChaosInput Input;
    public bool WaitForControl;
}
[Serializable]
public sealed class ChaosAssertion
{
    // Category: Effect, Cost, Turn, Trigger. Comparison: EqualDelta, MinimumDelta, MaximumDelta, Changed, Equal.
    public string Category, Metric, Actor = "controlled", Comparison = "EqualDelta";
    public double Expected;
    public bool ComparePaired;
}

public sealed partial class ChaosRunner
{
    private string ScenarioSetIdentity()
    {
        if (string.IsNullOrEmpty(Options.ScenarioDirectory)) return ChaosJson.Hash("built-in-heal-scenarios/v1");
        string root = Path.GetFullPath(Options.ScenarioDirectory);
        return ChaosJson.Hash(string.Join("\n", Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => p.Substring(root.Length) + ":" + ChaosJson.Hash(File.ReadAllText(p)))));
    }
    private IEnumerator FullSuite()
    {
        var scenarios = string.IsNullOrWhiteSpace(Options.ScenarioDirectory) ? new List<(ChaosScenario scenario, string path)>() :
            Directory.GetFiles(Options.ScenarioDirectory, "*.scenario.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (ChaosJson.Read<ChaosScenario>(p), p)).ToList();
        if (scenarios.Any(s => s.Item1.Version != 1 || string.IsNullOrEmpty(s.Item1.CaseId)) || scenarios.GroupBy(s => s.Item1.CaseId).Any(g => g.Count() > 1))
            throw new InvalidDataException("Scenario versions/IDs must be valid and unique.");
        int index = 0;
        foreach (var row in Report.Coverage.Cases.Where(c => !c.Demo && !c.Passed))
        {
            if (!Matches(row.Id, Options.ScenarioFilter) || !Matches(row.Ability, Options.AbilityFilter) ||
                Options.Device != ChaosDevice.Mixed && row.Device != "Independent" && row.Device != Options.Device.ToString())
            { row.Reason = "Excluded by this run's filter; remains required for full acceptance"; continue; }
            var match = scenarios.SingleOrDefault(s => s.Item1.CaseId == row.Id);
            var scenario = match.Item1 ?? BuiltInScenario(row);
            if (scenario == null) { row.Reason = "Missing input scenario and effect oracle"; continue; }
            if (scenario.DeadlineSeconds < Options.ActionTimeoutSeconds || !float.IsFinite(scenario.DeadlineSeconds) || scenario.Steps.Count == 0 ||
                scenario.Assertions.Count == 0) { row.Reason = "Unresolved scenario deadline, inputs, or assertions"; continue; }
            string root = match.Item2 == null ? DirectoryPath : Path.GetDirectoryName(Path.GetFullPath(match.Item2));
            ChaosFixture fixture = null;
            try
            {
                fixture = string.IsNullOrEmpty(scenario.FixturePath) ? ChaosFixtures.Create(ChaosRandom.Derive(Options.Seed, row.Id),
                    TownSceneLoader.Default, Common.Instance.ItemManager, scenario.Entry, previousComposition, scenario.Prerequisites, Report.Coverage) :
                    ChaosJson.Read<ChaosFixture>(Path.Combine(root, scenario.FixturePath));
                fixture.Validate();
            }
            catch (Exception error) { row.Reason = "Unresolved prerequisites: " + error.Message; }
            if (fixture == null || row.Reason?.StartsWith("Unresolved prerequisites", StringComparison.Ordinal) == true) continue;
            bool passive = row.Kind is "Passive" or "ClassPassive";
            if (passive && string.IsNullOrEmpty(scenario.PairedFixturePath)) { row.Reason = "Passive requires a paired fixture and comparison oracle"; continue; }
            var pairedDeltas = new Dictionary<string, double>();
            string pairedSegment = null;
            if (passive || scenario.Assertions.Any(a => a.ComparePaired))
            {
                var paired = ChaosJson.Read<ChaosFixture>(Path.Combine(root, scenario.PairedFixturePath)); paired.Validate();
                yield return Prepare(paired); yield return LoadThroughMenu(paired.Entry, row.Device == "Independent" ? "Keyboard" : row.Device);
                var pairBefore = Metrics(scenario.Assertions);
                yield return ScenarioInputs(scenario, row.Device);
                yield return Wait(() => ChaosSnapshot.Read().CanControl && Game.Instance?.PlayerController?.ControlledAlly?.PendingCast == null,
                    "paired scenario effects and control return", scenario.DeadlineSeconds);
                var pairAfter = Metrics(scenario.Assertions);
                foreach (var key in pairBefore.Keys) pairedDeltas[key] = pairAfter[key] - pairBefore[key];
                CloseSegment(); pairedSegment = segment;
            }
            previousComposition = fixture.Composition;
            yield return Prepare(fixture);
            ChaosJson.Write(Path.Combine(segment, "scenario.json"), scenario);
            yield return LoadThroughMenu(fixture.Entry, row.Device == "Independent" ? "Keyboard" : row.Device);
            var before = ChaosSnapshot.Read(); var metrics = Metrics(scenario.Assertions);
            int commandsBefore = commandCount, turnsBefore = turnCount, castStart = castEvents.Count;
            bool menuSeen = false;
            foreach (var step in scenario.Steps)
            { yield return ScenarioStep(step, row.Device, scenario.DeadlineSeconds); menuSeen |= MenuUIInputModule.Active?.HasDialog == true; }
            yield return Wait(() => ChaosSnapshot.Read().CanControl && Game.Instance?.PlayerController?.ControlledAlly?.PendingCast == null,
                "scenario effects and control return", scenario.DeadlineSeconds);
            var after = ChaosSnapshot.Read(); var afterMetrics = Metrics(scenario.Assertions);
            var assertions = scenario.Assertions.Select(a => (assertion: a, passed: AssertMetric(a, metrics, afterMetrics, pairedDeltas))).ToArray();
            bool Category(string name) => assertions.Any(a => a.assertion.Category == name) && assertions.Where(a => a.assertion.Category == name).All(a => a.passed);
            bool pairedEffectObserved = scenario.Assertions.Where(a => a.Category == "Effect" && a.ComparePaired).Any(a => {
                string key = a.Actor + "/" + a.Metric;
                return pairedDeltas.TryGetValue(key, out double baseline) && Math.Abs(afterMetrics[key] - metrics[key] - baseline) > .0001;
            });
            bool released = castEvents.Skip(castStart).Any(e => e == "SkillReleased/" + before.ControlledHero + "/" + row.Ability + "/" + row.Rank);
            bool rejected = row.Variant is "cancel-targeting" or "invalid-target" or "insufficient-resources" or "range-restriction" or "status-restriction";
            var evidence = new ChaosEvidence { CaseId = row.Id, BeforeHash = before.StateHash, AfterHash = after.StateHash,
                Oracle = ChaosJson.Hash(JsonUtility.ToJson(scenario)), MenuOpened = menuSeen, Invoked = rejected ? scenario.Steps.Any(s => s.Input != null || s.TargetLabel != null) :
                    row.Kind == "Active" ? released : commandCount > commandsBefore || before.StateHash != after.StateHash,
                EffectVerified = Category("Effect") && (!passive || pairedEffectObserved), CostsVerified = Category("Cost"), TurnsVerified = Category("Turn"),
                ControlRestored = after.CanControl && !after.Targeting && !MenusOpen(),
                Triggered = Category("Trigger") && turnCount > turnsBefore,
                PairedComparison = pairedDeltas.Count > 0 && scenario.Assertions.Any(a => a.ComparePaired),
                Failure = assertions.All(a => a.passed) ? null : "Failed assertions: " + string.Join(", ", assertions.Where(a => !a.passed).Select(a => a.assertion.Category + ":" + a.assertion.Metric)) };
            ChaosCatalog.Record(Report.Coverage, evidence);
            ChaosJson.Write(Path.Combine(segment, "evidence.json"), evidence);
            if (!row.Passed) AddIssue("ScenarioFailed", row.Id + ": " + row.Reason, null);
            CloseSegment();
            if (pairedSegment != null)
            {
                string pairedEvidence = Path.Combine(segment, "paired"); Directory.CreateDirectory(pairedEvidence);
                foreach (string path in Directory.GetFiles(pairedSegment)) File.Copy(path, Path.Combine(pairedEvidence, Path.GetFileName(path)), false);
            }
            var completed = new ChaosReport { State = "Completed", BuildId = Report.BuildId, ContentId = Report.ContentId, ScenarioSetId = Report.ScenarioSetId,
                ArtifactsComplete = true, Coverage = new ChaosCoverage { Cases = new() { ChaosJson.Clone(row) }, Participation = ChaosJson.Clone(Report.Coverage).Participation } };
            foreach (string path in Directory.GetFiles(segment, "*", SearchOption.AllDirectories)) completed.Artifacts.Add(new ChaosArtifact { Path = Path.GetRelativePath(segment, path), Bytes = new FileInfo(path).Length, Sha256 = ChaosJson.FileHash(path) });
            ChaosJson.Write(Path.Combine(segment, "case-result.json"), completed);
            WriteReport(); index++;
        }
        if (!Report.Coverage.FullPass) Report.Reason = "Full verification incomplete: " + Report.Coverage.Cases.Count(c => !c.Demo && !c.Passed) + " required cases remain; " + index + " scenarios executed.";
    }
    private IEnumerator ScenarioInputs(ChaosScenario scenario, string device)
    { foreach (var step in scenario.Steps) yield return ScenarioStep(step, device, scenario.DeadlineSeconds); }
    private IEnumerator ScenarioStep(ChaosScenarioStep step, string device, float deadline)
    {
        if (!string.IsNullOrEmpty(step.TargetLabel))
        {
            var target = ChaosUI.Visible().SingleOrDefault(s => ChaosUI.Label(s) == step.TargetLabel);
            yield return Activate(target, device == "Independent" ? "Keyboard" : device);
        }
        if (step.Input != null)
        {
            if (device != "Independent" && step.Input.Device != device) throw new InvalidDataException("Scenario input device differs from coverage device.");
            yield return Pulse(ChaosJson.Clone(step.Input));
        }
        if (step.WaitForControl || !string.IsNullOrEmpty(step.WaitScene))
            yield return Wait(() => (!step.WaitForControl || ChaosSnapshot.Read().CanControl) &&
                (string.IsNullOrEmpty(step.WaitScene) || ChaosSnapshot.Read().Scene == step.WaitScene), "scenario expectation", deadline);
    }
    private Dictionary<string, double> Metrics(IEnumerable<ChaosAssertion> assertions)
    {
        var result = new Dictionary<string, double>();
        var snapshot = ChaosSnapshot.Read();
        foreach (var assertion in assertions)
        {
            string key = assertion.Actor + "/" + assertion.Metric;
            var actors = snapshot.Actors.Where(a => assertion.Actor == "all" || assertion.Actor == "controlled" && a.Id == snapshot.ControlledHero || a.Id == assertion.Actor).ToArray();
            result[key] = assertion.Metric switch {
                "HP" => actors.Sum(a => a.HP), "Mana" => actors.Sum(a => a.Mana), "Hunger" => actors.Sum(a => a.Hunger),
                "Gold" => snapshot.Gold, "Turns" => turnCount, "Commands" => commandCount,
                "StartedCasts" => startedCasts, "ReleasedCasts" => releasedCasts,
                "Actors" => snapshot.Actors.Count, "Control" => snapshot.CanControl ? 1 : 0,
                "X" => actors.Sum(a => a.X), "Y" => actors.Sum(a => a.Y), "Arrows" => actors.Sum(a => a.Arrows),
                "MaxHP" => actors.Sum(a => a.MaxHP), "MaxMana" => actors.Sum(a => a.MaxMana), "Strength" => actors.Sum(a => a.Strength),
                "Defense" => actors.Sum(a => a.Defense), "CastRemaining" => actors.Sum(a => a.CastRemaining),
                "Statuses" => actors.Sum(a => a.Status.Split('|').Count(s => !string.IsNullOrEmpty(s))), "Floor" => snapshot.Floor,
                _ => throw new InvalidDataException("Unknown read-only metric: " + assertion.Metric) };
            if (assertion.Metric is "HP" or "Mana" or "Hunger" && actors.Length == 0) throw new InvalidDataException("Assertion actor is absent: " + assertion.Actor);
        }
        return result;
    }
    private static bool AssertMetric(ChaosAssertion a, Dictionary<string, double> before, Dictionary<string, double> after, Dictionary<string, double> paired)
    {
        string key = a.Actor + "/" + a.Metric;
        double delta = after[key] - before[key];
        if (a.ComparePaired) { if (!paired.ContainsKey(key)) return false; delta -= paired[key]; }
        return a.Comparison switch { "EqualDelta" => Math.Abs(delta - a.Expected) < .0001,
            "MinimumDelta" => delta >= a.Expected, "MaximumDelta" => delta <= a.Expected,
            "Changed" => delta != 0, "Equal" => !a.ComparePaired && Math.Abs(after[key] - a.Expected) < .0001,
            _ => throw new InvalidDataException("Unknown comparison: " + a.Comparison) };
    }
    // Small production-derived baseline. Other mechanics require their own declared fixture/oracle;
    // damage, summoning, songs, commands, exploration and probability are never inferred from a cast receipt.
    private ChaosScenario BuiltInScenario(ChaosCase row)
    {
        if (row.Kind != "Active" || row.Source != "class:healer" || row.Ability != "Heal" || !row.Variant.StartsWith("execute-", StringComparison.Ordinal)) return null;
        var skill = ClassCatalog.Load().Get("healer").Skills.Single(e => e.Skill?.SkillName == row.Ability).Skill;
        var scenario = new ChaosScenario { CaseId = row.Id, DeadlineSeconds = Math.Max(Options.ActionTimeoutSeconds, 30),
            Prerequisites = new ChaosPrerequisites { ClassId = "healer", SkillName = row.Ability, Rank = row.Rank,
                Level = LevelSystem.MaxLevel, HP = 12, ControlRequiredHero = true } };
        if (row.Device == "Mouse") scenario.Steps.Add(new ChaosScenarioStep { TargetLabel = "Skills" });
        else scenario.Steps.Add(new ChaosScenarioStep { Input = Button("R", row.Device) });
        scenario.Steps.Add(new ChaosScenarioStep { TargetLabel = $"{skill.SkillName}  R{row.Rank}  {skill.ManaCost} Mana" });
        if (skill.RequiresTargetSelection)
        {
            // Targeting mouse input is world-based. Until its fixture supplies a target screen point, do not guess.
            if (row.Device == "Mouse") return null;
            scenario.Steps.Add(new ChaosScenarioStep { Input = Button("Enter", row.Device) });
        }
        scenario.Steps.Add(new ChaosScenarioStep { WaitForControl = true });
        scenario.Assertions.AddRange(new[] {
            new ChaosAssertion { Category = "Effect", Metric = "HP", Comparison = "MinimumDelta", Expected = 1 },
            new ChaosAssertion { Category = "Cost", Metric = "Mana", Expected = -skill.ManaCost },
            new ChaosAssertion { Category = "Turn", Metric = "Turns", Comparison = "MinimumDelta", Expected = 1 },
            new ChaosAssertion { Category = "Effect", Metric = "ReleasedCasts", Expected = 1 }
        });
        return scenario;
    }
    private IEnumerator Replay()
    {
        var original = ChaosJson.Read<ChaosReport>(Path.Combine(Options.ReplayDirectory, "report.json"));
        ChaosJson.RequireIdentity(original, Report.BuildId, Report.ContentId);
        ChaosJson.VerifyArtifacts(Options.ReplayDirectory, original);
        var directories = Directory.GetDirectories(Options.ReplayDirectory, "scenario-*").OrderBy(d => d, StringComparer.Ordinal).ToArray();
        if (directories.Length == 0) throw new InvalidDataException("Replay has no fixtures.");
        foreach (var directory in directories)
        {
            var fixture = ChaosJson.Read<ChaosFixture>(Path.Combine(directory, "fixture.json")); fixture.Validate();
            yield return Prepare(fixture);
            var expected = File.ReadLines(Path.Combine(directory, "checkpoints.jsonl")).Select(JsonUtility.FromJson<Checkpoint>).ToDictionary(c => c.Index);
            foreach (string line in File.ReadLines(Path.Combine(directory, "inputs.jsonl")))
            {
                var input = JsonUtility.FromJson<ChaosInput>(line);
                if (!expected.TryGetValue(input.Index, out var checkpoint)) throw new ReplayDivergence("Input has no completed checkpoint: " + input.Index);
                string hash = ChaosSnapshot.Read().StateHash;
                if (hash != input.Before) throw new ReplayDivergence("Before input " + input.Index + ": expected " + input.Before + ", observed " + hash);
                yield return Pulse(input, checkpoint.Hash);
            }
        }
    }
}
#endif
