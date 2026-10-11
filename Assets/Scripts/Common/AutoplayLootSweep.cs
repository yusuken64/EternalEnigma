using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class AutoplayRunner
{
    public bool IsLootSweep => Options?.Mode == AutoplayMode.FloorLootSweep;
    public string ProgressLabel => $"{(IsLootSweep ? "Floor" : "Run")} {Math.Min(RunNumber, Options.RunCount)}/{Options.RunCount} · Seed {CurrentSeed}";
    public AutoplaySweepManifest Manifest { get; private set; }
    internal AutoplayLootLedger LootLedger { get; private set; }
    internal AutoplayFloorCase FloorCase => IsLootSweep && Manifest != null && RunNumber <= Manifest.Cases.Count ? Manifest.Cases[RunNumber - 1] : null;
    internal static bool Sweeping => Active != null && Active.IsLootSweep && !Active.PlayerControlled;
    internal static DungeonFloorOptions SweepGeneration => Sweeping ? Active.FloorCase?.Options() : null;
    private string sweepBuild, sweepContent;
    private readonly HashSet<string> finalizedCases = new();
    private readonly HashSet<Vector3Int> sweepVisited = new();
    private readonly Dictionary<int, int> sweepLowHP = new();
    private bool loadingSweep, sweepInspected;
    private int sweepCollected, sweepOpened;
    private Interactable sweepObjective;

    private void InitializeSweep()
    {
        if (!IsLootSweep) return;
        Options.DebugPlaythrough = Options.Godmode = true;
        Options.InfiniteResources = Options.DebugCoverage = Options.ExploreAll = false;
        sweepBuild = AutoplaySweepManifest.CurrentBuildId;
        sweepContent = AutoplaySweepManifest.CurrentContentId();
        if (!string.IsNullOrWhiteSpace(Options.ResumeManifestPath))
        {
            var path = Path.GetFullPath(Options.ResumeManifestPath);
            Manifest = JsonUtility.FromJson<AutoplaySweepManifest>(File.ReadAllText(path));
            Manifest.Validate();
            BatchDirectoryPath = Path.GetDirectoryName(path);
            Options.Seed = Manifest.StartingSeed; Options.RunCount = Manifest.Cases.Count;
            // Per-case reports are authoritative. A process can exit between writing a
            // finalized report and replacing the aggregate batch file.
            foreach (var attempt in AutoplaySweepManifest.ReadAttempts(BatchDirectoryPath))
                if (attempt.Report.ManifestId == Manifest.Id)
                    Batch.Runs.Add(AutoplayBatchRun.FromReport(attempt.Report, Path.GetFileName(attempt.Directory)));
            finalizedCases.UnionWith(Manifest.FinalizedCases(BatchDirectoryPath, sweepBuild, sweepContent));
        }
        else
        {
            Manifest = AutoplaySweepManifest.Create(Options, Resources.Load<DungeonThemeCatalog>("DungeonThemes/Catalog"), sweepBuild, sweepContent);
            Directory.CreateDirectory(BatchDirectoryPath);
            AutoplaySweepManifest.WriteJson(Path.Combine(BatchDirectoryPath, "manifest.json"), Manifest);
        }
        Batch.Mode = Options.Mode; Batch.ManifestId = Manifest.Id; Batch.BuildId = sweepBuild; Batch.ContentId = sweepContent;
        Batch.StartingSeed = Options.Seed; Batch.RequestedRuns = Options.RunCount;
        Batch.State = "Running"; Batch.EndedUtc = Batch.Reason = null;
        RunNumber = NextSweepCase(1);
        if (RunNumber > Options.RunCount)
        {
            // A no-op resume remains reviewable without creating a fabricated attempt.
            RunNumber = Options.RunCount;
            var row = Batch.Runs.Last(r => r.CaseId == FloorCase.Id && r.BuildId == sweepBuild && r.ContentId == sweepContent && r.ArtifactsComplete);
            DirectoryPath = Path.Combine(BatchDirectoryPath, row.Directory);
            Report = JsonUtility.FromJson<AutoplayReport>(File.ReadAllText(Path.Combine(DirectoryPath, "report.json")));
            Batch.State = "Completed"; Batch.EndedUtc = DateTime.UtcNow.ToString("O"); batchCanceled = true;
            Status = "All manifest cases already finalized for this build/content version.";
            WriteBatch();
        }
    }
    private int NextSweepCase(int first)
    {
        if (!IsLootSweep) return first;
        while (first <= Options.RunCount && finalizedCases.Contains(Manifest.Cases[first - 1].Id)) first++;
        return first;
    }
    private void BeginSweepRun()
    {
        if (!IsLootSweep) return;
        Report.ManifestId = Manifest.Id; Report.CaseId = FloorCase.Id; Report.Profile = FloorCase.Profile;
        Report.ContentId = sweepContent; Report.BuildId = sweepBuild;
        Report.GenerationOptions = JsonUtility.ToJson(FloorCase);
        Report.ValidationOnly = true; Report.EligibleForBalance = false;
        Report.Policy = "floor-loot-sweep-v1";
        LootLedger = new AutoplayLootLedger(Report, entry =>
        {
            WriteEvent(entry);
            if (entry.Kind == "PlacementFailure" || entry.Kind == "LootCollection" && !string.IsNullOrEmpty(entry.Message))
                Issue("LootVerification", entry.Message);
        });
        loadingSweep = sweepInspected = false; sweepObjective = null;
        sweepVisited.Clear(); sweepLowHP.Clear(); sweepCollected = sweepOpened = 0;
    }
    private void SweepTick(Common common)
    {
        if (!startedCampaign)
        {
            var menu = FindFirstObjectByType<MainMenu>();
            if (menu == null || !menu.IsReady) return;
            var configuration = menu.TownConfiguration ?? TownSceneLoader.Default;
            configuration.Validate();
            CampaignParty.ClearLiveParty(common);
            common.CampaignContext = null; common.PendingDemoLoadout = null;
            common.GameSaveData = menu.CreateNewSave(CurrentSeed);
            common.GameSaveData.DungeonSaveData = new DungeonSaveData { StartFloor = FloorCase.Difficulty, EndFloor = FloorCase.Difficulty,
                UseBiomeLayout = FloorCase.LayoutVersion > 0, LayoutTier = FloorCase.Tier, LayoutBiome = FloorCase.Biome, VisualSelection = FloorCase.Visuals() };
            common.GameSaveData.TownSaveData.RecruitedAlliesData.Clear();
            foreach (var prefab in configuration.StartingParty)
            {
                var ally = Instantiate(prefab, common.TownAllyParent);
                ally.Skills = new List<string>(prefab.Skills);
                ally.EnsureStartingSkills();
                // The validation party can harvest every generated gathering site. Use the real passive and GatherAction.
                if (!ally.Skills.Contains(ExplorationSkillNames.Survey)) ally.Skills.Add(ExplorationSkillNames.Survey);
                common.InstantiatedTownAllies.Add(ally);
                common.GameSaveData.TownSaveData.RecruitedAlliesData.Add(HeroClassBinding.FromPrefab(ally,
                    new TownAllyData { AllyId = ally.Id, AllyName = ally.Name, Skills = new(ally.Skills) }));
            }
            UnityEngine.Random.InitState(CurrentSeed);
            SaveSystem.SaveData(common.GameSaveData);
            startedCampaign = true; loadingSweep = true;
            Log("Generate " + FloorCase.Profile);
            StartCoroutine(GuardSession(LoadSweepFloor()));
            return;
        }
        var game = Game.Instance;
        if (loadingSweep || game == null || !game.IsReady || game.NewFloorMessage.gameObject.activeSelf ||
            game.TurnManager.IsProcessingTurn && !game.TurnManager.AwaitingCommand) return;
        if (!sweepInspected)
        {
            LootLedger.InspectPlacement(game.CurrentDungeon); sweepInspected = true;
            var floor = game.CurrentDungeon.Floor;
            Report.GeneratedFloor = DungeonFloorFingerprint.Compute(floor);
            Report.GeneratedSeed = floor.Seed; Report.GeneratedWidth = floor.Width; Report.GeneratedHeight = floor.Height;
            Report.GeneratedThrone = floor.IsThroneFloor;
            if (floor.Seed != FloorCase.Seed || floor.Width != FloorCase.Width || floor.Height != FloorCase.Height || floor.IsThroneFloor != FloorCase.Throne)
                throw new InvalidOperationException("Generated floor does not match the manifest's seed, dimensions or role.");
            CaptureSweepCamera("entrance.png");
            Capture(); WriteReport();
        }
        foreach (var pickup in game.CurrentDungeon.Interactables.ToArray()) LootLedger.Observe(pickup);
        LootLedger.Audit();
        TrackSweepProgress(game);
        if (LootLedger.Pending.Any() == false)
        {
            Finish(LootLedger.Failed || Report.CollectedLoot != Report.ExpectedLoot ? "Failed" : "Passed",
                $"Collected {Report.CollectedLoot}/{Report.ExpectedLoot} loot sources through gameplay; {Report.UnresolvedLoot.Count} unresolved.");
            return;
        }
        SweepDungeonTick(game);
    }
    private IEnumerator LoadSweepFloor()
    {
        yield return SceneManager.LoadSceneAsync("DungeonScene");
        while (Game.Instance == null || !Game.Instance.IsReady)
        {
            if (pendingError != null) { loadingSweep = false; yield break; }
            yield return null;
        }
        loadingSweep = false;
    }
    private void TrackSweepProgress(Game game)
    {
        bool progress = false;
        foreach (var ally in game.Allies.Where(a => a != null)) progress |= sweepVisited.Add(ally.TilemapPosition);
        foreach (var actor in game.Enemies.Where(a => a != null))
        {
            int id = actor.GetInstanceID();
            if (!sweepLowHP.TryGetValue(id, out int hp) || actor.Vitals.HP < hp)
            { sweepLowHP[id] = actor.Vitals.HP; progress = true; }
        }
        int opened = Report.Loot.Count(r => r.Source != "Loose" && r.Spawned);
        if (sweepCollected != Report.CollectedLoot || sweepOpened != opened) progress = true;
        sweepCollected = Report.CollectedLoot; sweepOpened = opened;
        if (progress) lastProgress = Time.realtimeSinceStartupAsDouble;
    }
    internal void SweepDungeonTick(Game game)
    {
        ObserveAbilities(game);
        if (PartyRules.IsPartyDefeated(game)) { Finish("Failed", "Validation party defeated."); return; }
        if (MenuManager.Instance.Opened) { Finish("Unsupported", "Unexpected floor-sweep dialog."); return; }
        var ally = game.PlayerController.ControlledAlly; var dungeon = game.CurrentDungeon;
        if (ally == null) return;
        var skills = new AllySkillPolicy(game, ally, 0) { IncludeControlledAlly = true };
        if (skills.ShouldRun())
        {
            var cast = skills.GetActions().FirstOrDefault();
            if (cast != null) { Act(ally, cast, "Skill " + skills.LastChoice.Option.Skill.SkillName + " (" + skills.LastChoice.Evaluator + ")"); return; }
        }
        var attack = new AllyAttackPolicy(game, ally, 0);
        if (attack.ShouldRun())
        {
            var action = attack.GetActions().FirstOrDefault(a => a.IsValid(ally));
            if (action != null) { Act(ally, action, "Attack"); return; }
        }
        var targets = LootLedger.Pending.Where(p => p != null).ToArray();
        var mimicInteraction = NearbyMimicInteraction(game, ally);
        if (mimicInteraction != null) { Act(ally, mimicInteraction, "Open treasure chest disguise"); return; }
        bool CanOccupy(Vector3Int p) => dungeon.IsWalkable(p) && dungeon.OverlapsAnyOtherCharacter(ally, Character.ToBounds(ally.FootPrint, p), true) == null;
        bool CanStep(Vector3Int a, Vector3Int b) => dungeon.CanWalkTo(a, b) && CanOccupy(b);
        AStar.Node[,] Grid()
        {
            var grid = Character.GetAStarGrid();
            foreach (var n in grid) n.IsWalkable &= CanOccupy(new Vector3Int(n.X, n.Y));
            return grid;
        }
        bool TryTarget(Interactable target)
        {
            var prop = target as DungeonProp;
            if (prop == null && target.Position == ally.TilemapPosition)
            { Act(ally, new InteractAction(target), "Collect " + target.name); return true; }
            var approaches = prop == null ? new[] { target.Position } : Steps.Select(s => target.Position + s)
                .Where(p => CanOccupy(p) && GridMovement.CanStep(p, prop.Position, cell => cell == prop.Position || dungeon.IsWalkable(cell)))
                .OrderBy(p => (p - ally.TilemapPosition).sqrMagnitude).ToArray();
            foreach (var goal in approaches)
            {
                if (goal == ally.TilemapPosition)
                {
                    ally.SetFacingByTargetPosition(prop.Position);
                    GameAction action = prop.Definition.Kind == DungeonSceneryKind.Container || prop.IsClosedDoor && SmallKeys.Count > 0 ?
                        new InteractAction(prop) : new AttackAction(ally, ally.TilemapPosition, prop.Position);
                    Act(ally, action, (action is InteractAction ? "Open " : "Bash ") + prop.name); return true;
                }
                var next = dungeonRoute.Next(ally.TilemapPosition, goal, Grid, CanStep);
                if (!next.HasValue) continue;
                var other = game.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == next.Value);
                ally.SetFacingByTargetPosition(next.Value);
                Act(ally, other == null ? new MovementAction(ally, ally.TilemapPosition, next.Value) : new SwapAllyPositionAction(ally, other), "Loot route " + next);
                return true;
            }
            return false;
        }
        if (sweepObjective != null && targets.Contains(sweepObjective) && TryTarget(sweepObjective)) return;
        foreach (var target in targets.OrderBy(p => (p.Position - ally.TilemapPosition).sqrMagnitude))
            if (TryTarget(target)) { sweepObjective = target; return; }
        // Closed doors and other scenery may separate entire loot regions.
        foreach (var prop in dungeon.Interactables.OfType<DungeonProp>().Where(p => p.Alive && p.BlocksMovement)
            .OrderBy(p => (p.Position - ally.TilemapPosition).sqrMagnitude)) if (TryTarget(prop)) return;
        // Approach blocking creatures using the same A* and combat policy as campaign autoplay.
        foreach (var enemy in game.Enemies.Where(e => e != null && e.Vitals.HP > 0).OrderBy(e => (e.TilemapPosition - ally.TilemapPosition).sqrMagnitude))
            foreach (var goal in enemy.ToBounds().Cells().SelectMany(p => Steps.Where(s => dungeon.CanWalkTo(p + s, p)).Select(s => p + s)).Where(CanOccupy).Distinct())
            {
                var next = approachRoute.Next(ally.TilemapPosition, goal, Grid, CanStep);
                if (!next.HasValue) continue;
                var other = game.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == next.Value);
                Act(ally, other == null ? new MovementAction(ally, ally.TilemapPosition, next.Value) : new SwapAllyPositionAction(ally, other), "Approach blocking enemy"); return;
            }
        Finish("Failed", "No legal route or interaction remains for unresolved loot.");
    }

    internal static void ObserveLoot(Interactable loot) { if (Sweeping && Active.Running) Active.LootLedger?.Observe(loot); }
    internal static IDisposable LootPickup(Interactable loot, Character actor, int amount, ItemDefinition material = null) =>
        Sweeping && Active.Running ? Active.LootLedger?.Pickup(loot, actor, amount, material) : null;
    internal static IDisposable LootReward(DungeonProp prop) => Sweeping && Active.Running ? Active.LootLedger?.Release(prop) : null;
    internal static void PlacementRecovery(string message)
    {
        if (!Sweeping || !Active.Running) return;
        Active.Report.RecoveryEvents++;
        Active.LootLedger?.InspectPlacement(Game.Instance.CurrentDungeon);
        Active.CaptureSweepCamera($"recovery-{Active.Report.RecoveryEvents:D4}.png");
        Active.Report.PlacementIssues.Add(message);
        Active.WriteEvent(new AutoplayEvent { Kind = "Recovery", Message = message });
        Active.Issue("PlacementFailure", message);
    }
    private bool sessionReloadLocked;
    private void HoldSessionReload()
    {
#if UNITY_EDITOR
        // File writers, save scopes and generation coroutines cannot survive a domain
        // reload. Defer it until scene cleanup has finished under the isolated store.
        UnityEditor.EditorApplication.LockReloadAssemblies(); sessionReloadLocked = true;
#endif
    }
    private void ReleaseSessionReload()
    {
#if UNITY_EDITOR
        if (sessionReloadLocked) { sessionReloadLocked = false; UnityEditor.EditorApplication.UnlockReloadAssemblies(); }
#endif
    }
    private void CaptureSweepCamera(string name)
    {
        var controller = Game.Instance?.PlayerController?.CameraController;
        var camera = controller?.Camera;
        if (camera == null) return;
        controller.SnapToFollowTarget();
        var target = new RenderTexture(640, 360, 24);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var texture = new Texture2D(640, 360, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(DirectoryPath, name), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; target.Release(); Destroy(target); Destroy(texture); }
    }
}
