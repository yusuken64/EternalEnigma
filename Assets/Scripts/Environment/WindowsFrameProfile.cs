#if UNITY_STANDALONE_WIN && (UNITY_EDITOR || DEVELOPMENT_BUILD || PC_FRAME_PROFILE)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// Explicit Windows player benchmark. Uses the normal backbuffer, game camera, HUD and turn playback.
public sealed class WindowsFrameProfile : MonoBehaviour
{
    const int Seed = 42;
    const int DefaultCommands = 24;
    static readonly Vector3Int[] Steps = { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down };
    static readonly HashSet<string> CounterNames = new()
    {
        "Main Thread", "Render Thread", "GPU Frame Time", "Camera.Render", "BehaviourUpdate",
        "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
        "Canvas.BuildBatch", "Draw Calls Count", "Triangles Count", "SetPass Calls Count",
        "GC Allocated In Frame", "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS"
    };

    [Serializable] sealed class Counter { public string name, unit; public double mean; }
    [Serializable] sealed class SlowFrame
    {
        public int action;
        public double elapsedMs, frameMs, inputMs;
    }
    [Serializable] sealed class CommandFrame
    {
        public int action;
        public double frameMs, inputMs, submitMs;
    }
    [Serializable] sealed class ActorState
    {
        public string character;
        public int x, y, hp, mana, hunger;
    }
    [Serializable] sealed class Sample
    {
        public string condition;
        public int frames, renderedFrames, actions, moves, attacks, menusOpened, heroesBrowsed, overBudget, enemyHpBefore, enemyHpAfter;
        public List<CommandFrame> commands = new();
        public List<SlowFrame> slowFrames = new();
        public List<ActorState> party = new(), enemies = new();
        public FinalePerformance.Sample[] gameplayScopes;
        public double fps, medianMs, p95Ms, p99Ms, worstMs;
        public List<Counter> counters = new();
    }
    [Serializable] sealed class Report
    {
        public string scene, location, cpu, gpu, api, unity, quality;
        public int seed = Seed, width, height, vSync, partySize, floor, enemies;
        public float masterVolume, listenerVolume;
        public double loadSeconds;
        public List<Sample> samples = new();
    }

    sealed class ProfileStore : ISaveStore
    {
        readonly string[] slots = new string[SaveSystem.SlotCount];
        public string Read(int slot) => slots[slot];
        public void Write(int slot, string data) => slots[slot] = data;
        public void Clear(int slot) => slots[slot] = null;
    }

    string scene, path;
    int commandCount = DefaultCommands;
    double submitMs;
    bool IsOverworld => scene is "overworld" or "overworld-party";
    bool IsCombat => scene is "dungeon-combat" or "dungeon-ambush";
    bool breakdown, paced;
    Camera view;
    OverworldScene world;
    Game game;
    int rendered;
    string playerError;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Application.isEditor) return;
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--pc-frame-profile");
        if (index < 0) return;
        try
        {
            if (index + 2 >= args.Length || args[index + 1] is not ("overworld" or "overworld-party" or "dungeon" or "dungeon-combat" or "dungeon-ambush"))
                throw new ArgumentException("Expected --pc-frame-profile overworld|overworld-party|dungeon|dungeon-combat|dungeon-ambush <output.json>");
            int count = DefaultCommands;
            int countIndex = Array.IndexOf(args, "--frame-commands");
            if (countIndex >= 0 && (countIndex + 1 >= args.Length || !int.TryParse(args[countIndex + 1], out count) || count < 1 || count > 240))
                throw new ArgumentException("--frame-commands must be between 1 and 240");
            // Keep both stores isolated through every scene's shutdown callbacks.
            GamePreferences.Isolate();
            if (args.Contains("--frame-muted"))
            {
                AudioListener.volume = 0;
                GamePreferences.SetFloat(AudioPreferences.Key("MasterVolume"), 0);
            }
            SaveSystem.UseStore(new ProfileStore());
            var host = new GameObject("Windows frame profile");
            DontDestroyOnLoad(host);
            var profile = host.AddComponent<WindowsFrameProfile>();
            profile.scene = args[index + 1];
            profile.commandCount = count;
            profile.path = Path.GetFullPath(args[index + 2]);
            profile.breakdown = args.Contains("--frame-breakdown");
            profile.paced = args.Contains("--frame-paced");
        }
        catch (Exception error) { Debug.LogException(error); Application.Quit(2); }
    }

    IEnumerator Start()
    {
        Application.logMessageReceived += CaptureError;
        Camera.onPostRender += CountRender;
        using var run = Run();
        while (true)
        {
            bool more;
            try
            {
                if (playerError != null) throw new InvalidOperationException(playerError);
                more = run.MoveNext();
            }
            catch (Exception error)
            {
                Debug.LogError("Windows frame profile failed: " + error);
                Application.Quit(1);
                yield break;
            }
            if (!more) break;
            yield return run.Current;
        }
        Application.Quit(0);
    }

    void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            playerError ??= message + "\n" + stack;
    }
    void CountRender(Camera camera) { if (camera == view) rendered++; }
    void OnDestroy() { Camera.onPostRender -= CountRender; Application.logMessageReceived -= CaptureError; }

    IEnumerator<object> Run()
    {
        Application.runInBackground = true;
        if (!paced) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
        double start = Time.realtimeSinceStartupAsDouble;
        while (FindFirstObjectByType<MainMenu>()?.IsReady != true || Common.Instance.ScreenTransition.BlocksInput)
        {
            if (Time.realtimeSinceStartupAsDouble - start > 120) throw new TimeoutException("Main menu");
            yield return null;
        }
        var common = Common.Instance;
        UnityEngine.Random.InitState(Seed);
        var save = EndgameDebugStart.Create(TownSceneLoader.Default, common.ItemManager, EndgameDebugDestination.Overworld, Seed);
        if (!IsOverworld)
        {
            var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, Seed), save.Campaign);
            var encounter = scene == "dungeon-ambush" ? EncounterKind.Ambush : EncounterKind.BossArena;
            var location = IsCombat
                ? context.Campaign.Locations.Where(l => l.Encounter?.Kind == encounter).OrderByDescending(l => l.Tier).First()
                : context.Campaign.Locations.First(l => l.Kind == LocationKind.StoryDungeon && l.Tier > 0);
            var plan = context.RunPlan(location.Id);
            save.Campaign.PendingDungeon = save.Campaign.LocationId = location.Id;
            save.Campaign.Scene = "DungeonScene";
            save.DungeonSaveData = new DungeonSaveData { StartFloor = plan.StartFloor, EndFloor = plan.EndFloor,
                Variant = plan.Variant, UseBiomeLayout = true, LayoutTier = location.Tier,
                LayoutBiome = OverworldGridGenerator.BiomeForRegion(context.Campaign, location.RegionId) };
        }
        common.GameSaveData = save;
        start = Time.realtimeSinceStartupAsDouble;
        common.Travel.Continue();
        while (true)
        {
            if (IsOverworld && world == null) world = FindFirstObjectByType<OverworldScene>();
            if (!IsOverworld && game == null) game = Game.RegisteredInstance;
            if (!common.Travel.IsTransitioning && (world != null && world.IsReady ||
                game != null && game.IsReady && !game.NewFloorMessage.gameObject.activeSelf)) break;
            if (Time.realtimeSinceStartupAsDouble - start > 180) throw new TimeoutException(scene + " ready");
            yield return null;
        }
        view = Camera.main;
        if (view == null || !view.enabled || view.targetTexture != null) throw new InvalidOperationException("Expected an active gameplay backbuffer camera");
        var report = new Report { scene = scene, location = save.Campaign.LocationId, gpu = SystemInfo.graphicsDeviceName,
            cpu = SystemInfo.processorType, api = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()], width = view.pixelWidth, height = view.pixelHeight,
            masterVolume = AudioPreferences.Read(common.AudioManager.MusicAudioMixerGroup, "MasterVolume"), listenerVolume = AudioListener.volume,
            floor = game != null ? game.PlayerController.Floor : 0, enemies = game != null ? game.Enemies.Count : 0,
            vSync = QualitySettings.vSyncCount, partySize = game != null ? game.Allies.Count : world.Followers.Count + 1,
            loadSeconds = Time.realtimeSinceStartupAsDouble - start };
        if (Environment.GetCommandLineArgs().Contains("--frame-muted") && (report.masterVolume != 0 || report.listenerVolume != 0))
            throw new InvalidOperationException("The requested zero-volume profile is audible");
        while (TerrainStreaming.Pending > 0)
        {
            if (Time.realtimeSinceStartupAsDouble - start > 180) throw new TimeoutException("Terrain streaming");
            yield return null;
        }
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var counters = handles.Select(ProfilerRecorderHandle.GetDescription).Where(d => CounterNames.Contains(d.Name)).ToArray();
        var silhouettes = view.GetComponent<SilhouetteRenderer>();
        bool hasSilhouettes = silhouettes != null && silhouettes.enabled;
        int originalMask = view.cullingMask;
        foreach (string condition in breakdown ? new[] { "idle", "silhouettes off", "empty world camera", "active" } : new[] { "idle", "active" })
        {
            if (hasSilhouettes) silhouettes.enabled = condition != "silhouettes off";
            view.cullingMask = condition == "empty world camera" ? 0 : originalMask;
            yield return new WaitForSecondsRealtime(1.5f);
            var sample = new Sample { condition = condition, enemyHpBefore = EnemyHp() };
            bool active = condition == "active";
            var times = new List<double>(8192);
            var recorders = counters.Select(d => ProfilerRecorder.StartNew(d.Category, d.Name)).ToArray();
            var sums = new double[recorders.Length];
            int renderStart = rendered;
            double last = Time.realtimeSinceStartupAsDouble, began = last, nextCommand = last;
            if (active) FinalePerformance.Begin();
            try
            {
                while (true)
                {
                    double inputMs = 0;
                    bool accepted = false;
                    submitMs = 0;
                    if (active && sample.actions < commandCount && last >= nextCommand)
                    {
                        double inputStart = Time.realtimeSinceStartupAsDouble;
                        accepted = Step(sample);
                        if (accepted) nextCommand = last + .35;
                        inputMs = (Time.realtimeSinceStartupAsDouble - inputStart) * 1000;
                    }
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    double frameMs = (now - last) * 1000;
                    times.Add(frameMs);
                    if (accepted) sample.commands.Add(new CommandFrame { action = sample.actions, frameMs = frameMs, inputMs = inputMs, submitMs = submitMs });
                    if (frameMs > 25 && sample.slowFrames.Count < 20)
                        sample.slowFrames.Add(new SlowFrame { action = sample.actions, elapsedMs = (now - began) * 1000, frameMs = frameMs, inputMs = inputMs });
                    last = now;
                    for (int i = 0; i < recorders.Length; i++)
                        if (recorders[i].Valid) sums[i] += recorders[i].LastValue;
                    if (active ? sample.actions == commandCount && now >= nextCommand &&
                        (world != null ? !world.IsMoving : !game.TurnManager.IsProcessingTurn) : now - began >= 5) break;
                    if (now - began > Math.Max(75, commandCount * 3)) throw new TimeoutException(condition + " after " + sample.actions + " actions");
                }
                for (int i = 0; i < recorders.Length; i++)
                    if (recorders[i].Valid && recorders[i].Count > 0) sample.counters.Add(new Counter {
                        name = counters[i].Name, unit = counters[i].UnitType.ToString(), mean = sums[i] / times.Count });
            }
            finally
            {
                if (active) sample.gameplayScopes = FinalePerformance.Finish();
                foreach (var recorder in recorders) recorder.Dispose();
            }
            sample.enemyHpAfter = EnemyHp();
            if (active) RecordOutcome(sample);
            times.Sort();
            double Percentile(double fraction) => times[(int)Math.Ceiling(fraction * times.Count) - 1];
            sample.frames = times.Count; sample.renderedFrames = rendered - renderStart;
            if (sample.renderedFrames < sample.frames - 1) throw new InvalidOperationException("Gameplay camera skipped sampled frames");
            sample.fps = 1000 / times.Average(); sample.medianMs = Percentile(.5); sample.p95Ms = Percentile(.95);
            sample.p99Ms = Percentile(.99); sample.worstMs = times[times.Count - 1];
            sample.overBudget = times.Count(ms => ms > 1000d / 60);
            report.samples.Add(sample);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (IsCombat && active && sample.attacks == 0 && sample.enemyHpAfter >= sample.enemyHpBefore)
                throw new InvalidOperationException("The combat route finished without engaging an enemy");
            Debug.Log($"PC_FRAME {scene} {condition}: {sample.fps:F1} fps, p95 {sample.p95Ms:F2} ms, moves {sample.moves}/{sample.actions}");
        }
    }

    int EnemyHp() => game == null ? 0 : game.Enemies.Where(e => e != null).Sum(e => Mathf.Max(0, e.Vitals.HP));

    void RecordOutcome(Sample sample)
    {
        if (world != null)
        {
            foreach (var hero in new[] { world.Player }.Concat(world.Followers))
                sample.party.Add(new ActorState { character = hero.Id, x = hero.TilemapPosition.x, y = hero.TilemapPosition.y,
                    hp = hero.Hp, mana = hero.Mana, hunger = hero.Hunger });
            return;
        }
        ActorState State(Character actor) => new() { character = actor.CharacterName, x = actor.TilemapPosition.x,
            y = actor.TilemapPosition.y, hp = actor.Vitals.HP, mana = actor.Vitals.Mana, hunger = actor.Vitals.Hunger };
        foreach (var ally in game.Allies) sample.party.Add(State(ally));
        foreach (var ally in game.DownedAllies) sample.party.Add(State(ally));
        foreach (var enemy in game.Enemies)
            if (enemy != null) sample.enemies.Add(State(enemy));
    }

    bool Step(Sample sample)
    {
        if (world != null && world.IsMoving) return false;
        if (scene == "overworld-party")
        {
            var menus = world.GetComponent<OverworldMenuManager>();
            switch (sample.actions % 6)
            {
                case 2:
                    menus.OpenPartyMenu(PartyMenuTab.Stats);
                    if (!menus.Opened) return false;
                    sample.menusOpened++;
                    sample.actions++;
                    return true;
                case 3:
                    if (!menus.PartyMenu.IsRoot) return false;
                    menus.PartyMenu.BrowseHero(1);
                    sample.heroesBrowsed++;
                    sample.actions++;
                    return true;
                case 4:
                    menus.PartyMenu.CloseDialog();
                    if (menus.Opened) return false;
                    sample.actions++;
                    return true;
            }
        }
        Ally ally = game != null ? game.PlayerController.ControlledAlly : null;
        if (game != null && (!ally.IsWaitingForPlayerInput || MenuManager.Instance.Opened)) return false;
        if (ally != null)
        {
            var attack = new AllyAttackPolicy(game, ally, 0);
            if (attack.ShouldRun() && Submit(ally, attack.GetActions()[0]))
            { sample.actions++; sample.attacks++; return true; }
            if (IsCombat && ApproachEnemy(ally))
            { sample.actions++; sample.moves++; return true; }
        }
        for (int i = 0; i < Steps.Length; i++)
        {
            var step = Steps[(sample.actions / 6 + i) % Steps.Length];
            if (world != null)
            {
                var next = new GridPoint(world.Position.X + step.x, world.Position.Y + step.y);
                if (!world.CanStep(world.Position, next) || !world.TryMove(step.x, step.y)) continue;
            }
            else
            {
                var from = ally.TilemapPosition;
                var to = from + step;
                var floor = game.CurrentDungeon;
                if (to == floor.GetStairPosition() || !floor.CanWalkTo(from, to, ally.FootPrint) ||
                    floor.OverlapsAnyOtherCharacter(ally, Character.ToBounds(to)) != null ||
                    !Submit(ally, new MovementAction(ally, from, to))) continue;
            }
            sample.actions++; sample.moves++; return true;
        }
        if (game != null && Submit(ally, new WaitAction())) { sample.actions++; return true; }
        return false;
    }

    bool ApproachEnemy(Ally ally)
    {
        var target = game.Enemies.Where(e => e != null && e.Vitals.HP > 0 && e.Team != ally.Team)
            .OrderBy(e => TileWorldDungeon.ChevDistance(ally.TilemapPosition, e.TilemapPosition)).FirstOrDefault();
        if (target == null) return false;
        ally.PursuitPosition = target.TilemapPosition;
        var path = ally.CalculatePursuitPath();
        if (path == null || path.Count == 0) return false;
        var next = new Vector3Int(path[0].X, path[0].Y);
        var floor = game.CurrentDungeon;
        if (next == floor.GetStairPosition() || !floor.CanWalkTo(ally.TilemapPosition, next, ally.FootPrint) ||
            floor.OverlapsAnyOtherCharacter(ally, Character.ToBounds(next)) != null) return false;
        return Submit(ally, new MovementAction(ally, ally.TilemapPosition, next));
    }

    bool Submit(Ally ally, GameAction action)
    {
        double start = Time.realtimeSinceStartupAsDouble;
        bool accepted = game.TurnManager.SubmitCommand(ally, action);
        submitMs += (Time.realtimeSinceStartupAsDouble - start) * 1000;
        return accepted;
    }
}
#endif
