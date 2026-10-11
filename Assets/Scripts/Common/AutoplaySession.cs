using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class AutoplayBatch
{
    public AutoplayMode Mode;
    public string ManifestId, BuildId, ContentId;
    public int FinalizedFloors, PassedFloors, FailedFloors;
    public bool AllFloorsPassed;
    public string StartedUtc, EndedUtc, State = "Running", Reason;
    public int StartingSeed, RequestedRuns;
    public List<AutoplayBatchRun> Runs = new();
}

[Serializable]
public sealed class AutoplayBatchRun
{
    public string CaseId, Profile, BuildId, ContentId;
    public int ExpectedLoot, CollectedLoot, UnresolvedLoot, PlacementCorrections, RecoveryEvents;
    public int RunNumber, Seed, Actions, Purchases, LearnedRanks, CompletedCasts, InterruptedCasts, UnexercisedAbilities, Issues;
    public string RunId, Directory, Outcome, Reason;
    public bool ArtifactsComplete;
    public double Seconds;
    public static AutoplayBatchRun FromReport(AutoplayReport r, string directory) => new() {
        RunId = r.RunId, Directory = directory, RunNumber = r.RunNumber, Seed = r.Seed, Outcome = r.Outcome, Reason = r.Reason,
        CaseId = r.CaseId, Profile = r.Profile, BuildId = r.BuildId, ContentId = r.ContentId, ArtifactsComplete = r.ArtifactsComplete,
        Actions = r.Actions, Seconds = r.Seconds, Issues = r.IssueCount, ExpectedLoot = r.ExpectedLoot, CollectedLoot = r.CollectedLoot,
        UnresolvedLoot = r.UnresolvedLoot?.Count ?? 0, PlacementCorrections = r.PlacementCorrections, RecoveryEvents = r.RecoveryEvents,
        Purchases = r.Purchases.Count(p => p.Success), LearnedRanks = r.Training.Count(p => p.Success),
        CompletedCasts = r.SkillUses.Sum(s => s.Casts), InterruptedCasts = r.SkillUses.Sum(s => s.Interrupted),
        UnexercisedAbilities = r.UnexercisedAbilities?.Length ?? 0 };
}

[Serializable]
public sealed class AutoplayEvent
{
    public string CaseId, Profile, LootId;
    public int ExpectedAmount;
    public bool Removed;
    public string Kind, RunId, Scene, Location, ActorId, TargetId, Message, Position, Severity, StackTrace;
    public string Item, Skill, Evaluator, Currency, Building, CastId;
    public int Seed, RunNumber, Action, Floor, HP, SP, Hunger, Gold, Cost, RankBefore, RankAfter, StockBefore, StockAfter;
    public double Seconds;
    public bool Success;
}

[Serializable]
public sealed class AutoplayTransaction
{
    public string ActorId, Name, Building, Currency, Reason;
    public int Cost, RankBefore, RankAfter;
    public bool Success;
}

[Serializable]
public sealed class AutoplayAbilityCoverage
{
    public string ActorId, Skill, Reason;
    public int Rank, Selected, Completed, Interrupted;
    public bool Active;
}

public sealed partial class AutoplayRunner
{
    public string BatchDirectoryPath { get; private set; }
    public AutoplayBatch Batch { get; private set; }
    public int RunNumber { get; private set; } = 1;
    public int CurrentSeed => Options.SeedForRun(RunNumber - 1);
    public bool Finalizing { get; private set; }
    public bool Advancing { get; private set; }
    public bool SessionRunning => !batchCanceled && (Running || Finalizing || Advancing);
    public bool CoverageEnabled => Running && !PlayerControlled && !takingControl && Options.DebugPlaythrough && Options.DebugCoverage;
    private bool batchCanceled, unloadingRun, reportingFailed;
    private string pendingReportingError;
    private readonly Dictionary<string,string> skipReasons = new();

    public static bool ContinuesAfter(string outcome, AutoplayMode mode = AutoplayMode.Campaign) =>
        mode == AutoplayMode.FloorLootSweep ? outcome is "Passed" or "Failed" : outcome is "Victory" or "Defeat" or "Stalled" or "TimeLimit";

    private void BeginRun()
    {
        bool validation = Report?.ValidationOnly == true;
        actions?.Dispose(); issues?.Dispose(); actions = issues = null;
        visited.Clear(); triedParties.Clear(); discovered.Clear(); repetitions.Clear(); shopped.Clear(); worldSteps.Clear();
        townRoute.Clear(); dungeonRoute.Clear(); approachRoute.Clear();
        purchased.Clear(); failedPurchases.Clear(); failedTraining.Clear(); skipReasons.Clear(); castTraces.Clear();
        pathWorld = null; combatObjective = null; preparedTown = null;
        progressSignature = pendingError = floorIdentity = null;
        pendingReportingError = null; reportingFailed = false; startedCampaign = false;
        started = lastProgress = pausedAt = Time.realtimeSinceStartupAsDouble; pausedSeconds = 0;
        nextTick = Time.unscaledTime;
        UnityEngine.Random.InitState(CurrentSeed);
        string id = $"run-{RunNumber:D4}-seed-{CurrentSeed}-{Guid.NewGuid().ToString("N").Substring(0,8)}";
        DirectoryPath = Path.Combine(BatchDirectoryPath,id);
        Report = new AutoplayReport { Options = JsonUtility.FromJson<AutoplayOptions>(JsonUtility.ToJson(Options)),
            RunId = id, Seed = CurrentSeed, RunNumber = RunNumber, ValidationOnly = validation,
            EligibleForBalance = !Options.DebugPlaythrough && !validation,
            InfiniteStrength = Options.DebugPlaythrough && Options.Godmode,
            StartedUtc = DateTime.UtcNow.ToString("O"), GameVersion = Application.version,
#if UNITY_WEBGL && !UNITY_EDITOR
            BuildId = Application.buildGUID,
#else
            BuildId = typeof(Game).Assembly.ManifestModule.ModuleVersionId.ToString(),
#endif
            UnityVersion = Application.unityVersion };
        BeginSweepRun();
        Directory.CreateDirectory(DirectoryPath);
        store.Begin(DirectoryPath);
        actions = new StreamWriter(Path.Combine(DirectoryPath,"actions.jsonl")) { AutoFlush = true };
        issues = new StreamWriter(Path.Combine(DirectoryPath,"issues.jsonl")) { AutoFlush = true };
        // An early stop still has a valid save artifact, never a copy of the player's save.
        File.WriteAllText(Path.Combine(DirectoryPath,"save.json"),JsonUtility.ToJson(new GameSaveData()));
        Status = "Starting " + ProgressLabel;
        Time.timeScale = Paused ? 0 : Options.Speed;
        WriteReport(); WriteBatch();
    }

    private double Elapsed => Math.Max(0,Time.realtimeSinceStartupAsDouble-started-pausedSeconds-
        (Paused ? Time.realtimeSinceStartupAsDouble-pausedAt : 0));

    private AutoplayEvent Stamp(AutoplayEvent entry)
    {
        var c = Common.Instance?.CampaignContext;
        var game = Game.Instance;
        var town = game == null ? FindFirstObjectByType<Town>() : null;
        var ally = game?.PlayerController?.ControlledAlly;
        if (game != null && !string.IsNullOrEmpty(entry.ActorId))
            ally = game.Allies.Concat(game.DownedAllies).FirstOrDefault(a => a != null && ActorId(a) == entry.ActorId) ?? ally;
        entry.Seed = Report.Seed; entry.RunId = Report.RunId; entry.RunNumber = RunNumber;
        entry.CaseId = Report.CaseId; entry.Profile = Report.Profile;
        entry.Action = Report.Actions; entry.Seconds = Elapsed;
        entry.Scene = SceneManager.GetActiveScene().name;
        entry.Location = !string.IsNullOrEmpty(c?.State.PendingDungeon) ? c.State.PendingDungeon : c?.Location?.Id;
        entry.Floor = game?.PlayerController?.Floor ?? 0;
        entry.ActorId ??= ally != null ? ActorId(ally) : town?.TownPlayer?.ControllingTownAlly?.Id;
        entry.Position ??= ally != null ? ally.TilemapPosition.ToString() : town?.TownPlayer?.ControllingTownAlly != null ?
            town.TownPlayer.ControllingTownAlly.TilemapPosition.ToString() : c?.Position.ToString();
        entry.HP = ally?.Vitals?.HP ?? 0; entry.SP = ally?.Vitals?.SP ?? 0; entry.Hunger = ally?.Vitals?.Hunger ?? 0;
        entry.Gold = game?.PlayerController?.Gold ?? town?.TownPlayer?.Gold ?? 0;
        return entry;
    }

    private void WriteEvent(AutoplayEvent entry)
    {
        try { actions?.WriteLine(JsonUtility.ToJson(Stamp(entry))); }
        catch (Exception e) { pendingReportingError ??= e.ToString(); }
    }

    private void Issue(string severity, string message, string stack = null, string actorId = null)
    {
        if (Report == null) return;
        Report.IssueCount++;
        if (severity == "Warning") Report.WarningCount++;
        else Report.ErrorCount++;
        try
        {
            string json = JsonUtility.ToJson(Stamp(new AutoplayEvent {
                Kind = "Issue", Severity = severity, Message = message, StackTrace = stack, ActorId = actorId }));
            if (issues != null) issues.WriteLine(json);
            else if (Directory.Exists(DirectoryPath)) File.AppendAllText(Path.Combine(DirectoryPath,"issues.jsonl"),json+"\n");
        }
        catch (Exception e) { pendingReportingError ??= e.ToString(); }
    }

    private void Skip(string key, string reason, string actorId = null, string skill = null, string item = null)
    {
        if (skipReasons.TryGetValue(key,out var previous) && previous == reason) return;
        skipReasons[key] = reason;
        WriteEvent(new AutoplayEvent { Kind = "Skipped", Message = reason, ActorId = actorId, Skill = skill, Item = item });
    }

    private void WriteReport()
    {
        if (Report == null || DirectoryPath == null) return;
        if (IsLootSweep) LootLedger?.Audit();
        Report.Options.Speed = Options.Speed;
        Report.UnexercisedAbilities = Report.Abilities.Where(a => a.Active && a.Completed == 0)
            .OrderBy(a => a.ActorId).ThenBy(a => a.Skill)
            .Select(a => $"{a.ActorId}: {a.Skill} (rank {a.Rank}) — {a.Reason ?? "No context-appropriate completed cast"}").ToArray();
        if (IsLootSweep) AutoplaySweepManifest.WriteJson(Path.Combine(DirectoryPath,"report.json"), Report);
        else File.WriteAllText(Path.Combine(DirectoryPath,"report.json"),JsonUtility.ToJson(Report,true));
    }

    private void WriteBatch()
    {
        if (Batch == null || BatchDirectoryPath == null) return;
        if (Report != null)
        {
            var row = Batch.Runs.FirstOrDefault(r => r.RunId == Report.RunId);
            if (row == null) Batch.Runs.Add(row = new AutoplayBatchRun { RunId = Report.RunId, Seed = Report.Seed,
                RunNumber = Report.RunNumber, Directory = Path.GetFileName(DirectoryPath) });
            row.Outcome = Report.Outcome; row.Reason = Report.Reason; row.ArtifactsComplete = Report.ArtifactsComplete;
            row.CaseId = Report.CaseId; row.Profile = Report.Profile; row.BuildId = Report.BuildId; row.ContentId = Report.ContentId;
            row.ExpectedLoot = Report.ExpectedLoot; row.CollectedLoot = Report.CollectedLoot; row.UnresolvedLoot = Report.UnresolvedLoot.Count;
            row.PlacementCorrections = Report.PlacementCorrections; row.RecoveryEvents = Report.RecoveryEvents;
            row.Actions = Report.Actions; row.Seconds = Report.Seconds; row.Issues = Report.IssueCount;
            row.Purchases = Report.Purchases.Count(p => p.Success); row.LearnedRanks = Report.Training.Count(t => t.Success);
            row.CompletedCasts = Report.SkillUses.Sum(s => s.Casts); row.InterruptedCasts = Report.SkillUses.Sum(s => s.Interrupted);
            row.UnexercisedAbilities = Report.UnexercisedAbilities?.Length ?? 0;
        }
        if (IsLootSweep)
        {
            var latest = Batch.Runs.Where(r => r.BuildId == sweepBuild && r.ContentId == sweepContent && r.ArtifactsComplete && r.Outcome is "Passed" or "Failed")
                .GroupBy(r => r.CaseId).Select(g => g.Last()).ToArray();
            Batch.FinalizedFloors = latest.Length; Batch.PassedFloors = latest.Count(r => r.Outcome == "Passed");
            Batch.FailedFloors = latest.Count(r => r.Outcome == "Failed");
            Batch.AllFloorsPassed = Batch.FinalizedFloors == Options.RunCount && Batch.PassedFloors == Options.RunCount;
        }
        if (IsLootSweep) AutoplaySweepManifest.WriteJson(Path.Combine(BatchDirectoryPath,"batch.json"), Batch);
        else File.WriteAllText(Path.Combine(BatchDirectoryPath,"batch.json"),JsonUtility.ToJson(Batch,true));
        var summary = new StringBuilder($"Autoplay batch: {Batch.State}\nStarting seed: {Batch.StartingSeed}; requested runs: {Batch.RequestedRuns}\n{Batch.Reason}\n\n");
        if (IsLootSweep) summary.AppendLine($"VALIDATION ONLY — excluded from balance statistics\nManifest: {Manifest.Id}\nBuild: {sweepBuild}; content: {sweepContent}\nFinalized floors: {Batch.FinalizedFloors}/{Options.RunCount}; passed: {Batch.PassedFloors}; failed: {Batch.FailedFloors}; all passed: {Batch.AllFloorsPassed}\n");
        foreach (var run in Batch.Runs)
            summary.AppendLine($"Run {run.RunNumber}, seed {run.Seed}: {run.Outcome} ({run.Seconds:0.0}s, {run.Actions} actions, {run.Issues} issues)\n" +
                $"  {run.Reason}\n  {run.Directory}/report.json\n  Case: {run.CaseId}; profile: {run.Profile}; loot: {run.CollectedLoot}/{run.ExpectedLoot}; unresolved: {run.UnresolvedLoot}; corrections: {run.PlacementCorrections}; recoveries: {run.RecoveryEvents}\n  Purchases: {run.Purchases}; learned ranks: {run.LearnedRanks}; completed casts: {run.CompletedCasts}; interrupted: {run.InterruptedCasts}; unexercised: {run.UnexercisedAbilities}; artifacts complete: {run.ArtifactsComplete}\n");
        File.WriteAllText(Path.Combine(BatchDirectoryPath,"summary.txt"),summary.ToString());
    }

    private void CancelBatch(string reason)
    {
        batchCanceled = true; Advancing = false;
        if (Batch == null) return;
        if (Batch.State != "Running") return;
        Batch.State = "Stopped"; Batch.Reason = reason; Batch.EndedUtc = DateTime.UtcNow.ToString("O");
        try { WriteBatch(); } catch (Exception e) { pendingReportingError ??= e.ToString(); }
    }

    public void Finish(string outcome, string reason)
    {
        if (!Running) return;
        if (IsLootSweep)
        {
            LootLedger?.Audit();
            if (outcome == "Passed" && (!sweepInspected || LootLedger == null || LootLedger.Failed || Report.CollectedLoot != Report.ExpectedLoot))
            { outcome = "Failed"; reason = "Loot accounting or placement verification failed. " + reason; }
        }
        if (!ContinuesAfter(outcome, Options.Mode)) CancelBatch(reason);
        if (Report.ValidationOnly) Report.EligibleForBalance = false;
        Report.Outcome = outcome; Report.Reason = reason; Report.EndedUtc = DateTime.UtcNow.ToString("O");
        pendingError = null;
        Status = outcome + ": " + reason;
        Time.timeScale = 0;
        Finalizing = true;
        try
        {
            foreach (var trace in castTraces.ToArray()) trace.Interrupt("Run ended before cast completed: " + outcome);
            if (outcome is "Failed" or "Stalled" or "Unsupported" or "AutomationError" or "GameError") Issue(outcome,reason);
            Capture(); WriteReport(); actions?.Flush(); issues?.Flush();
            StartCoroutine(GuardSession(FinishRoutine()));
        }
        catch (Exception e) { Finalizing = false; FailSession("Unable to finish run artifacts",e); }
    }

    private IEnumerator FinishRoutine()
    {
        // Generation can already be in flight when stop/pause arrives. Keep the isolated
        // store installed and capture the completed scene before permitting cleanup.
        double generationDeadline = Time.realtimeSinceStartupAsDouble + 60;
        while (loadingSweep)
        {
            if (Time.realtimeSinceStartupAsDouble > generationDeadline) throw new TimeoutException("Floor generation did not settle during finalization.");
            yield return null;
        }
        if (IsLootSweep) { Capture(); LootLedger?.Audit(); CaptureSweepCamera("gameplay-final.png"); }
        store.Sealed = true;
        string screenshot = Path.Combine(DirectoryPath,"final.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        // CaptureScreenshot writes at the end of a rendered frame. Never unload before it is on disk.
        while (!File.Exists(screenshot) || new FileInfo(screenshot).Length == 0)
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new IOException("Final screenshot was not written: " + screenshot);
            yield return null;
        }
        if (reportingFailed) yield break;
        if (pendingReportingError != null || pendingError != null)
            throw new IOException(pendingReportingError ?? pendingError);
        Report.ArtifactsComplete = true;
        WriteReport(); CloseLogs();
        if (IsLootSweep && ContinuesAfter(Report.Outcome, Options.Mode)) finalizedCases.Add(Report.CaseId);
        Finalizing = false;
        int nextRun = NextSweepCase(RunNumber + 1);
        Advancing = !batchCanceled && ContinuesAfter(Report.Outcome, Options.Mode) && nextRun <= Options.RunCount;
        if (!Advancing && !batchCanceled) { Batch.State = "Completed"; Batch.EndedUtc = DateTime.UtcNow.ToString("O"); }
        WriteBatch();
        if (!Advancing) yield break;
        Status = $"{Report.Outcome}. Next: run {RunNumber+1}/{Options.RunCount}, seed {Options.SeedForRun(RunNumber)}";
        // Keep the finished scene available for controls before committing the unload.
        yield return null;
        while (Paused && !batchCanceled) yield return null;
        if (batchCanceled || takingControl || exiting) yield break;
        unloadingRun = true;
        var common = Common.Instance;
        common.ScreenTransition.HoldClosed();
        CampaignParty.ClearLiveParty(common);
        // Keep the previous isolated store and campaign installed through scene destruction.
        var load = SceneManager.LoadSceneAsync("MainMenu");
        double transitionDeadline = Time.realtimeSinceStartupAsDouble + 30;
        while (!load.isDone)
        {
            if (Time.realtimeSinceStartupAsDouble > transitionDeadline) throw new TimeoutException("Timed out unloading the previous campaign.");
            yield return null;
        }
        common.CampaignContext = null; common.GameSaveData = null; common.CurrentTownConfiguration = null;
        common.Travel.SceneReady(); startedCampaign = false; unloadingRun = false;
        common.ScreenTransition.ReleaseFade();
        WriteReport(); WriteBatch();
        // Pause/stop may arrive while the asynchronous unload is in flight.
        while (Paused && !batchCanceled) yield return null;
        if (batchCanceled || exiting) { Advancing = false; yield break; }
        if (pendingError != null) throw new InvalidOperationException(pendingError);
        RunNumber = nextRun;
        BeginRun(); Advancing = false;
    }

    private IEnumerator GuardSession(IEnumerator routine)
    {
        using (routine as IDisposable)
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) yield break; current = routine.Current; }
                catch (Exception e) { Finalizing = false; unloadingRun = false; FailSession("Run transition or reporting failed",e); yield break; }
                yield return current;
            }
    }

    private void CloseLogs()
    {
        try { actions?.Dispose(); }
        finally { actions = null; try { issues?.Dispose(); } finally { issues = null; } }
    }

    private void FailSession(string context, Exception error, string outcome = "AutomationError")
    {
        reportingFailed = true; batchCanceled = true; Advancing = false; Finalizing = false;
        Time.timeScale = 0;
        string reason = context + ": " + error;
        Status = reason;
        if (Batch != null) { Batch.State = "Error"; Batch.Reason = reason; Batch.EndedUtc = DateTime.UtcNow.ToString("O"); }
        if (Report != null)
        {
            Report.Outcome = outcome; Report.Reason = reason; Report.EligibleForBalance = false;
            Report.EndedUtc = DateTime.UtcNow.ToString("O"); Report.ArtifactsComplete = false;
            Issue(outcome,context,error.ToString());
        }
        // Best effort only: a failed disk must not recursively log errors or advance the batch.
        if (store != null) store.Sealed = true;
        try { CloseLogs(); } catch { }
        try { WriteReport(); } catch { }
        try { WriteBatch(); } catch { }
        pendingReportingError = null;
    }

    private void FinalizeOnDestroy()
    {
        if (Report == null) return;
        if (Running)
        {
            Report.Outcome = "Stopped"; Report.Reason = "Play Mode ended."; Report.EndedUtc = DateTime.UtcNow.ToString("O");
            foreach (var trace in castTraces.ToArray()) trace.Interrupt(Report.Reason);
            try { Capture(); } catch (Exception e) { Issue("AutomationError","Final snapshot failed",e.ToString()); }
        }
        if (Finalizing && !Report.ArtifactsComplete) Report.Reason += "\nSession closed before artifact finalization completed.";
        if (Batch.State == "Running") { Batch.State = "Stopped"; Batch.Reason = "Session closed."; }
        Batch.EndedUtc ??= DateTime.UtcNow.ToString("O");
        try { WriteReport(); WriteBatch(); } catch { }
    }
}
