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

    /// Deterministic: rolls index into the remaining unrecruited offers (configuration order); each pick is removed before the next roll.
    internal List<TownAlly> GenerateRandomAllies(IReadOnlyList<int> rolls, IEnumerable<string> recruited)
    {
        var names = recruited.ToHashSet();
        var candidates = configuration.Recruits.Where(r => !names.Contains(r.Ally.Id)).ToList();
        var result = new List<TownAlly>();
        foreach (int roll in rolls)
        {
            if (candidates.Count == 0) break;
            var offer = candidates[(int)((uint)roll % (uint)candidates.Count)];
            candidates.Remove(offer);
            var ally = Instantiate(offer.Ally, transform); ally.RecruitCost = offer.Cost; result.Add(ally);
        }
        return result;
    }
}
