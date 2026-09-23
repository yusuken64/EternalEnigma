using System;
using System.Linq;
using UnityEngine;
using EternalEnigma.Core.Capabilities;

[Serializable]
public sealed class CampaignCompanionPrefab
{
    public Capability Capability;
    public TownAlly Prefab;
}

public static class CampaignParty
{
    public static TownAlly Resolve(string id, TownConfiguration configuration)
    {
        var companion = Common.Instance.CampaignContext?.Campaign.Companions.FirstOrDefault(c => c.Id == id);
        if (companion != null)
            return configuration.CampaignCompanions.FirstOrDefault(m => m.Capability == companion.Capability)?.Prefab
                ?? configuration.AllyCatalog[(int)companion.Capability % configuration.AllyCatalog.Count];
        return configuration.AllyCatalog.FirstOrDefault(a => a.Id == id);
    }
    public static void Capture(Common common)
    {
        if (common.CampaignContext == null) return;
        var save = common.GameSaveData;
        foreach (var member in save.TownSaveData.RecruitedAlliesData)
        {
            save.Roster.RemoveAll(a => a.AllyId == member.AllyId); save.Roster.Add(member);
            if (member.AllyId != save.ProtagonistId) common.CampaignContext.Roster.Add(member.AllyId);
        }
    }
    public static void PrepareActive(Common common, TownConfiguration configuration)
    {
        var save = common.GameSaveData; var context = common.CampaignContext;
        foreach (var id in context.Roster)
            if (!save.Roster.Any(a => a.AllyId == id))
            {
                var prefab = Resolve(id, configuration);
                if (prefab == null) throw new InvalidOperationException("Missing campaign companion prefab: " + id);
                save.Roster.Add(HeroClassBinding.FromPrefab(prefab, new TownAllyData { AllyId = id, AllyName = prefab.Name, Skills = prefab.Skills.ToList() }));
            }
        save.TownSaveData.RecruitedAlliesData = new[] { save.ProtagonistId }.Concat(context.Active)
            .Select(id => save.Roster.Single(a => a.AllyId == id)).ToList();
    }
    public static void ClearLiveParty(Common common)
    {
        foreach (Transform child in common.TownAllyParent.Cast<Transform>().ToArray())
        { child.SetParent(null); child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
        common.InstantiatedTownAllies.Clear();
    }
    public static void BuildDungeonParty(Common common)
    {
        var configuration = TownSceneLoader.ResolveSaved();
        PrepareActive(common, configuration);
        ClearLiveParty(common);
        foreach (var data in common.GameSaveData.TownSaveData.RecruitedAlliesData)
        {
            var ally = UnityEngine.Object.Instantiate(Resolve(data.AllyId, configuration), common.TownAllyParent);
            ally.Id = data.AllyId; ally.Skills = data.Skills.ToList();
            ally.SkillRanks = (data.SkillRanks ?? new System.Collections.Generic.List<SkillRankSaveData>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.SkillName))
                .Select(r => new SkillRankSaveData { SkillName = r.SkillName, Rank = r.Rank }).ToList();
            ally.HighestLevel = Mathf.Max(1, data.HighestLevel);
            HeroClassBinding.Apply(ally, data, common.GameSaveData);
            foreach (var item in data.Equipment)
                if (item.Restore(common.ItemManager) is EquipableInventoryItem equipment) ally.Equipment.Equip(equipment);
            ally.EnsureStartingSkills();
            common.InstantiatedTownAllies.Add(ally);
        }
    }
}
