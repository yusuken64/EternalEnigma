using System.Collections.Generic;
using System.Linq;

public static class DungeonReturnService
{
    // Idempotent so duplicate confirmation/transition callbacks cannot award gold twice.
    public static void Commit(GameSaveData save, TownConfiguration configuration, bool victory,
        int gold, IEnumerable<InventoryItem> bag, IEnumerable<Ally> allies, bool keepLoot = false)
    {
        if (save.DungeonSaveData.ReturnCommitted) return;
        var town = save.TownSaveData;
        bool loot = victory || keepLoot;
        // Gold is the carried balance, including the money brought from town.
        // Losing dungeon loot retains the pre-run balance, as before.
        if (loot || configuration.KeepGoldOnDefeat) town.Gold = gold;
        bool keepItems = loot || !configuration.LoseItemsOnDefeat;
        town.InventoryItems = keepItems ? ItemSaveData.Capture(bag) : new();
        foreach (var member in town.RecruitedAlliesData)
        {
            var ally = allies.FirstOrDefault(a => a != null && (!string.IsNullOrEmpty(member.AllyId)
                ? a.TownAllyId == member.AllyId : a.CharacterName == member.AllyName));
            member.Equipment = keepItems && ally != null ? ItemSaveData.Capture(ally.Equipment.GetEquippedItems()) : new();
            if (ally != null && ally.SkillsForgotten)
            {
                member.Skills = TownAlly.StartingSkillNames(ally.PrimaryClass, ally.SecondaryClass).ToList();
                member.SkillRanks = new();
            }
            if (ally != null && ally.Vitals != null)
            {
                member.Level = ally.Vitals.Level; member.Experience = ally.Vitals.Exp;
                member.HighestLevel = System.Math.Max(System.Math.Max(1, member.HighestLevel), ally.Vitals.Level);
                // Damage persists after a successful return; a defeat resets the party to full.
                member.Hp = loot ? System.Math.Max(1, ally.Vitals.HP) : -1;
                member.Sp = loot ? ally.Vitals.SP : -1;
                member.HasHunger = loot;
                member.Hunger = loot ? ally.Vitals.Hunger : 0;
                member.HungerAccumulate = loot ? ally.Vitals.HungerAccumulate : 0;
            }
        }
        if (victory)
        {
            string tier = $"{configuration.Id}/{save.DungeonSaveData.StartFloor}-{save.DungeonSaveData.EndFloor}";
            if (!town.CompletedTiers.Contains(tier)) town.CompletedTiers.Add(tier);
        }
        if (loot) town.RestockCycle++;
        save.DungeonSaveData.ReturnCommitted = true;
    }
}
