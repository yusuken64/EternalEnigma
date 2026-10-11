#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using UnityEngine;

public static class ChaosCatalog
{
    public static readonly string[] Devices = { "Keyboard", "Mouse", "Controller" };
    public static ChaosCoverage Build(Common common, TownConfiguration town)
    {
        var result = new ChaosCoverage();
        var seen = new HashSet<string>();
        void Add(string kind, string ability, string source, int rank, string variant, string device, bool demo = false)
        {
            string id = string.Join("/", kind, source, ability, rank, variant, device);
            if (seen.Add(id)) result.Cases.Add(new ChaosCase { Id = id, Kind = kind, Ability = ability, Source = source,
                Rank = rank, Variant = variant, Device = device, Demo = demo });
        }
        void SkillCases(Skill skill, string source, int maxRank, bool demo = false)
        {
            if (skill == null) return;
            string kind = skill.ActivationType == ActivationType.Active ? "Active" :
                skill.PassiveResponses?.OfType<ClassPassive>().Any() == true ? "ClassPassive" : "Passive";
            for (int rank = 1; rank <= Math.Max(1, maxRank); rank++)
            {
                if (skill.ActivationType == ActivationType.Passive)
                {
                    Add(kind, skill.SkillName, source, rank, "trigger-and-paired-effect", "Independent", demo);
                    continue;
                }
                var variants = new List<string> { "execute-" + skill.Targeting, "status-restriction" };
                if (skill.RequiresTargetSelection) variants.AddRange(new[] { "cancel-targeting", "invalid-target", "range-restriction" });
                if (skill.SPCost > 0 || skill.UsesArrows) variants.Add("insufficient-resources");
                if (skill.CastTime > 0) variants.Add("delayed-release");
                if (skill.AreaRadius > 0 || skill.Targeting == SkillTargeting.AllTargets) variants.Add("multiple-targets");
                foreach (var variant in variants) foreach (var device in Devices) Add(kind, skill.SkillName, source, rank, variant, device, demo);
            }
        }
        var production = new HashSet<Skill>();
        var catalog = ClassCatalog.Load();
        foreach (var definition in catalog.Classes.Where(c => c != null).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            result.Participation.Add(new ChaosParticipation { Kind = "Class", Id = definition.Id });
            foreach (var entry in definition.Skills.Where(e => e?.Skill != null))
            { production.Add(entry.Skill); SkillCases(entry.Skill, "class:" + definition.Id, entry.MaxRank); }
        }
        foreach (var hero in town.AllyCatalog.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            result.Participation.Add(new ChaosParticipation { Kind = "Hero", Id = hero.Id });
            foreach (var name in hero.Skills ?? new())
            {
                var skill = common.SkillManager.GetSkillByName(name);
                if (skill != null && production.Add(skill)) SkillCases(skill, "hero:" + hero.Id, skill.MaxRank);
            }
        }
        foreach (var skill in town.LearnableSkills.Concat(common.SkillManager.SkillPrefabs).Where(s => s != null))
            if (production.Add(skill)) SkillCases(skill, "learnable", skill.MaxRank);
        foreach (var item in ChaosFixtures.Items(common.ItemManager))
        {
            foreach (var device in Devices)
            {
                Add("ItemFlow", item.ItemName, "item", 0, "inspect", device);
                Add("ItemFlow", item.ItemName, "item", 0, "discard", device);
                if (item is EquipmentItemDefinition) Add("ItemFlow", item.ItemName, "item", 0, "equip-and-unequip", device);
                if (item is UsableItemDefinition usable)
                {
                    Add("ItemFlow", item.ItemName, "item", 0, "use-" + usable.Targeting, device);
                    if (usable.Targeting != SkillTargeting.Self && usable.Targeting != SkillTargeting.AllTargets)
                        Add("ItemFlow", item.ItemName, "item", 0, "cancel-targeting", device);
                }
            }
            if (item.ItemEffectDefinition is SkillItemEffectDefinition effect && effect.EffectSkill != null)
            { production.Add(effect.EffectSkill); SkillCases(effect.EffectSkill, "item:" + item.ItemName, 1); }
        }
        var campaign = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42));
        foreach (var location in campaign.Campaign.Locations)
        {
            var skill = DungeonClearAbilities.ForDungeon(location.Id);
            if (skill != null && production.Add(skill)) SkillCases(skill, "dungeon-clear:" + location.Id, 1);
        }
        foreach (var skill in Resources.LoadAll<Skill>("Classes/Skills").Where(s => !production.Contains(s)))
        { production.Add(skill); SkillCases(skill, "production-resource", skill.MaxRank); }
        var demo = DemoDungeonLoadout.Load();
        if (demo != null) foreach (var skill in demo.Skills.Where(s => s != null && !production.Contains(s))) SkillCases(skill, "demo", skill.MaxRank, true);
        foreach (var feature in new[] { "campaign-create", "campaign-load", "settings", "nested-dialogs", "device-switching", "party-management", "inventory", "equipment",
            "town-services", "overworld-travel", "dungeon-interactions", "scene-transition", "defeat", "endgame", "targeting-cancel" })
            foreach (var device in Devices) Add("Feature", feature, "feature", 0, "flow", device);
        result.Cases = result.Cases.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        return result;
    }
    public static void Record(ChaosCoverage coverage, ChaosEvidence evidence)
    {
        var row = coverage.Cases.Single(c => c.Id == evidence.CaseId);
        if (evidence.Satisfies(row)) { row.Passed = true; row.Reason = null; }
        else if (!row.Passed) row.Reason = evidence.Failure ?? "Incomplete effect, cost, turn, trigger, or control evidence";
    }
}
#endif
