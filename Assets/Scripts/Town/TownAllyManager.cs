using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TownAllyManager : MonoBehaviour
{
    private TownConfiguration configuration;
    public void Configure(TownConfiguration value) => configuration = value;
    internal TownAlly GetAlly(TownAllyData data) =>
        (Common.Instance.CampaignContext != null ? CampaignParty.Resolve(data.AllyId, configuration) : null) ??
        configuration.AllyCatalog.FirstOrDefault(a => !string.IsNullOrEmpty(data.AllyId) ? a.Id == data.AllyId : a.Name == data.AllyName) ??
        throw new InvalidOperationException($"Ally '{data.AllyName}' is missing from town '{configuration.Id}' catalog.");

    internal List<TownAlly> GenerateRandomAllies(int capacity, IEnumerable<string> recruited)
    {
        var names = recruited.ToHashSet();
        var candidates = configuration.Recruits.Where(r => !names.Contains(r.Ally.Id)).ToList();
        var result = new List<TownAlly>();
        foreach (var offer in candidates.Sample(Mathf.Min(Mathf.Max(0, capacity), candidates.Count)))
        {
            var ally = Instantiate(offer.Ally, transform);
            ally.RecruitCost = offer.Cost;
            result.Add(ally);
        }
        return result;
    }
}
