using System.Collections.Generic;
using System.Linq;

public static class DungeonReturnService
{
    // Idempotent so duplicate confirmation/transition callbacks cannot award gold twice.
    public static void Commit(GameSaveData save, TownConfiguration configuration, bool victory,
        int gold, IEnumerable<InventoryItem> bag, IEnumerable<Ally> allies)
    {
        if (save.DungeonSaveData.ReturnCommitted) return;
        var town = save.TownSaveData;
        if (victory || configuration.KeepGoldOnDefeat) town.Gold += gold;
        bool keepItems = victory || !configuration.LoseItemsOnDefeat;
        town.InventoryItems = keepItems ? ItemSaveData.Capture(bag) : new();
        town.InventoryFormatVersion = 1;
        town.Inventory = town.InventoryItems.Select(i => i.ItemName).ToList();
        foreach (var member in town.RecruitedAlliesData)
        {
            var ally = allies.FirstOrDefault(a => a != null && (!string.IsNullOrEmpty(member.AllyId)
                ? a.TownAllyId == member.AllyId : a.CharacterName == member.AllyName));
            member.Equipment = keepItems && ally != null ? ItemSaveData.Capture(ally.Equipment.GetEquippedItems()) : new();
            if (ally != null && ally.Vitals != null)
                member.HighestLevel = System.Math.Max(System.Math.Max(1, member.HighestLevel), ally.Vitals.Level);
        }
        if (victory)
        {
            string tier = $"{configuration.Id}/{save.DungeonSaveData.StartFloor}-{save.DungeonSaveData.EndFloor}";
            if (!town.CompletedTiers.Contains(tier)) town.CompletedTiers.Add(tier);
        }
        town.RestockVersion++;
        save.DungeonSaveData.ReturnCommitted = true;
    }
}
