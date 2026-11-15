using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEngine;

[Serializable]
public class CampaignSaveSummary
{
    public OverworldBiome Biome;
    public string Town, Region;
    public int RequiredCleared, RequiredTotal, RegionsVisited, RegionsTotal;
    public List<SavedHeroSummary> Party = new();
}
[Serializable]
public class SavedHeroSummary
{
    public string Id, Name, PrimaryClass, SecondaryClass, PortraitId;
    public int Level, Experience, Hp, MaxHp, Mana, MaxMana, Strength, Defense;
    public float ExperienceProgress;
}
public static class CampaignSaving
{
    public static bool CanSaveOverworld(Common common)
    {
        var context = common?.CampaignContext;
        if (context == null || context.IsSandbox || common.GameSaveData == null || common.GameSaveData.IsSandbox ||
            context.State.Scene != "Overworld" || context.Finale != null || common.Travel.IsTransitioning ||
            AutoplayRunner.Active != null) return false;
        var world = UnityEngine.Object.FindFirstObjectByType<OverworldScene>();
        return world != null && world.Context == context && world.IsReady && !world.IsMoving &&
            world.GetComponent<OverworldMenuManager>().Interaction == null;
    }

    public static bool SaveOverworld(Common common, out string error)
    {
        if (!CanSaveOverworld(common))
        { error = "Save when your party is standing still on the overworld."; return false; }
        CampaignParty.Capture(common);
        // Commit always writes the active campaign slot; there is no save-slot choice.
        return Commit(common, "overworld", null, Facing.Down, out error);
    }

    public static bool Commit(Common common, string point, Vector3Int? arrival, Facing facing, out string error, bool beginOpeningDay = false)
    {
        error = null;
        if (common.CampaignContext == null || common.CampaignContext.IsSandbox || common.GameSaveData.IsSandbox)
        { error = "This session cannot save a campaign."; return false; }
        if (common.CampaignContext.Finale != null)
        { error = "Return to a town to save."; return false; }
        if (beginOpeningDay && (point != "town-0/home" || common.CampaignContext.State.LocationId != "town-0" ||
            common.CampaignContext.State.Scene != "Town" || !common.CampaignContext.ReadyForOpeningDay))
        { error = common.CampaignContext.OpeningPreparationHint; return false; }
        try
        {
            SaveSystem.Capture(common);
            var regionId = SaveRegionId(common, common.GameSaveData);
            if (!common.GameSaveData.VisitedRegions.Contains(regionId)) common.GameSaveData.VisitedRegions.Add(regionId);
            var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(common.GameSaveData));
            snapshot.Campaign.OpeningClearNeedsSave = false;
            // Publish the opening key to live state only after the complete morning save succeeds.
            if (beginOpeningDay)
                snapshot.Campaign.Keys = snapshot.Campaign.Keys.Append(CampaignContext.OpeningDayKey).Distinct().ToArray();
            snapshot.SavePointId = point;
            snapshot.SavedUtc = DateTime.UtcNow.ToString("O");
            snapshot.HasArrival = arrival.HasValue;
            snapshot.ArrivalTownId = snapshot.Campaign.LocationId;
            snapshot.ArrivalHeroId = UnityEngine.Object.FindFirstObjectByType<Town>()?.TownPlayer.ControllingTownAlly?.Id;
            if (arrival.HasValue) { snapshot.ArrivalX = arrival.Value.x; snapshot.ArrivalY = arrival.Value.y; snapshot.ArrivalFacing = facing; }
            snapshot.Summary = BuildSummary(common, snapshot);
            SaveSystem.SaveData(snapshot);
            common.CampaignContext.State.OpeningClearNeedsSave = false;
            if (beginOpeningDay)
            {
                common.CampaignContext.Keys.Add(CampaignContext.OpeningDayKey);
                SaveSystem.Capture(common);
            }
            common.GameSaveData.SavedUtc = snapshot.SavedUtc;
            common.GameSaveData.Summary = snapshot.Summary;
            common.GameSaveData.SavePointId = point;
            common.GameSaveData.NeedsInitialSave = false;
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Saving campaign failed. " + e); error = "Saving failed. Your previous save is intact. Please try again."; return false; }
    }
    public static CampaignSaveSummary BuildSummary(Common common, GameSaveData save)
    {
        var campaign = common.CampaignContext.Campaign;
        var location = common.CampaignContext.ResolveDungeonLocation(save.Campaign.LocationId) ??
            campaign.Locations.Single(l => l.Id == save.Campaign.LocationId);
        var region = campaign.Regions.Single(r => r.Id == SaveRegionId(common, save));
        var required = campaign.Locations.Where(l => l.Required && (l.Kind == LocationKind.StoryDungeon || l.Kind == LocationKind.FinalDungeon || l.Kind == LocationKind.RepeatableDungeon)).ToArray();
        var result = new CampaignSaveSummary {
            Biome = OverworldGridGenerator.BiomeForRegion(campaign, region.Id), Region = region.DisplayName,
            Town = save.Campaign.Scene == "Overworld" ? "Overworld" :
                location.Kind == LocationKind.Town ? common.CampaignContext.GetTownDisplayName(location.Id) : region.DisplayName,
            RequiredTotal = required.Length, RequiredCleared = required.Count(l => save.Campaign.Completed.Contains(l.Id)),
            RegionsTotal = campaign.Regions.Count, RegionsVisited = save.VisitedRegions.Distinct().Count() };
        var catalog = ClassCatalog.Load();
        foreach (var hero in save.TownSaveData.RecruitedAlliesData.OrderBy(a => a.AllyId == save.ProtagonistId ? 0 : 1).Take(4))
        {
            var prefab = CampaignParty.Resolve(hero.AllyId, TownSceneLoader.Default);
            var primary = catalog?.Get(hero.PrimaryClassId);
            var stats = HeroStatRules.BaseStats(GamePresentationProfile.Current.AllyTemplate.StartingStats, primary, hero.Level, hero.Attributes);
            foreach (var item in hero.Equipment)
                if (item.Restore(common.ItemManager) is EquipableInventoryItem equipment) stats += equipment.GetEquipmentStatModification();
            foreach (var name in hero.Skills ?? new())
            {
                var skill = common.SkillManager.GetSkillByName(name);
                if (skill?.ActivationType == ActivationType.Passive)
                    stats += StatScaling.Scale(skill.PassiveStatModification, skill.RankScaling, Math.Max(1, hero.SkillRanks.FirstOrDefault(r => r.SkillName == name)?.Rank ?? 1));
            }
            result.Party.Add(new SavedHeroSummary { Id = hero.AllyId, PortraitId = prefab?.Id, Name = hero.AllyName,
                PrimaryClass = primary?.DisplayName ?? "", SecondaryClass = catalog?.Get(hero.SecondaryClassId)?.DisplayName ?? "",
                Level = hero.Level, Experience = hero.Experience, ExperienceProgress = LevelSystem.Progress(hero.Level, hero.Experience),
                Hp = hero.Hp < 0 ? stats.HPMax : hero.Hp, MaxHp = stats.HPMax, Mana = hero.Mana < 0 ? stats.ManaMax : hero.Mana, MaxMana = stats.ManaMax,
                Strength = stats.Strength, Defense = stats.Defense });
        }
        return result;
    }

    private static string SaveRegionId(Common common, GameSaveData save)
    {
        var context = common.CampaignContext;
        if (save.Campaign.Scene == "Overworld")
        {
            var position = new GridPoint(save.Campaign.X, save.Campaign.Y);
            var grid = context.Grid;
            var region = context.Campaign.Regions.FirstOrDefault(r =>
                grid.Layers.TryGetValue(OverworldLayers.Region(r.Id), out var layer) && layer.At(position));
            if (region != null) return region.Id;
            // Crossings between region masks use the closest overworld location.
            return context.Campaign.Locations.Where(l => grid.Locations.ContainsKey(l.Id))
                .OrderBy(l => Math.Abs(grid.Locations[l.Id].X - position.X) + Math.Abs(grid.Locations[l.Id].Y - position.Y))
                .First().RegionId;
        }
        return (context.ResolveDungeonLocation(save.Campaign.LocationId) ??
            context.Campaign.Locations.Single(l => l.Id == save.Campaign.LocationId)).RegionId;
    }
}
