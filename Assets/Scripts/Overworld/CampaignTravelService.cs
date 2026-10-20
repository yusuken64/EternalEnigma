using System;
using System.Collections;
using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates and commits campaign travel before a scene transition.</summary>
public sealed class CampaignTravelService
{
    private readonly Common common;
    private bool transitioning;
    public bool IsTransitioning => transitioning || common.ScreenTransition.BlocksInput;
    private string returnArrivalTown;
    private BookReturnArrival bookReturn;
    private bool bookReturnPending;
    private sealed class BookReturnArrival
    {
        public string TownId, LeaderId;
        public Vector3Int Tile;
        public Facing Facing;
    }
    public bool HomecomingPending { get; private set; }
    public bool HomecomingCompleted { get; private set; }
    internal bool OpeningGateRevealPending { get; private set; }
    internal bool ConsumeOpeningGateReveal() { bool pending = OpeningGateRevealPending; OpeningGateRevealPending = false; return pending; }
    internal bool IsHomecoming(string town) => HomecomingPending && town == Context?.Campaign.StartLocationId;
    internal void FinishHomecoming(Town town)
    {
        if (!IsHomecoming(town.Configuration.Id) || !town.IsReady || town.TownPlayer.CutsceneLocked ||
            !town.TownPlayer.CameraController.enabled || common.ScreenTransition.BlockScreen.activeSelf || Context?.State.Finished != true) return;
        HomecomingPending = false; HomecomingCompleted = true;
        AutoplayRunner.FinaleEvent("HomecomingCompleted", town.Configuration.Id);
        if (common.GameSaveData.Completion is { } record)
            UnityEngine.Object.FindFirstObjectByType<TownMenuManager>().Open(CampaignSummaryDialog.Create(town.transform, record));
    }
    public bool ConsumeDungeonReturn(string townId)
    {
        if (returnArrivalTown != townId) return false;
        returnArrivalTown = null;
        return true;
    }
    public bool TryConsumeBookReturn(string townId, out Vector3Int tile, out Facing facing, out string leaderId)
    {
        tile = default; facing = Facing.Down; leaderId = null;
        if (!bookReturnPending || bookReturn == null || bookReturn.TownId != townId) return false;
        tile = bookReturn.Tile; facing = bookReturn.Facing; leaderId = bookReturn.LeaderId;
        bookReturnPending = false; bookReturn = null;
        return true;
    }
    private CampaignContext Context => common.CampaignContext;
    public CampaignTravelService(Common common) { this.common = common; }
    public void SceneReady() => transitioning = false;
    public void NewCampaign(int seed)
    {
        if (transitioning) return;
        returnArrivalTown = null;
        bookReturn = null; bookReturnPending = false;
        HomecomingPending = HomecomingCompleted = false;
        OpeningGateRevealPending = false;
        common.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
        Context.State.RequiresOpeningPreparation = true;
        Context.State.OpeningLessonVersion = 1;
        Context.State.HomeLessonVersion = 1;
        var save = common.GameSaveData;
        save.ProtagonistId = save.TownSaveData.RecruitedAlliesData.First().AllyId;
        save.Roster = save.TownSaveData.RecruitedAlliesData.ToList();
        Context.Roster.UnionWith(save.Roster.Skip(1).Select(a => a.AllyId));
        Context.SetParty(save.Roster.Skip(1).Select(a => a.AllyId).ToArray());
        save.NeedsInitialSave = true;
        EnterLocation();
    }
    public void Continue()
    {
        if (transitioning) return;
        var save = common.GameSaveData;
        if (save == null) return;
        returnArrivalTown = null;
        bookReturn = null; bookReturnPending = false;
        if (!save.HasCampaign) { common.CampaignContext = null; transitioning = true; TownSceneLoader.Load(TownSceneLoader.ResolveSaved()); return; }
        HomecomingPending = HomecomingCompleted = false;
        OpeningGateRevealPending = false;
        common.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), save.Campaign);
        CampaignParty.ClearLiveParty(common);
        if (Context.State.Scene == "Town") PrepareTown(Context.State.LocationId);
        if (Context.State.Scene == "DungeonScene") CampaignParty.BuildDungeonParty(common);
        Load(Context.State.Scene);
    }
    public bool EnterLocation(DungeonEncounterVisualSettings visuals = null)
    {
        if (IsTransitioning || Context == null || Context.IsSandbox) return false;
        var location = Context.Location;
        if (location == null || (Context.State.Scene != "Town" && !Context.Gates.IsWalkable(Context.Position, Context.Held))) return false;
        if (location.Kind == LocationKind.Town)
        {
            bool enteringFromOverworld = Context.State.Scene == "Overworld";
            var world = enteringFromOverworld ? UnityEngine.Object.FindFirstObjectByType<OverworldScene>() : null;
            var presentation = world != null ? CampaignTravelPresentation.Create(SceneTravelKind.OverworldToTown,
                world.CellCenterToWorld(Context.Grid.Locations[location.Id]), world.ViewCamera, location.Id) : null;
            Context.State.LastTownId = location.Id; Context.Towns.Add(location.Id);
            Context.State.LocationId = location.Id;
            PrepareTown(location.Id);
            if (enteringFromOverworld) common.AudioManager?.PlayCue(SoundCue.DoorOpen);
            Load("Town", travel: presentation);
            return true;
        }
        if (!Context.BeginDungeon()) return false;
        StartDungeon(location, visuals);
        return true;
    }
    public bool EnterTownDungeon(Town town, string dungeonId, DungeonEncounterVisualSettings visuals = null)
    {
        if (Context == null || dungeonId == Context.BookDungeon.Id) return false;
        return EnterTownDungeonInternal(town, dungeonId, visuals, null);
    }
    public bool EnterBookDungeon(Town town, TownDungeonBook book)
    {
        if (IsTransitioning || Context == null || town == null || book == null || town.DungeonBook != book ||
            town.Configuration.Id != Context.BookDungeon.ParentTownId || !Context.CanEnterDungeon(Context.BookDungeon.Id)) return false;
        var leader = town.TownPlayer.ControllingTownAlly;
        if (leader == null || town.TownPlayer.IsBusy || town.TownPlayer.CutsceneLocked ||
            Mathf.Max(Mathf.Abs(leader.TilemapPosition.x - book.Tile.x), Mathf.Abs(leader.TilemapPosition.y - book.Tile.y)) != 1) return false;
        var arrival = new BookReturnArrival { TownId = town.Configuration.Id, Tile = leader.TilemapPosition,
            Facing = leader.CurrentFacing, LeaderId = leader.Id };
        var presentation = CampaignTravelPresentation.Create(SceneTravelKind.HouseToBookDungeon,
            book.PageAnchor.position, town.TownPlayer.CameraController.Camera, Context.BookDungeon.Id);
        presentation.PrepareDeparture = book.Open;
        if (!EnterTownDungeonInternal(town, Context.BookDungeon.Id, null, presentation)) return false;
        bookReturn = arrival; bookReturnPending = false;
        return true;
    }
    private bool EnterTownDungeonInternal(Town town, string dungeonId, DungeonEncounterVisualSettings visuals, SceneTravelPresentation presentation)
    {
        if (IsTransitioning || Context == null || Context.IsSandbox || town == null ||
            Context.State.Scene != "Town" || town.Configuration.Id != Context.State.LocationId) return false;
        if (Context.HasHomeLesson && !Context.State.HomeDoorUnlocked) return false;
        town.WriteSaveData();
        if (!Context.BeginTownDungeon(dungeonId)) return false;
        if (presentation == null)
        {
            var entrance = town.TownBuildings.FirstOrDefault(b => b.Definition.DialogId == "entrance");
            if (entrance != null) presentation = CampaignTravelPresentation.Create(SceneTravelKind.TownToDungeon,
                CampaignTravelPresentation.TownCellAnchor(town, entrance.TilemapPosition), town.TownPlayer.CameraController.Camera, dungeonId);
        }
        StartDungeon(Context.ResolveDungeonLocation(dungeonId), visuals, presentation);
        return true;
    }
    private void StartDungeon(CampaignLocation location, DungeonEncounterVisualSettings visuals, SceneTravelPresentation presentation = null)
    {
        var floors = Context.RunPlan(location.Id);
        common.GameSaveData.DungeonSaveData = new DungeonSaveData { StartFloor = floors.StartFloor, EndFloor = floors.EndFloor };
        common.GameSaveData.DungeonSaveData.VisualSelection = DungeonVisualSelection.Resolve(Context, visuals);
        var run = common.GameSaveData.DungeonSaveData;
        run.Variant = floors.Variant;
        run.UseBiomeLayout = !EternalEnigma.Core.Generation.DungeonLayoutProfile.IsStarter(location.Id);
        run.LayoutTier = location.Tier;
        run.LayoutBiome = location.Id == Context.BookDungeon.Id ? Context.BookDungeon.Biome :
            EternalEnigma.Core.Generation.OverworldGridGenerator.BiomeForRegion(Context.Campaign, location.RegionId);
        Load("DungeonScene", location.Id == Context.BookDungeon.Id ? Context.BookDungeon.Title : null, presentation,
            () => CampaignParty.BuildDungeonParty(common));
    }
    public bool ExitTown(Town town)
    {
        if (IsTransitioning || Context == null || Context.IsSandbox || Context.State.Scene != "Town") return false;
        if (Context.HasHomeLesson && !Context.State.HomeDoorUnlocked) return false;
        if (!Context.CanLeaveTown(Context.State.LocationId))
        { common.AudioManager?.PlayCue(SoundCue.Denied); TownMenu.ShowMessage("Clear the dungeon inside town to unlock the town gate."); return false; }
        var townId = Context.State.LocationId;
        var presentation = CampaignTravelPresentation.Create(SceneTravelKind.TownToOverworld,
            CampaignTravelPresentation.TownCellAnchor(town, town.Plan.Exit.ToCell()), town.TownPlayer.CameraController.Camera, townId);
        town.WriteSaveData();
        Load("Overworld", "Overworld", presentation, () =>
        {
            Context.Position = Context.Grid.Locations[townId];
            CampaignParty.ClearLiveParty(common);
        }, deferDestinationState: true);
        return true;
    }
    public bool FinishDungeon(bool victory, PlayerController player, bool travel = true, bool keepLoot = false)
    {
        if (Context == null || Context.IsSandbox || IsTransitioning || Context.State.PendingDungeon.Length == 0) return false;
        if (victory && Context.State.PendingDungeon == Context.Campaign.FinalLocationId && Context.Finale?.VictoryReady != true) return false;
        if (victory && Game.Instance?.CurrentDungeon != null &&
            (!Game.Instance.CheckFloorExit() || player.Floor < common.GameSaveData.DungeonSaveData.EndFloor)) return false;
        if (!victory) Game.Instance?.Prison?.Recover(Game.Instance);
        DungeonReturnService.Commit(common.GameSaveData, TownSceneLoader.ResolveSaved(), victory,
            player.Gold, player.Inventory.InventoryItems, PartyRules.PartyMembers(Game.Instance), keepLoot);
        var previousKeys = Context.Keys.ToArray();
        var previousClaims = Context.Claimed.ToArray();
        var previousOpenedGates = Context.Opened.ToArray();
        string firstClear = victory && !Context.Completed.Contains(Context.State.PendingDungeon)
            ? Context.State.PendingDungeon : null;
        bool homecoming = victory && Context.State.PendingDungeon == Context.Campaign.FinalLocationId;
        var finishedLocation = Context.ResolveDungeonLocation(Context.State.PendingDungeon);
        bool leavingBook = Context.State.PendingDungeon == Context.BookDungeon.Id;
        SceneTravelPresentation presentation = null;
        if (!homecoming && (victory || keepLoot) && finishedLocation?.ParentTownId != null && player?.ControlledAlly != null)
            presentation = CampaignTravelPresentation.Create(leavingBook ? SceneTravelKind.BookDungeonToHouse : SceneTravelKind.DungeonToTown,
                player.ControlledAlly.transform.position, player.CameraController.Camera, finishedLocation.ParentTownId);
        if (!Context.CompleteDungeon(victory)) return false;
        if (firstClear == "story-0") OpeningGateRevealPending = true;
        // Town exits and encounter-controlled crossings open at the successful
        // completion commit, rather than when the next scene redraws their gates.
        if (victory && Context.Opened.Except(previousOpenedGates).Any())
            common.AudioManager?.PlayCue(SoundCue.GateUnlock);
        if (homecoming) common.GameSaveData.Completion = CampaignCompletionRecord.Capture(common, player);
        if (homecoming) { HomecomingPending = true; HomecomingCompleted = false; common.GameSaveData.HasArrival = false; }
        bookReturnPending = leavingBook && !homecoming && (victory || keepLoot) && bookReturn != null;
        returnArrivalTown = !bookReturnPending && !homecoming && Context.State.Scene == "Town" ? Context.State.LocationId : null;
        if (!bookReturnPending) bookReturn = null;
        var newKeys = Context.Keys.Except(previousKeys).OrderBy(key => key).ToArray();
        var capabilities = Context.Campaign.Sources.Where(source => Context.Claimed.Contains(source.Id) && !previousClaims.Contains(source.Id))
            .Select(source => DescribeCapabilityReward(Context, source)).ToArray();
        if (firstClear != null)
        {
            var ability = DungeonClearAbilities.ForDungeon(firstClear);
            if (ability != null) capabilities = capabilities.Append($"<b>{ability.DisplayName}</b>\n{ability.Description}").ToArray();
            if (firstClear == "repeatable-0") capabilities = capabilities.Append("<b>Reset Floor</b>\nPermanently restore puzzle floors to their entry state at no Mana cost.").ToArray();
        }
        CampaignParty.ClearLiveParty(common);
        CampaignParty.Capture(common);
        if (Context.State.Scene == "Town") PrepareTown(Context.State.LocationId);
        SaveSystem.Capture(common);
        if (travel)
        {
            void ContinueTravel()
            {
                transitioning = false;
                Load(Context.State.Scene, travel: presentation);
            }
            if (!homecoming && (newKeys.Length > 0 || capabilities.Length > 0))
            {
                transitioning = true;
                common.StartCoroutine(ShowKeyRewards(newKeys, ContinueTravel, capabilityRewards: capabilities));
            }
            else ContinueTravel();
        }
        return true;
    }

    public void ShowRewardsAcquired(string[] keys, string[] capabilityRewards)
    {
        if (transitioning || (keys.Length == 0 && capabilityRewards.Length == 0)) return;
        transitioning = true;
        common.StartCoroutine(ShowKeyRewards(keys, () => transitioning = false, false, capabilityRewards));
    }

    public static string DescribeCapabilityReward(CampaignContext context, CapabilitySource source) =>
        $"<b>{CampaignRewardNames.Name(context, source.Capability)}</b>\n{source.Capability.DisplayName()}\n" +
        CampaignRewardNames.Purpose(source.Capability) + "\n" + (source.CompanionId != null
            ? "Add the companion with this capability to your party at a town to use it."
            : "Permanently available for matching overworld obstacles and routes.");

    private string CapabilityName(EternalEnigma.Core.Capabilities.Capability capability) =>
        CampaignRewardNames.Name(Context, capability);

    private IEnumerator ShowKeyRewards(string[] keys, Action continueTravel, bool inDungeon = true, string[] capabilityRewards = null)
    {
        var rewards = keys.Select(key =>
        {
            bool townGate = Context.Campaign.Routes.Any(route => route.KeyId == key && route.IsTownExit);
            return $"<b>{Context.Campaign.KeyLabel(key)}</b>\n" + (townGate && inDungeon
                ? "The town gate is now open. Rest at home to save your victory, then follow the gate to the overworld!"
                : "Unlocks its matching locked gate on the overworld.");
        });
        string heading = keys.Length == 1 ? "KEY ACQUIRED!" : "KEYS ACQUIRED!";
        if (capabilityRewards != null && capabilityRewards.Length > 0)
        {
            heading = keys.Length > 0 ? "REWARDS ACQUIRED!" : "CAPABILITY ACQUIRED!";
            rewards = rewards.Concat(capabilityRewards);
        }
        yield return ShowKeyMessage($"<b>{heading}</b>\n" + (inDungeon ? "Dungeon cleared!\n\n" : "\n") +
            string.Join("\n\n", rewards), continueTravel, inDungeon);
    }

    public void ShowKeyUsed(string key)
    {
        if (transitioning) return;
        transitioning = true;
        common.StartCoroutine(ShowKeyMessage($"<b>GATE UNLOCKED!</b>\n\nYou used <b>{KeyLabel(key)}</b>.\n\nThe way is now open!",
            () => transitioning = false, false));
    }

    /// <summary>Keys are identified by id in saves and shown by name. <paramref name="lockText"/> is the gate's own fiction, which names the key.</summary>
    public void ShowMissingKey(string key, string lockText = null)
    {
        if (transitioning) return;
        transitioning = true;
        string need = string.IsNullOrEmpty(lockText) ? $"You need <b>{KeyLabel(key)}</b> to open this gate." : lockText;
        common.StartCoroutine(ShowKeyMessage($"<b>GATE LOCKED!</b>\n\n{need}\n\nFind the key and return here.",
            () => transitioning = false, false));
    }

    private string KeyLabel(string key) => Context?.Campaign.KeyLabel(key) ?? key;

    public void ShowCapabilityUsed(Requirement requirement, EternalEnigma.Core.Capabilities.CapabilitySet held)
    {
        if (transitioning || requirement.IsOpen) return;
        var used = requirement.Alternatives.Where(held.ContainsAll).OrderBy(set => set.Count).First();
        string names = string.Join(" + ", used.Values.Select(CapabilityName));
        transitioning = true;
        common.StartCoroutine(ShowKeyMessage($"<b>CAPABILITY USED!</b>\n\nYou used <b>{names}</b>.\n\nThe way is now open!",
            () => transitioning = false, false));
    }

    private IEnumerator ShowKeyMessage(string message, Action continueTravel, bool inDungeon)
    {
        var rewardContext = Context;
        // Let the triggering confirmation/input finish before showing its successor.
        yield return null;
        if (Context != rewardContext) { transitioning = false; yield break; }
        var dialog = common.MessageDialog;
        dialog.PromptText.text = GameplayText.Format(message);
        var canvas = dialog.GetComponent<Canvas>();
        int previousOrder = canvas.sortingOrder;
        canvas.sortingOrder = 2000;
        bool acknowledged = false;
        dialog.CloseAction = () => acknowledged = true;
        var worldDialogs = inDungeon ? null : new JuicyChickenGames.Menu.DialogController();
        if (inDungeon) MenuManager.Open(dialog);
        else worldDialogs.Open(dialog);
        float demoReadTime = 0;
        while (!acknowledged)
        {
            if (Context != rewardContext)
            {
                dialog.CloseDialog();
                canvas.sortingOrder = previousOrder;
                transitioning = false;
                yield break;
            }
            worldDialogs?.Tick();
            // Watching a demo must not stall on a reward; pause still pauses this timer.
            var demo = AutoplayRunner.Active;
            if (demo != null && demo.Running && !demo.PlayerControlled && !demo.Paused)
            {
                demoReadTime += Time.unscaledDeltaTime;
                if (demoReadTime >= 3f) dialog.Ok_Clicked();
            }
            yield return null;
        }
        canvas.sortingOrder = previousOrder;
        continueTravel();
    }
    public bool ReturnToMenu()
    {
        if (AutoplayRunner.Active != null) { AutoplayRunner.Active.ExitDemo(); return true; }
        if (transitioning || Context == null) return false;
        bookReturn = null; bookReturnPending = false;
        if (Context.IsSandbox) common.EndSandbox();
        else { common.GameSaveData = SaveSystem.Inspect(SaveSystem.ActiveSlot, out _); common.CampaignContext = null; }
        CampaignParty.ClearLiveParty(common);
        transitioning = true;
        common.ScreenTransition.DoTransition(() => SceneManager.LoadScene("MainMenu"), autoOpen: false);
        return true;
    }

    private void PrepareTown(string id)
    {
        var configuration = UnityEngine.Object.Instantiate(TownSceneLoader.Default);
        configuration.name = Context.GetTownDisplayName(id); configuration.Id = id;
        CampaignTownLayout.Configure(configuration, Context.TownLayout(id));
        configuration.MaxPartySize = 4;
        common.GameSaveData.TownSaveData.TownSeed = Context.LocationSeed(id);
        CampaignParty.PrepareActive(common, configuration);
        TownSceneLoader.Configure(configuration);
    }
    private void Load(string scene, string destinationTitle = null, SceneTravelPresentation travel = null, Action beforeLoad = null,
        bool deferDestinationState = false)
    {
        if (transitioning) return;
        transitioning = true;
        void CommitDestination() { Context.State.Scene = scene; SaveSystem.Capture(common); }
        if (!deferDestinationState) CommitDestination();
        if(scene=="Town")destinationTitle=Context.GetTownDisplayName(Context.State.LocationId);
        common.ScreenTransition.DoTransition(() =>
        {
            // First departure keeps town presentation on its cheap location lookup.
            // Switching Scene earlier lets TownBiomeStyle force lazy world generation
            // during the zoom, before a covered frame has actually been presented.
            beforeLoad?.Invoke();
            if (deferDestinationState) CommitDestination();
            SceneManager.LoadScene(scene);
        },
            autoOpen: false, destinationTitle: destinationTitle, travel: travel);
    }
}
