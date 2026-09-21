using System;
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
        if (save.CampaignFormatVersion == 0 || save.Campaign == null) { common.CampaignContext = null; transitioning = true; TownSceneLoader.Load(TownSceneLoader.ResolveSaved()); return; }
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
    public bool EnterLocation()
    {
        if (transitioning || Context == null || Context.IsSandbox) return false;
        var location = Context.Location;
        if (location == null || !Context.Gates.IsWalkable(Context.Position, Context.Held)) return false;
        if (location.Kind == LocationKind.Town)
        {
            Context.State.LastTownId = location.Id; Context.Towns.Add(location.Id);
            Context.State.LocationId = location.Id; PrepareTown(location.Id); Load("Town"); return true;
        }
        if (!Context.BeginDungeon()) return false;
        StartDungeon(location);
        return true;
    }
    public bool EnterTownDungeon(Town town, string dungeonId)
    {
        if (transitioning || Context == null || Context.IsSandbox || town == null ||
            Context.State.Scene != "Town" || town.Configuration.Id != Context.State.LocationId) return false;
        town.WriteSaveData();
        if (!Context.BeginTownDungeon(dungeonId)) return false;
        StartDungeon(Context.Campaign.Locations.Single(l => l.Id == dungeonId));
        return true;
    }
    private void StartDungeon(CampaignLocation location)
    {
        var floors = CampaignContext.Floors(location.Tier);
        common.GameSaveData.PreRunTownJson = JsonUtility.ToJson(common.GameSaveData.TownSaveData);
        common.GameSaveData.DungeonSaveData = new DungeonSaveData { StartFloor = floors.Start, EndFloor = floors.End };
        CampaignParty.BuildDungeonParty(common);
        Load("DungeonScene");
    }
    public bool ExitTown(Town town)
    {
        if (transitioning || Context == null || Context.IsSandbox || Context.State.Scene != "Town") return false;
        if (!Context.CanLeaveTown(Context.State.LocationId))
        { TownMenu.ShowMessage("Clear the dungeon inside town to unlock the town gate."); return false; }
        town.WriteSaveData(); CampaignParty.ClearLiveParty(common); Context.Position = Context.Grid.Locations[Context.State.LocationId]; Load("Overworld"); return true;
    }
    public bool FinishDungeon(bool victory, PlayerController player, bool travel = true)
    {
        if (Context == null || Context.IsSandbox || transitioning || Context.State.PendingDungeon.Length == 0) return false;
        DungeonReturnService.Commit(common.GameSaveData, TownSceneLoader.ResolveSaved(), victory,
            player.Gold, player.Inventory.InventoryItems, Game.Instance.Allies);
        Context.CompleteDungeon(victory);
        CampaignParty.ClearLiveParty(common);
        CampaignParty.Capture(common);
        common.GameSaveData.PreRunTownJson = null;
        if (Context.State.Scene == "Town") PrepareTown(Context.State.LocationId);
        SaveSystem.SaveData(common.GameSaveData);
        if (travel) Load(Context.State.Scene);
        return true;
    }
    public bool ReturnToMenu()
    {
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
        configuration.name = "Campaign " + id; configuration.Id = id;
        configuration.PartySpawn = new Vector3Int(10, 2, 0);
        configuration.MaxPartySize = 4;
        common.GameSaveData.TownSaveData.TownSeed = Context.LocationSeed(id);
        CampaignParty.PrepareActive(common, configuration);
        TownSceneLoader.Configure(configuration);
    }
    private void Load(string scene)
    {
        if (transitioning) return;
        transitioning = true; Context.State.Scene = scene;
        SaveSystem.SaveData(common.GameSaveData);
        common.ScreenTransition.DoTransition(() => SceneManager.LoadScene(scene), autoOpen: false);
    }
}
