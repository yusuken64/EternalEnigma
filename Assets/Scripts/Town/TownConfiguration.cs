using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Town/Configuration")]
public class TownConfiguration : ScriptableObject
{
    // Resource key under Resources/Towns; also identifies persistent town state.
    public string Id = "DefaultTown";
    public Vector3Int PartySpawn = new(10, 4, 0);
    public string BuildingLayer = "Buildings";
    public string AllyLayer = "Allies";
    [Min(1)] public int MaxPartySize = 4;
    public List<TownBuildingDefinition> Buildings = new();
    public List<TownAlly> AllyCatalog = new();
    public List<TownRecruitOffer> Recruits = new();
    public List<TownAlly> StartingParty = new();
    public List<DungeonTierData> DungeonTiers = new();
    public List<Skill> LearnableSkills = new();
    public bool LoseItemsOnDefeat = true;
    public bool KeepGoldOnDefeat = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || MaxPartySize < 1)
            throw new InvalidOperationException("Town requires an ID and a positive party limit.");
        if (Buildings.Any(b => b == null || b.Prefab == null || string.IsNullOrWhiteSpace(b.Id) ||
            (b.DialogPrefab == null && string.IsNullOrWhiteSpace(b.DialogId))) ||
            Buildings.Select(b => b.Id).Distinct().Count() != Buildings.Count)
            throw new InvalidOperationException($"Town '{Id}' has missing or duplicate building definitions.");
        if (AllyCatalog.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id)) ||
            AllyCatalog.Select(a => a.Id).Distinct().Count() != AllyCatalog.Count)
            throw new InvalidOperationException($"Town '{Id}' requires unique ally IDs in its catalog.");
        if (Recruits.Any(r => r == null || !AllyCatalog.Contains(r.Ally) || r.Cost < 0) ||
            Recruits.Select(r => r.Ally).Distinct().Count() != Recruits.Count)
            throw new InvalidOperationException($"Town '{Id}' has invalid recruit offers.");
        if (StartingParty.Count == 0 || StartingParty.Count > MaxPartySize || StartingParty.Any(a => !AllyCatalog.Contains(a)) ||
            StartingParty.Distinct().Count() != StartingParty.Count)
            throw new InvalidOperationException($"Town '{Id}' requires a valid starting party.");
        if (DungeonTiers.Any(t => t == null || t.StartFloor < 1 || t.EndFloor < t.StartFloor || t.RequiredDonation < 0))
            throw new InvalidOperationException($"Town '{Id}' has an invalid dungeon tier.");
        foreach (var building in Buildings) building.Validate();
    }
}

[Serializable]
public class TownRecruitOffer
{
    public TownAlly Ally;
    [Min(0)] public int Cost = 300;
}
