#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fixtures may write saves only before a scenario. Scenario routines only observe and queue device events.
public sealed partial class ChaosRunner : MonoBehaviour
{
    public const string PendingKey = "EternalEnigma.Chaos.Pending";
    public static ChaosRunner Active { get; private set; }
    public ChaosOptions Options { get; private set; }
    public ChaosReport Report { get; private set; }
    public string DirectoryPath { get; private set; }
    public string Status { get; private set; } = "Initializing";
    private IDisposable saveScope, preferences;
    private ChaosSaveStore store;
    private ChaosInputDevices devices;
    private StreamWriter inputs, checkpoints, logs, observations;
    private UnityEngine.Random.State originalRandom;
    private bool stop, disposed, prepared, finishing, reloadLocked;
    private double started, heartbeatAt;
    private string segment, pendingError, reportingError, previousComposition;
    private int inputIndex, segmentIndex, turnCount, commandCount, startedCasts, releasedCasts;
    private float originalTimeScale;
    private int originalWidth, originalHeight;
    private FullScreenMode originalMode;
    private bool? originalFullControl;
    private DungeonAnimationMode? originalAnimation;
    private readonly List<(UnityEngine.Audio.AudioMixer mixer, string parameter, float value)> audioState = new();
    private readonly ChaosProgress progress = new();
    private readonly List<string> castEvents = new();
    private double Now => Time.realtimeSinceStartupAsDouble;
    private double Elapsed => Now - started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string json = null;
        try
        {
#if UNITY_EDITOR
            json = UnityEditor.SessionState.GetString(PendingKey, "");
            UnityEditor.SessionState.EraseString(PendingKey);
#else
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--chaos-options");
            if (index >= 0) json = File.ReadAllText(args[index + 1]);
#endif
            if (!string.IsNullOrWhiteSpace(json)) Launch(JsonUtility.FromJson<ChaosOptions>(json));
        }
        catch (Exception error) { Debug.LogException(error); if (!Application.isEditor) Application.Quit(2); }
    }

    public static void Launch(ChaosOptions options)
    {
        options.Validate();
        if (Active != null || ChaosSessionLock.Held || AutoplayRunner.Active != null)
            throw new InvalidOperationException("Stop the existing chaos/autoplay session first.");
        var host = new GameObject("Input-only chaos runner"); DontDestroyOnLoad(host);
        var runner = host.AddComponent<ChaosRunner>();
        try { runner.Initialize(options); } catch { runner.Dispose(); Destroy(host); throw; }
    }
    private void Initialize(ChaosOptions options)
    {
        Active = this; ChaosSessionLock.Held = true; Options = ChaosJson.Clone(options);
        originalRandom = UnityEngine.Random.state; originalTimeScale = Time.timeScale;
        originalWidth = Screen.width; originalHeight = Screen.height; originalMode = Screen.fullScreenMode;
        originalFullControl = DungeonPreferences.FullControlOverride; originalAnimation = DungeonPreferences.AnimationOverride;
        DungeonPreferences.FullControlOverride = null; DungeonPreferences.AnimationOverride = null;
        started = Now;
        string root = string.IsNullOrWhiteSpace(options.ReportRoot) ?
            Application.isEditor ? "Logs/Chaos" : Path.Combine(Application.persistentDataPath, "Chaos") : options.ReportRoot;
        DirectoryPath = Path.GetFullPath(Path.Combine(root, "chaos-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
        Directory.CreateDirectory(DirectoryPath);
        Report = new ChaosReport { Options = Options, Seed = options.Seed, StartedUtc = DateTime.UtcNow.ToString("O"),
            BuildId = AutoplaySweepManifest.CurrentBuildId, ContentId = AutoplaySweepManifest.CurrentContentId() };
        ChaosJson.Write(Path.Combine(root, "latest.json"), new Location { Directory = DirectoryPath });
        logs = new StreamWriter(Path.Combine(DirectoryPath, "logs.jsonl")) { AutoFlush = true };
        Application.logMessageReceived += OnLog;
        store = new ChaosSaveStore(); saveScope = SaveSystem.UseStore(store);
        preferences = GamePreferences.Isolate();
        devices = new ChaosInputDevices();
        GameInputDiagnostics.Observed += OnObservation;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.LockReloadAssemblies(); reloadLocked = true;
#endif
        WriteReport();
    }
    private IEnumerator Start()
    {
        Exception failure = null;
        var stack = new Stack<IEnumerator>(); stack.Push(Session());
        while (stack.Count > 0)
        {
            object next = null; bool moved = false;
            try
            {
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
                else (stack.Pop() as IDisposable)?.Dispose();
            }
            catch (Exception error) { failure = error; break; }
            if (!moved) continue;
            if (next is IEnumerator routine) stack.Push(routine); else yield return next;
        }
        foreach (var routine in stack) (routine as IDisposable)?.Dispose();
        if (failure != null)
        {
            Report.State = failure is OperationCanceledException ? "Stopped" : failure is ReplayDivergence ? "ReplayDiverged" : "Failed";
            Report.Reason = failure.ToString();
            Report.ReplayDiverged = failure is ReplayDivergence;
            AddIssue(Report.State, failure.Message, failure.StackTrace).Screenshot = "final.png";
        }
        else Report.State = "Completed";
        finishing = true;
        devices?.Release();
        for (int i = 0; i < 3; i++) yield return null;
        string screenshot = Path.Combine(DirectoryPath, "final.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        double deadline = Now + 15;
        while ((!File.Exists(screenshot) || new FileInfo(screenshot).Length == 0) && Now < deadline) yield return null;
        try
        {
            CloseSegment();
            logs?.Flush();
            File.WriteAllText(Path.Combine(DirectoryPath, "save.json"), store.Read(0) ?? "{}");
            WriteReport();
        }
        catch (Exception error) { reportingError = error.ToString(); Report.ArtifactsComplete = false; Report.State = "ReportingFailed"; Debug.LogError("Chaos reporting failure: " + error); }
        // Keep isolation installed until the old scene and its OnDestroy save callbacks have finished.
        if (Common.InstanceOrNull != null) CampaignParty.ClearLiveParty(Common.Instance);
        var cleanup = SceneManager.LoadSceneAsync("MainMenu");
        deadline = Now + Options.LoadTimeoutSeconds;
        while (!cleanup.isDone && Now < deadline) yield return null;
        if (!cleanup.isDone)
        {
            Report.ArtifactsComplete = Report.SmokePassed = Report.FullPassed = false;
            Report.State = "CleanupFailed";
            try { WriteReport(); } catch { }
            Status = "Cleanup failed; save isolation retained until process/Play Mode exit";
            if (!Application.isEditor) Application.Quit(2);
            yield break;
        }
        yield return null;
        bool quit = Options.QuitWhenFinished;
        Dispose();
        if (Common.InstanceOrNull != null) { Common.Instance.CampaignContext = null; Common.Instance.GameSaveData = SaveSystem.Inspect(SaveSystem.ActiveSlot, out _); }
        try
        {
            if (pendingError != null && Report.State == "Completed") { Report.State = "Failed"; AddIssue("CleanupError", pendingError, null); }
            foreach (var path in Directory.GetFiles(DirectoryPath, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith("report.json") && !p.EndsWith("heartbeat.json")))
                Report.Artifacts.Add(new ChaosArtifact { Path = Path.GetRelativePath(DirectoryPath, path), Bytes = new FileInfo(path).Length, Sha256 = ChaosJson.FileHash(path) });
            Report.ArtifactsComplete = File.Exists(screenshot) && new FileInfo(screenshot).Length > 0 && reportingError == null;
            Report.Seconds = Elapsed; Report.EndedUtc = DateTime.UtcNow.ToString("O");
            bool clean = Report.State == "Completed" && Report.ArtifactsComplete && Report.Issues.Count == 0;
            Report.SmokePassed = clean && Options.Mode == ChaosMode.Smoke && Options.DurationSeconds >= 300 && Report.Seconds >= Options.DurationSeconds;
            Report.FullPassed = clean && Report.Coverage?.FullPass == true;
            if (!Report.ArtifactsComplete) { Report.State = "ReportingFailed"; Report.Reason = reportingError ?? "Final screenshot missing"; }
            WriteReport();
        }
        catch (Exception error) { Report.ArtifactsComplete = Report.SmokePassed = Report.FullPassed = false; Report.State = "ReportingFailed"; Debug.LogError("Chaos final reporting failed: " + error); }
        int exit = (Options.Mode == ChaosMode.Full ? Report.FullPassed : Report.SmokePassed) ? 0 : 1;
        Status = Report.State + ": " + DirectoryPath;
        if (!Application.isEditor && quit) Application.Quit(exit);
    }
    private IEnumerator Session()
    {
        yield return Wait(() => Common.InstanceOrNull?.Travel != null && FindFirstObjectByType<MainMenu>()?.IsReady == true, "main menu startup", Options.LoadTimeoutSeconds);
        foreach (var slider in FindObjectsByType<VolumeSlider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (slider.AudioMixerGroup != null && slider.AudioMixerGroup.audioMixer.GetFloat(slider.VolumeParameterName, out float value))
                audioState.Add((slider.AudioMixerGroup.audioMixer, slider.VolumeParameterName, value));
        Report.Coverage = ChaosCatalog.Build(Common.Instance, TownSceneLoader.Default);
        Report.ScenarioSetId = ScenarioSetIdentity();
        if (!string.IsNullOrEmpty(Options.ResumeDirectory))
        {
            var old = ChaosJson.Read<ChaosReport>(Path.Combine(Options.ResumeDirectory, "report.json"));
            ChaosJson.RequireIdentity(old, Report.BuildId, Report.ContentId);
            if (old.ScenarioSetId != Report.ScenarioSetId) throw new InvalidDataException("Scenario/fixture files changed; resume refused.");
            // Each completed scenario is sealed independently, even if the overall process was interrupted.
            foreach (string casePath in Directory.GetFiles(Options.ResumeDirectory, "case-result.json", SearchOption.AllDirectories))
            {
                var completed = ChaosJson.Read<ChaosReport>(casePath);
                ChaosJson.RequireIdentity(completed, Report.BuildId, Report.ContentId);
                if (completed.ScenarioSetId != Report.ScenarioSetId) throw new InvalidDataException("Completed case scenario version changed.");
                string directory = Path.GetDirectoryName(casePath);
                ChaosJson.VerifyArtifacts(directory, completed);
                var fixture = ChaosJson.Read<ChaosFixture>(Path.Combine(directory, "fixture.json")); fixture.Validate();
                var evidence = ChaosJson.Read<ChaosEvidence>(Path.Combine(directory, "evidence.json"));
                ChaosCatalog.Record(Report.Coverage, evidence);
                foreach (string line in File.ReadLines(Path.Combine(directory, "observations.jsonl")))
                {
                    var receipt = JsonUtility.FromJson<ChaosEvidence>(line);
                    if (!string.IsNullOrEmpty(receipt.CaseId)) ChaosCatalog.Record(Report.Coverage, receipt);
                }
                foreach (var row in Report.Coverage.Participation)
                { var previous = completed.Coverage.Participation.FirstOrDefault(p => p.Id == row.Id && p.Kind == row.Kind); row.Exercised = Math.Max(row.Exercised, previous?.Exercised ?? 0); }
                // Preserve the evidence chain in the new report as well as its source location.
                string imported = Path.Combine(DirectoryPath, "resumed", "case-" + ChaosJson.Hash(evidence.CaseId).Substring(0, 16) + "-" + ChaosJson.FileHash(casePath).Substring(0, 12));
                if (Directory.Exists(imported)) { ChaosJson.VerifyArtifacts(imported, completed); continue; }
                Directory.CreateDirectory(imported);
                foreach (var artifact in completed.Artifacts)
                {
                    string destination = Path.Combine(imported, artifact.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(Path.Combine(directory, artifact.Path), destination, false);
                }
                File.Copy(casePath, Path.Combine(imported, "case-result.json"), false);
            }
        }
        if (!string.IsNullOrEmpty(Options.ReplayDirectory)) { yield return Replay(); yield break; }
        if (Options.Mode == ChaosMode.Full) { yield return FullSuite(); yield break; }
        for (int run = 0; run < Options.RunCount; run++)
        {
            var entries = new[] { "Town", "DungeonScene", "Overworld" };
            foreach (string entry in entries)
            {
                if (!Matches(entry, Options.ScenarioFilter)) continue;
                int seed = ChaosRandom.Derive(checked(Options.Seed + run), entry);
                var fixture = ChaosFixtures.Create(seed, TownSceneLoader.Default, Common.Instance.ItemManager, entry, previousComposition);
                previousComposition = fixture.Composition;
                yield return Prepare(fixture);
                double until = Now + Options.DurationSeconds / entries.Count(e => Matches(e, Options.ScenarioFilter));
                yield return LoadThroughMenu(fixture.Entry, SelectDevice(run));
                yield return DirectedMenus(SelectDevice(run));
                var random = new ChaosRandom(fixture.InputSeed);
                progress.Reset(Now);
                while (Now < until)
                {
                    CheckStop();
                    var before = ChaosSnapshot.Read();
                    progress.Observe(before.ProgressHash, Now);
                    if (before.Loading || !before.Ready) { yield return Wait(() => !ChaosSnapshot.Read().Loading && ChaosSnapshot.Read().Ready, "scene transition", Options.LoadTimeoutSeconds); continue; }
                    if (!before.CanControl) { yield return Wait(() => ChaosSnapshot.Read().CanControl, "action completion", Options.ActionTimeoutSeconds); continue; }
                    if (progress.Expired(Now, Options.ActionTimeoutSeconds)) { yield return Recover("No meaningful progress", before); progress.Reset(Now); }
                    if (inputIndex % 24 == 0) yield return DirectedMenus(SelectDevice(inputIndex / 24));
                    else yield return Pulse(RandomInput(random, SelectDevice(inputIndex / 8)));
                }
                CloseSegment();
            }
        }
        if (segmentIndex == 0) throw new InvalidOperationException("No smoke scenarios match the filter.");
    }

    private IEnumerator Prepare(ChaosFixture fixture)
    {
        prepared = false; devices.Release(); CloseSegment();
        ChaosSnapshot.ResetActors();
        if (Common.InstanceOrNull != null) CampaignParty.ClearLiveParty(Common.Instance);
        var loading = SceneManager.LoadSceneAsync("MainMenu");
        yield return Wait(() => loading.isDone, "fixture scene unload", Options.LoadTimeoutSeconds);
        var common = Common.Instance;
        common.CampaignContext = null; common.GameSaveData = null; common.CurrentTownConfiguration = null;
        common.PendingDemoLoadout = null;
        yield return Wait(() => FindFirstObjectByType<MainMenu>()?.IsReady == true, "fixture menu", Options.LoadTimeoutSeconds);
        fixture.Validate();
        store.ClearAll(); store.Write(0, fixture.SaveJson);
        SaveSystem.ActiveSlot = 0;
        UnityEngine.Random.InitState(fixture.Seed);
        if (!string.IsNullOrEmpty(fixture.UnityRandomState)) UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(fixture.UnityRandomState);
        else { fixture.UnityRandomState = JsonUtility.ToJson(UnityEngine.Random.state); fixture.Seal(); }
        segment = Path.Combine(DirectoryPath, "scenario-" + (++segmentIndex).ToString("D4")); Directory.CreateDirectory(segment);
        ChaosJson.Write(Path.Combine(segment, "fixture.json"), fixture);
        Report.FixtureHashes.Add(fixture.Hash);
        foreach (var hero in fixture.Party)
            foreach (var row in Report.Coverage.Participation.Where(p => p.Kind == "Hero" && p.Id == hero.AllyId ||
                p.Kind == "Class" && (p.Id == hero.PrimaryClassId || p.Id == hero.SecondaryClassId))) row.Prepared++;
        inputs = new StreamWriter(Path.Combine(segment, "inputs.jsonl")) { AutoFlush = true };
        checkpoints = new StreamWriter(Path.Combine(segment, "checkpoints.jsonl")) { AutoFlush = true };
        observations = new StreamWriter(Path.Combine(segment, "observations.jsonl")) { AutoFlush = true };
        inputIndex = 0; turnCount = commandCount = startedCasts = releasedCasts = 0; castEvents.Clear();
        prepared = true; WriteReport();
    }
    private IEnumerator LoadThroughMenu(string entry, string device)
    {
        if (entry == "MainMenu") yield break;
        var menu = FindFirstObjectByType<MainMenu>();
        var before = ChaosSnapshot.Read();
        yield return Activate(menu.ContinueButton.GetComponent<Selectable>(), device);
        yield return Activate(menu.CampaignStartMenu.CampaignSlotViews[0].SelectToActivateButton, device);
        yield return Wait(() => SceneManager.GetActiveScene().name == entry && ChaosSnapshot.Read().CanControl, "load " + entry, Options.LoadTimeoutSeconds);
        Feature("campaign-load", device, before, ChaosSnapshot.Read());
        Feature("scene-transition", device, before, ChaosSnapshot.Read());
        progress.Reset(Now);
    }
    private IEnumerator DirectedMenus(string device)
    {
        var before = ChaosSnapshot.Read();
        if (before.Loading || !before.Ready || !before.CanControl) yield break;
        // Cancel through nested scopes until world input is available, without forcing focus.
        for (int i = 0; i < 8 && MenusOpen(); i++)
            yield return Pulse(Button("Escape", device));
        if (SceneManager.GetActiveScene().name == "MainMenu") yield break;
        yield return Pulse(Button("Escape", device));
        if (Common.Instance.GlobalSettings.IsOpen)
        {
            yield return Activate(Common.Instance.GlobalSettings.ResumeButton, device);
            if (!Common.Instance.GlobalSettings.IsOpen && ChaosSnapshot.Read().CanControl) Feature("settings", device, before, ChaosSnapshot.Read());
        }
        foreach (var shortcut in new[] { "Q", "R" })
        {
            yield return Pulse(Button(shortcut, device));
            if (MenusOpen())
            {
                var visible = ChaosUI.Visible();
                if (visible.Length > 0)
                {
                    // Inspect the first inventory/skill entry and cancel the nested dialog.
                    var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
                    if (selected != null) yield return Activate(selected, device);
                    yield return Pulse(Button("Escape", device));
                }
                for (int i = 0; i < 6 && MenusOpen(); i++) yield return Pulse(Button("Escape", device));
                if (!MenusOpen() && ChaosSnapshot.Read().CanControl)
                    Feature("nested-dialogs", device, before, ChaosSnapshot.Read());
            }
        }
    }
    private IEnumerator Activate(Selectable target, string device)
    {
        if (target == null || !target.IsActive() || !target.IsInteractable()) throw new InvalidOperationException("Requested UI target is unavailable.");
        double deadline = Now + Options.ActionTimeoutSeconds;
        if (device == "Mouse")
        {
            yield return Pulse(new ChaosInput { Device = "Mouse", Position = ChaosUI.Center(target), Label = "point:" + ChaosUI.Path(target.transform) });
            if (target is SelectToActivateButton button && !button.ActivateOnFirstPointerPress && EventSystem.current?.currentSelectedGameObject != target.gameObject)
                yield return Pulse(ChaosInput.Click(ChaosUI.Center(target), "focus:" + ChaosUI.Path(target.transform)));
            yield return Pulse(ChaosInput.Click(ChaosUI.Center(target), "activate:" + ChaosUI.Path(target.transform))); yield break;
        }
        while (EventSystem.current?.currentSelectedGameObject != target.gameObject)
        {
            if (Now >= deadline) { yield return Recover("Cannot navigate to " + ChaosUI.Path(target.transform), ChaosSnapshot.Read()); throw new TimeoutException("Directed navigation failed."); }
            var current = EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
            yield return Pulse(Button(ChaosUI.NextDirection(current, target), device));
        }
        yield return Pulse(Button("Enter", device));
    }
    private IEnumerator Pulse(ChaosInput input, string expectedAfter = null)
    {
        CheckStop();
        if (!prepared) throw new InvalidOperationException("Input sent before fixture preparation.");
        // A random pixel can hit an excluded developer/quit control even when the candidate list is filtered.
        // Record the neutral replacement, so replay dispatches exactly the same input.
        string focused = ChaosUI.Label(EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>());
        if (ChaosUI.Forbidden(focused) && ((input.Keys?.Contains("Enter") ?? false) || (input.Buttons?.Contains("South") ?? false)) ||
            (input.MouseButtons & 1) != 0 && ChaosUI.ForbiddenPoint(input.Position))
            input = new ChaosInput { Device = input.Device, HoldFrames = input.HoldFrames, SettleFrames = input.SettleFrames, Label = "excluded-control" };
        var before = ChaosSnapshot.Read();
        input.Before = before.StateHash; input.Index = inputIndex++; input.Seconds = Elapsed;
        inputs.WriteLine(JsonUtility.ToJson(input));
        devices.Send(input); Report.Inputs++;
        for (int frame = 0; frame < input.HoldFrames; frame++) { CheckStop(); yield return null; }
        devices.Release();
        for (int frame = 0; frame < input.SettleFrames; frame++) { CheckStop(); yield return null; }
        var after = ChaosSnapshot.Read();
        checkpoints.WriteLine(JsonUtility.ToJson(new Checkpoint { Index = input.Index, Hash = after.StateHash, Snapshot = after }));
        if (before.Position != after.Position && !string.IsNullOrEmpty(after.ControlledHero)) Participate(after.ControlledHero);
        if (expectedAfter != null && expectedAfter != after.StateHash) throw new ReplayDivergence("Checkpoint " + input.Index + " expected " + expectedAfter + ", observed " + after.StateHash);
    }
    private IEnumerator Wait(Func<bool> condition, string description, double limit)
    {
        double deadline = Now + limit;
        while (!condition())
        {
            CheckStop();
            if (Now >= deadline)
            {
                if (prepared) yield return Recover("Deadline exceeded: " + description, ChaosSnapshot.Read());
                if (!condition()) throw new TimeoutException(description);
                yield break;
            }
            if (prepared) yield return Pulse(new ChaosInput { HoldFrames = 1, SettleFrames = 1, Label = "wait:" + description });
            else yield return null;
        }
    }
    private IEnumerator Recover(string reason, ChaosSnapshot before)
    {
        var issue = AddIssue("SuspectedLock", reason, null); issue.Before = JsonUtility.ToJson(before);
        issue.Screenshot = Path.Combine(segment, "lock-" + Report.Issues.Count + ".png");
        ScreenCapture.CaptureScreenshot(issue.Screenshot);
        double captureDeadline = Now + 15;
        while (!File.Exists(issue.Screenshot) && Now < captureDeadline) yield return null;
        if (!File.Exists(issue.Screenshot)) throw new IOException("Failed to capture suspected lock before recovery.");
        double deadline = Now + Options.RecoverySeconds;
        while (Now < deadline)
        {
            yield return Pulse(Button("Escape", SelectDevice(inputIndex)));
            yield return Pulse(Button("DownArrow", SelectDevice(inputIndex)));
            var after = ChaosSnapshot.Read();
            if (after.CanControl && (after.Dialog != before.Dialog || after.Position != before.Position || after.Selected != before.Selected && !string.IsNullOrEmpty(after.Selected)))
            { issue.Kind = "RecoveredNavigationFailure"; issue.After = JsonUtility.ToJson(after); WriteReport(); yield break; }
        }
        issue.Kind = "Softlock"; issue.After = JsonUtility.ToJson(ChaosSnapshot.Read());
        throw new TimeoutException("Unrecovered input-only softlock: " + reason);
    }
    private string SelectDevice(int index) => Options.Device == ChaosDevice.Mixed ? ChaosCatalog.Devices[Math.Abs(index % 3)] : Options.Device.ToString();
    private static bool Matches(string value, string filter) => string.IsNullOrEmpty(filter) || value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    private ChaosInput Button(string key, string device)
    {
        if (device == "Controller") return ChaosInput.Pad(key switch {
            "Escape" => MenusOpen() ? GamepadButton.East : GamepadButton.Start,
            "Enter" => GamepadButton.South, "Q" => GamepadButton.West, "R" => GamepadButton.LeftShoulder,
            "UpArrow" => GamepadButton.DpadUp, "DownArrow" => GamepadButton.DpadDown,
            "LeftArrow" => GamepadButton.DpadLeft, "RightArrow" => GamepadButton.DpadRight, _ => GamepadButton.North }, key);
        if (device == "Mouse")
        {
            string label = key switch { "Escape" => "Back", "Q" => "Inventory", "R" => "Skills", _ => "" };
            var target = ChaosUI.Visible().FirstOrDefault(s => !string.IsNullOrEmpty(label) && ChaosUI.Label(s).IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
            if (target != null) return ChaosInput.Click(ChaosUI.Center(target), label);
            // Mouse cancellation uses the production right-click binding, never a keyboard fallback.
            return new ChaosInput { Device = "Mouse", Position = new Vector2(Screen.width / 2f, Screen.height / 2f), MouseButtons = 2, Label = "right-click:" + key };
        }
        return ChaosInput.Key((Key)Enum.Parse(typeof(Key), key));
    }
    private bool MenusOpen() => Common.InstanceOrNull?.GlobalSettings.IsOpen == true ||
        Game.Instance != null && MenuManager.Instance?.Opened == true || FindFirstObjectByType<TownMenuManager>()?.Opened == true ||
        FindFirstObjectByType<OverworldMenuManager>()?.Opened == true;
    private ChaosInput RandomInput(ChaosRandom random, string device)
    {
        int choice = random.Range(12);
        ChaosInput input;
        if (device == "Mouse")
        {
            var targets = ChaosUI.Visible().Where(s => !ChaosUI.Forbidden(ChaosUI.Label(s))).ToArray();
            Vector2 point = choice < 8 && targets.Length > 0 ? ChaosUI.Center(targets[random.Range(targets.Length)]) : new Vector2(random.Range(Math.Max(1, Screen.width)), random.Range(Math.Max(1, Screen.height)));
            input = ChaosInput.Click(point); if (choice == 10) input.MouseButtons = 2;
            if (choice == 11) { input.MouseButtons = 0; input.Scroll = new Vector2(0, random.Range(2) == 0 ? 120 : -120); }
        }
        else if (device == "Controller")
        {
            input = Button(new[] { "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Enter", "Escape", "Q", "R" }[random.Range(8)], device);
            if (choice >= 8) { input.Buttons = Array.Empty<string>(); input.Stick = new Vector2(random.Range(3) - 1, random.Range(3) - 1); }
            if (choice == 11) input.Buttons = new[] { "DpadUp", "DpadDown", "DpadLeft", "DpadRight" };
        }
        else
        {
            input = ChaosInput.Key(new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Enter, Key.Escape, Key.Q, Key.R, Key.E, Key.Period, Key.I, Key.T }[random.Range(12)]);
            if (choice == 11) input.Keys = new[] { "UpArrow", "DownArrow", "LeftArrow", "RightArrow" };
            if (choice == 10) input.Keys = new[] { "Enter", "Escape" };
        }
        // Quitting or launching another diagnostic is outside a scenario. Read the focused label before submitting.
        string focused = ChaosUI.Label(EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>());
        if (ChaosUI.Forbidden(focused) && (input.Keys.Contains("Enter") || input.Buttons.Contains("South"))) input = Button("Escape", device);
        input.HoldFrames = choice == 9 ? 15 + random.Range(45) : 2;
        input.SettleFrames = 4 + random.Range(12);
        return input;
    }
    private void Feature(string name, string device, ChaosSnapshot before, ChaosSnapshot after)
    {
        var row = Report.Coverage.Cases.Single(c => c.Kind == "Feature" && c.Ability == name && c.Device == device);
        var evidence = new ChaosEvidence { CaseId = row.Id, BeforeHash = before.StateHash, AfterHash = after.StateHash,
            Invoked = true, EffectVerified = true, ControlRestored = after.CanControl, Oracle = name + ": directed UI transition and control return" };
        ChaosCatalog.Record(Report.Coverage, evidence);
        observations.WriteLine(JsonUtility.ToJson(evidence));
    }
    private void Participate(string heroId)
    {
        var hero = TownSceneLoader.Default.AllyCatalog.FirstOrDefault(h => h.Id == heroId);
        foreach (var row in Report.Coverage.Participation.Where(p => p.Kind == "Hero" && p.Id == heroId || p.Kind == "Class" &&
            (p.Id == hero?.PrimaryClass?.Id || p.Id == hero?.SecondaryClass?.Id))) row.Exercised++;
    }
    private void OnObservation(string kind, Character actor, Skill skill)
    {
        if (!prepared || finishing) return;
        if (kind == "TurnStarted") turnCount++;
        if (kind == "CommandAccepted") { commandCount++; if (actor is Ally ally) Participate(ally.TownAllyId); }
        if (kind == "SkillStarted") startedCasts++;
        if (kind == "SkillReleased") releasedCasts++;
        if (skill != null) castEvents.Add(kind + "/" + (actor is Ally hero ? hero.TownAllyId : actor?.CharacterName) + "/" + skill.SkillName + "/" + skill.Rank);
        try { observations?.WriteLine(JsonUtility.ToJson(new Observation { Kind = kind, Actor = actor is Ally a ? a.TownAllyId : actor?.CharacterName,
            Skill = skill?.SkillName, Rank = skill?.Rank ?? 0, Seconds = Elapsed })); }
        catch (Exception error) { reportingError = error.ToString(); }
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (disposed) return;
        try { logs?.WriteLine(JsonUtility.ToJson(new ChaosIssue { Kind = type.ToString(), Message = message, Stack = stack, Seconds = Elapsed })); }
        catch (Exception error) { reportingError ??= error.ToString(); }
        if (type is LogType.Error or LogType.Exception or LogType.Assert) pendingError ??= message + "\n" + stack;
    }
    private void CheckStop()
    {
        if (stop) throw new OperationCanceledException("Stopped by user");
        if (reportingError != null) throw new IOException("Reporting failed: " + reportingError);
        if (pendingError != null) throw new InvalidOperationException("Unity error: " + pendingError);
    }
    public void Stop() { stop = true; devices?.Release(); }
    private ChaosIssue AddIssue(string kind, string message, string stack)
    { var issue = new ChaosIssue { Kind = kind, Message = message, Stack = stack, Seconds = Elapsed }; Report.Issues.Add(issue); return issue; }
    private void Update()
    {
        if (disposed || Report == null || Now < heartbeatAt) return;
        heartbeatAt = Now + 1;
        try
        {
            ChaosJson.Write(Path.Combine(DirectoryPath, "heartbeat.json"), new Heartbeat { Utc = DateTime.UtcNow.ToString("O"),
                State = Report.State, Frame = Time.frameCount, Inputs = Report.Inputs, Seconds = Elapsed, ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id });
            Status = Report.State + " · " + (prepared ? ChaosSnapshot.Read().Scene : "preparing") + " · " + Report.Inputs + " inputs";
        }
        catch (Exception error) { reportingError = error.ToString(); }
    }
    private void WriteReport() { Report.Seconds = Elapsed; ChaosJson.Write(Path.Combine(DirectoryPath, "report.json"), Report); }
    private void CloseSegment()
    {
        prepared = false;
        inputs?.Dispose(); checkpoints?.Dispose(); observations?.Dispose(); inputs = checkpoints = observations = null;
    }
    private void OnDestroy()
    {
        if (!disposed)
        {
            if (!finishing && Report != null)
            {
                Report.State = "Interrupted"; Report.ArtifactsComplete = Report.SmokePassed = Report.FullPassed = false;
                Report.Reason = "Process/Play Mode ended before finalization";
                try { WriteReport(); } catch (Exception error) { Debug.LogWarning("Chaos could not write interrupted report: " + error.Message); }
            }
#if UNITY_EDITOR
            // Scene OnDestroy callbacks can still write. Retain the isolated save through all of them.
            var deferred = saveScope; saveScope = null;
            void RestoreAfterExit(UnityEditor.PlayModeStateChange state)
            {
                if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
                UnityEditor.EditorApplication.playModeStateChanged -= RestoreAfterExit;
                deferred?.Dispose();
            }
            UnityEditor.EditorApplication.playModeStateChanged += RestoreAfterExit;
#else
            saveScope = null; // The process is exiting; keep isolation through remaining destruction callbacks.
#endif
        }
        Dispose();
    }
    private void OnApplicationQuit() => OnDestroy();
    private void Dispose()
    {
        if (disposed) return; disposed = true;
        Application.logMessageReceived -= OnLog; GameInputDiagnostics.Observed -= OnObservation;
        try { CloseSegment(); logs?.Dispose(); } finally
        {
            try { devices?.Dispose(); } finally { preferences?.Dispose(); saveScope?.Dispose(); }
            UnityEngine.Random.state = originalRandom; Time.timeScale = originalTimeScale;
            DungeonPreferences.FullControlOverride = originalFullControl; DungeonPreferences.AnimationOverride = originalAnimation;
            foreach (var audio in audioState) if (audio.mixer != null) audio.mixer.SetFloat(audio.parameter, audio.value);
            if (!Application.isEditor) Screen.SetResolution(originalWidth, originalHeight, originalMode);
            ChaosSessionLock.Held = false; if (Active == this) Active = null;
#if UNITY_EDITOR
            if (reloadLocked) { UnityEditor.EditorApplication.UnlockReloadAssemblies(); reloadLocked = false; }
#endif
        }
    }
    [Serializable] private sealed class Location { public string Directory; }
    [Serializable] private sealed class Heartbeat { public string Utc, State; public int ProcessId, Frame, Inputs; public double Seconds; }
    [Serializable] private sealed class Checkpoint { public int Index; public string Hash; public ChaosSnapshot Snapshot; }
    [Serializable] private sealed class Observation { public string Kind, Actor, Skill; public int Rank; public double Seconds; }
    private sealed class ReplayDivergence : Exception { public ReplayDivergence(string message) : base(message) { } }
}

public sealed class ChaosSaveStore : ISaveStore
{
    private readonly string[] slots = new string[SaveSystem.SlotCount];
    public string Read(int slot) { SaveSystem.ValidateSlot(slot); return slots[slot]; }
    public void Write(int slot, string json) { SaveSystem.ValidateSlot(slot); slots[slot] = json; }
    public void Clear(int slot) { SaveSystem.ValidateSlot(slot); slots[slot] = null; }
    public void ClearAll() { for (int i = 0; i < slots.Length; i++) slots[i] = null; }
}
#endif
