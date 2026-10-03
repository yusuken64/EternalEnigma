using System;
using System.Collections;
using System.Linq;
using EternalEnigma.Core.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates and commits campaign travel before a scene transition.</summary>
public sealed class CampaignTravelService
{
    private readonly Common common;
    private bool transitioning;
    public bool IsTransitioning => transitioning;
    private CampaignContext Context => common.CampaignContext;
    public CampaignTravelService(Common common) { this.common = common; }
    public void SceneReady() => transitioning = false;
    public void NewCampaign(int seed)
    {
        if (transitioning) return;
        common.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
        var save = common.GameSaveData;
        save.ProtagonistId = save.TownSaveData.RecruitedAlliesData.First().AllyId;
        save.Roster = save.TownSaveData.RecruitedAlliesData.ToList();
        Context.Roster.UnionWith(save.Roster.Skip(1).Select(a => a.AllyId));
        Context.SetParty(save.Roster.Skip(1).Select(a => a.AllyId).ToArray());
        EnterLocation();
    }
    public void Continue()
    {
        if (transitioning) return;
        var save = common.GameSaveData;
        if (!save.HasCampaign) { common.CampaignContext = null; transitioning = true; TownSceneLoader.Load(TownSceneLoader.ResolveSaved()); return; }
        if (save.Campaign.Finished) return;
        common.CampaignContext = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign), save.Campaign);
        if (Context.RecoverInterruptedRun())
        {
            if (!string.IsNullOrEmpty(save.PreRunTownJson)) save.TownSaveData = JsonUtility.FromJson<TownSaveData>(save.PreRunTownJson);
            save.PreRunTownJson = null;
            save.DungeonSaveData.ReturnCommitted = true;
        }
        CampaignParty.ClearLiveParty(common);
        if (Context.State.Scene == "Town") PrepareTown(Context.State.LocationId);
        Load(Context.State.Scene);
    }
    public bool EnterLocation(DungeonEncounterVisualSettings visuals = null)
    {
        if (transitioning || Context == null || Context.IsSandbox) return false;
        var location = Context.Location;
        if (location == null || (Context.State.Scene != "Town" && !Context.Gates.IsWalkable(Context.Position, Context.Held))) return false;
        if (location.Kind == LocationKind.Town)
        {
            Context.State.LastTownId = location.Id; Context.Towns.Add(location.Id);
            Context.State.LocationId = location.Id; PrepareTown(location.Id); Load("Town"); return true;
        }
        if (!Context.BeginDungeon()) return false;
        StartDungeon(location, visuals);
        return true;
    }
    public bool EnterTownDungeon(Town town, string dungeonId, DungeonEncounterVisualSettings visuals = null)
    {
        if (transitioning || Context == null || Context.IsSandbox || town == null ||
            Context.State.Scene != "Town" || town.Configuration.Id != Context.State.LocationId) return false;
        town.WriteSaveData();
        if (!Context.BeginTownDungeon(dungeonId)) return false;
        StartDungeon(Context.Campaign.Locations.Single(l => l.Id == dungeonId), visuals);
        return true;
    }
    private void StartDungeon(CampaignLocation location, DungeonEncounterVisualSettings visuals)
    {
        var floors = CampaignContext.Floors(location.Tier);
        common.GameSaveData.PreRunTownJson = JsonUtility.ToJson(common.GameSaveData.TownSaveData);
        common.GameSaveData.DungeonSaveData = new DungeonSaveData { StartFloor = floors.Start, EndFloor = floors.End };
        common.GameSaveData.DungeonSaveData.VisualSelection = DungeonVisualSelection.Resolve(Context, visuals);
        var run = common.GameSaveData.DungeonSaveData;
        run.UseBiomeLayout = !EternalEnigma.Core.Generation.DungeonLayoutProfile.IsStarter(location.Id);
        run.LayoutTier = location.Tier;
        run.LayoutBiome = EternalEnigma.Core.Generation.OverworldGridGenerator.BiomeForRegion(Context.Campaign, location.RegionId);
        CampaignParty.BuildDungeonParty(common);
        Load("DungeonScene");
    }
    public bool ExitTown(Town town)
    {
        if (transitioning || Context == null || Context.IsSandbox || Context.State.Scene != "Town") return false;
        if (!Context.CanLeaveTown(Context.State.LocationId))
        { TownMenu.ShowMessage("Clear the dungeon inside town to unlock the town gate."); return false; }
        town.WriteSaveData(); CampaignParty.ClearLiveParty(common); Context.Position = Context.Grid.Locations[Context.State.LocationId]; Load("Overworld", "Overworld"); return true;
    }
    public bool FinishDungeon(bool victory, PlayerController player, bool travel = true, bool keepLoot = false)
    {
        if (Context == null || Context.IsSandbox || transitioning || Context.State.PendingDungeon.Length == 0) return false;
        DungeonReturnService.Commit(common.GameSaveData, TownSceneLoader.ResolveSaved(), victory,
            player.Gold, player.Inventory.InventoryItems, PartyRules.PartyMembers(Game.Instance), keepLoot);
        var previousKeys = Context.Keys.ToArray();
        var previousClaims = Context.Claimed.ToArray();
        Context.CompleteDungeon(victory);
        var newKeys = Context.Keys.Except(previousKeys).OrderBy(key => key).ToArray();
        var capabilities = Context.Campaign.Sources.Where(source => Context.Claimed.Contains(source.Id) && !previousClaims.Contains(source.Id))
            .Select(DescribeCapabilityReward).ToArray();
        CampaignParty.ClearLiveParty(common);
        CampaignParty.Capture(common);
        common.GameSaveData.PreRunTownJson = null;
        if (Context.State.Scene == "Town") PrepareTown(Context.State.LocationId);
        SaveSystem.SaveData(common.GameSaveData);
        if (travel)
        {
            void ContinueTravel()
            {
                transitioning = false;
                if (victory && Context.State.Finished) Game.Instance.ShowGameOver(victory: true);
                else Load(Context.State.Scene);
            }
            if (newKeys.Length > 0 || capabilities.Length > 0)
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

    public static string DescribeCapabilityReward(CapabilitySource source) =>
        $"<b>{CapabilityName(source.Capability)}</b>\n" + (source.CompanionId != null
            ? "Add the companion with this capability to your party at a town to use it."
            : "Permanently available for matching overworld obstacles and routes.");

    private static string CapabilityName(EternalEnigma.Core.Capabilities.Capability capability) =>
        EternalEnigma.Core.Capabilities.CapabilityCatalog.DisplayName(capability);

    private IEnumerator ShowKeyRewards(string[] keys, Action continueTravel, bool inDungeon = true, string[] capabilityRewards = null)
    {
        var rewards = keys.Select(key =>
        {
            bool townGate = Context.Campaign.Routes.Any(route => route.KeyId == key && route.IsTownExit);
            return $"<b>{Context.Campaign.KeyLabel(key)}</b>\n" + (townGate && inDungeon
                ? "The town gate is now open. You can leave for the overworld!"
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
        dialog.PromptText.text = message;
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
        if (AutoplayRunner.Active != null && AutoplayRunner.Active.PlayerControlled) { AutoplayRunner.Active.ExitDemo(); return true; }
        if (transitioning || Context == null) return false;
        if (Context.IsSandbox) common.EndSandbox();
        else
        {
            var dungeon = UnityEngine.Object.FindFirstObjectByType<Game>();
            if (Context.State.PendingDungeon.Length != 0 && dungeon != null)
                FinishDungeon(false, dungeon.PlayerController, false);
            var town = UnityEngine.Object.FindFirstObjectByType<Town>();
            if (town != null) town.WriteSaveData();
            SaveSystem.SaveData(common.GameSaveData);
        }
        CampaignParty.ClearLiveParty(common);
        transitioning = true;
        common.ScreenTransition.DoTransition(() => SceneManager.LoadScene("MainMenu"));
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
    private void Load(string scene, string destinationTitle = null)
    {
        if (transitioning) return;
        transitioning = true; Context.State.Scene = scene;
        SaveSystem.SaveData(common.GameSaveData);
        if(scene=="Town")destinationTitle=Context.GetTownDisplayName(Context.State.LocationId);
        common.ScreenTransition.DoTransition(() => SceneManager.LoadScene(scene), autoOpen: false, destinationTitle: destinationTitle);
    }
}
