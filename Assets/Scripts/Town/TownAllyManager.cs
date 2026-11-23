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

    internal List<TownAlly> GenerateRandomAllies(IReadOnlyList<int> rolls, IEnumerable<string> recruited)
    {
        var result = new List<TownAlly>();
        foreach (var offer in SelectOffers(rolls, recruited))
        {
            var ally = Instantiate(offer.Ally, transform);
            ally.Level = 1;
            ally.Experience = 0;
            ally.Attributes = default;
            ally.HighestLevel = 1;
            ally.RecruitCost = offer.Cost;
            ally.RefreshEquipmentVisuals();
            result.Add(ally);
        }
        return result;
    }

    // Spawning and traveler directions evaluate the same rolls against the current roster.
    internal IEnumerable<TownRecruitOffer> SelectOffers(IReadOnlyList<int> rolls, IEnumerable<string> recruited)
    {
        var ids = recruited.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        var common = Common.InstanceOrNull;
        var unavailable = configuration.AllyCatalog.Where(a => ids.Contains(a.Id)).ToList();
        if (common?.CampaignContext != null)
        {
            // Campaign reward IDs are aliases for authored heroes. Reserve those heroes
            // so a town offer cannot create another copy before or after the reward.
            unavailable.AddRange(common.CampaignContext.Campaign.Companions
                .Select(c => CampaignParty.Resolve(c.Id, configuration)).Where(a => a != null));
        }
        var savedNames = (common?.GameSaveData?.Roster ?? new List<TownAllyData>())
            .Concat(common?.GameSaveData?.TownSaveData?.RecruitedAlliesData ?? new List<TownAllyData>())
            .Select(a => a.AllyName).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = configuration.Recruits.Where(r => !ids.Contains(r.Ally.Id) &&
            !savedNames.Contains(r.Ally.Name) && !unavailable.Any(a => SameHero(a, r.Ally))).ToList();
        foreach (int roll in rolls)
        {
            if (candidates.Count == 0) yield break;
            var offer = candidates[(int)((uint)roll % (uint)candidates.Count)];
            candidates.RemoveAll(candidate => SameHero(candidate.Ally, offer.Ally));
            yield return offer;
        }
    }

    internal static bool SameHero(TownAlly a, TownAlly b) => a != null && b != null &&
        (!string.IsNullOrEmpty(a.Id) && a.Id == b.Id || !string.IsNullOrWhiteSpace(a.Name) &&
            string.Equals(a.Name.Trim(), b.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
}
