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
    public int Level, Experience, Hp, MaxHp, Sp, MaxSp, Strength, Defense;
    public float ExperienceProgress;
}
public static class CampaignSaving
{
    public static bool Commit(Common common, string point, Vector3Int? arrival, Facing facing, out string error)
    {
        error = null;
        if (common.CampaignContext == null || common.CampaignContext.IsSandbox || common.GameSaveData.IsSandbox)
        { error = "This session cannot save a campaign."; return false; }
        try
        {
            SaveSystem.Capture(common);
            var regionId = common.CampaignContext.Campaign.Locations.Single(l => l.Id == common.GameSaveData.Campaign.LocationId).RegionId;
            if (!common.GameSaveData.VisitedRegions.Contains(regionId)) common.GameSaveData.VisitedRegions.Add(regionId);
            var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(common.GameSaveData));
            snapshot.SavePointId = point;
            snapshot.SavedUtc = DateTime.UtcNow.ToString("O");
            snapshot.HasArrival = arrival.HasValue;
            snapshot.ArrivalTownId = snapshot.Campaign.LocationId;
            snapshot.ArrivalHeroId = UnityEngine.Object.FindFirstObjectByType<Town>()?.TownPlayer.ControllingTownAlly?.Id;
            if (arrival.HasValue) { snapshot.ArrivalX = arrival.Value.x; snapshot.ArrivalY = arrival.Value.y; snapshot.ArrivalFacing = facing; }
            snapshot.Summary = BuildSummary(common, snapshot);
            SaveSystem.SaveData(snapshot);
            common.GameSaveData.SavedUtc = snapshot.SavedUtc;
            common.GameSaveData.Summary = snapshot.Summary;
            common.GameSaveData.SavePointId = point;
            common.GameSaveData.NeedsInitialSave = false;
            return true;
        }
        catch (Exception e) { error = "Saving failed. Your previous save is intact. " + e.Message; return false; }
    }
    public static CampaignSaveSummary BuildSummary(Common common, GameSaveData save)
    {
        var campaign = common.CampaignContext.Campaign;
        var location = campaign.Locations.Single(l => l.Id == save.Campaign.LocationId);
        var region = campaign.Regions.Single(r => r.Id == location.RegionId);
        var required = campaign.Locations.Where(l => l.Required && (l.Kind == LocationKind.StoryDungeon || l.Kind == LocationKind.FinalDungeon || l.Kind == LocationKind.RepeatableDungeon)).ToArray();
        var result = new CampaignSaveSummary {
            Biome = OverworldGridGenerator.BiomeForRegion(campaign, region.Id), Region = region.DisplayName,
            Town = location.Kind == LocationKind.Town ? common.CampaignContext.GetTownDisplayName(location.Id) : region.DisplayName,
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
                Hp = hero.Hp < 0 ? stats.HPMax : hero.Hp, MaxHp = stats.HPMax, Sp = hero.Sp < 0 ? stats.SPMax : hero.Sp, MaxSp = stats.SPMax,
                Strength = stats.Strength, Defense = stats.Defense });
        }
        return result;
    }
}
