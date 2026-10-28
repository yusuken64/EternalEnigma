using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Experiments change loaded assets only and restore them at session cleanup. No AssetDatabase writes.
// Every change also reaches runtime clones through normal new-campaign initialization.
public sealed class BalanceExperimentScope : IDisposable
{
    readonly List<(Skill skill, int mana)> skills = new();
    readonly List<(TownShopOffer offer, int price)> offers = new();
    public static int ScaleCost(int value, float multiplier) => value == 0 ? 0 : Math.Max(1,
        (int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));

    public BalanceExperimentScope(BalanceExperimentOptions options)
    {
        if (options == null || !options.Validate(out _)) throw new ArgumentException("Invalid balance experiment.");
        // Resolve every target before applying any mutation; unknown names cannot silently pass.
        var allSkills = Resources.LoadAll<Skill>("Classes/Skills").Concat(Resources.LoadAll<Skill>("Skills")).Distinct().ToArray();
        var allOffers = Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings").SelectMany(b => b.ShopCatalog).ToArray();
        foreach (var row in options.AbilityMana)
            if (!allSkills.Any(s => s.SkillName == row.Name)) throw new ArgumentException("Unknown ability: " + row.Name);
        foreach (var row in options.ItemPrices)
            if (!allOffers.Any(o => o.Item != null && o.Item.ItemName == row.Name)) throw new ArgumentException("Unknown shop item: " + row.Name);
        foreach (var row in options.AbilityMana)
            foreach (var skill in allSkills.Where(s => s.SkillName == row.Name))
            { skills.Add((skill, skill.ManaCost)); skill.ManaCost = ScaleCost(skill.ManaCost, row.Multiplier); }
        foreach (var row in options.ItemPrices)
            foreach (var offer in allOffers.Where(o => o.Item != null && o.Item.ItemName == row.Name))
            { offers.Add((offer, offer.Price)); offer.Price = ScaleCost(offer.Price, row.Multiplier); }
    }

    public void Dispose()
    {
        foreach (var row in skills) if (row.skill != null) row.skill.ManaCost = row.mana;
        foreach (var row in offers) row.offer.Price = row.price;
        skills.Clear(); offers.Clear();
    }
}
