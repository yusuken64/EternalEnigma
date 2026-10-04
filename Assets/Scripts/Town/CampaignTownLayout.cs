using System;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using UnityEngine;

/// <summary>Maps Core's seeded service slots onto authored Unity building definitions.</summary>
public static class CampaignTownLayout
{
    public static void Configure(TownConfiguration configuration, TownLayout layout)
    {
        var authored = configuration.Buildings.Where(b => b.DialogId == "entrance").ToArray();
        if (authored.Length != CampaignContext.AuthoredTownBuildings)
            throw new InvalidOperationException("Campaign towns require an entrance definition.");
        var definitions = Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings");
        var slots = new TownBuildingDefinition[layout.SlotServices.Count];
        int nextAuthored = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            var service = layout.SlotServices[i];
            if (service == null)
            {
                if (nextAuthored < authored.Length) slots[i] = authored[nextAuthored++];
                continue; // Remaining slots are residential geometry, without an interaction.
            }
            var matches = definitions.Where(d => d.Id == service.Id).ToArray();
            if (matches.Length != 1 || !matches[0].HasInterior)
                throw new InvalidOperationException($"Town service '{service.Id}' requires one authored interior definition.");
            slots[i] = matches[0];
        }
        if (configuration.Id == "town-0")
        {
            int home = Array.FindIndex(slots, d => d == null);
            if (home < 0) throw new InvalidOperationException("Starting town requires a residential home.");
            var definition = UnityEngine.Object.Instantiate(definitions.Single(d => d.Id == "inn"));
            definition.Id = "home"; definition.DisplayName = "Home"; definition.DialogId = "home";
            slots[home] = definition;
        }
        configuration.Layout = layout;
        configuration.SlotBuildings = slots;
        configuration.Buildings = slots.Where(d => d != null).ToList();
        configuration.PartySpawn = new Vector3Int(layout.Options.PartySpawn.X, layout.Options.PartySpawn.Y, 0);
        configuration.Validate();
    }
}
