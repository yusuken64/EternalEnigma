using System;
using System.Collections.Generic;
using System.Linq;

// Gameplay changes commit here, independently of dialog lifetime and close paths.
public sealed class TownServices
{
    private readonly Town town;
    private TownPlayer Player => town.TownPlayer;
    private TownSaveData Save => Common.Instance.GameSaveData.TownSaveData;
    public TownServices(Town town) { this.town = town; }

    public TownShopSaveData Shop(TownBuildingDefinition building)
    {
        string key = town.Configuration.Id + "/" + building.Id;
        var shop = Save.Shops.FirstOrDefault(s => s.Key == key);
        if (shop == null)
        {
            shop = new TownShopSaveData { Key = key };
            Save.Shops.Add(shop);
        }
        if (shop.RestockVersion != Save.RestockVersion)
        {
            shop.Stock = building.ShopCatalog.Select(o => new TownStockSaveData {
                ItemName = o.Item.ItemName, Remaining = o.Quantity }).ToList();
            shop.RestockVersion = Save.RestockVersion;
        }
        return shop;
    }

    public bool Buy(TownBuildingDefinition building, string itemName, out string reason)
    {
        var offer = building.ShopCatalog.FirstOrDefault(o => o.Item.ItemName == itemName);
        var stock = Shop(building).Stock.FirstOrDefault(s => s.ItemName == itemName);
        reason = "This item is sold out.";
        if (offer == null || stock == null || stock.Remaining <= 0) return false;
        reason = "Not enough gold.";
        if (Player.Gold < offer.Price) return false;
        var item = offer.Item.AsInventoryItem(offer.Item.StackMax > 0 ? offer.StackCount : null);
        Player.Gold -= offer.Price;
        stock.Remaining--;
        Player.Inventory.Add(item);
        town.SaveProgress();
        reason = null;
        return true;
    }

    public bool Learn(TownAlly ally, Skill skill, out string reason)
    {
        reason = "This skill cannot be learned.";
        if (!Player.RecruitedAllies.Contains(ally) || !town.Configuration.LearnableSkills.Contains(skill)) return false;
        reason = "Already learned.";
        if (ally.Skills.Contains(skill.SkillName)) return false;
        reason = "Not enough gold.";
        if (skill.LearnCost < 0 || Player.Gold < skill.LearnCost) return false;
        Player.Gold -= skill.LearnCost;
        ally.Skills.Add(skill.SkillName);
        town.SaveProgress();
        reason = null;
        return true;
    }

    public int Donate(int requested)
    {
        int amount = Math.Min(Math.Max(0, requested), Math.Min(Player.Gold, int.MaxValue - Save.DonationTotal));
        Player.Gold -= amount;
        Save.DonationTotal += amount;
        town.SaveProgress();
        return amount;
    }

    public bool CanEnter(DungeonTierData tier) => tier != null &&
        town.Configuration.DungeonTiers.Contains(tier) && Save.DonationTotal >= tier.RequiredDonation;

    public bool Recruit(TownAlly ally, out string reason)
    {
        reason = $"Party limit is {town.Configuration.MaxPartySize}.";
        if (Player.RecruitedAllies.Count >= town.Configuration.MaxPartySize) return false;
        reason = "This ally is no longer available.";
        if (!town.TownAllies.Contains(ally)) return false;
        reason = "Not enough gold.";
        if (Player.Gold < ally.RecruitCost) return false;
        var context = Common.Instance.CampaignContext;
        if (context != null) { context.Roster.Add(ally.Id); context.Active.Add(ally.Id); }
        Player.Gold -= ally.RecruitCost;
        AllyRecruitDialog.Recruit(town, ally);
        town.SaveProgress();
        reason = null;
        return true;
    }

    public bool Dismiss(TownAlly ally, out string reason)
    {
        reason = "Keep at least one party member.";
        if (Player.RecruitedAllies.Count <= 1) return false;
        reason = "This ally is not in the party.";
        if (!Player.RecruitedAllies.Contains(ally)) return false;
        var context = Common.Instance.CampaignContext;
        if (context != null)
        {
            if (ally.Id == Common.Instance.GameSaveData.ProtagonistId) { reason = "The protagonist stays in the party."; return false; }
            town.WriteSaveData(); context.Active.Remove(ally.Id);
            town.RefreshCampaignParty(); reason = null; return true;
        }
        foreach (var item in ally.Equipment.GetEquippedItems().ToArray())
        {
            ally.Equipment.UnEquip(item);
            Player.Inventory.Add(item);
        }
        AllyRecruitDialog.RemoveAlly(town, ally);
        Player.EnsureControlledAlly();
        town.SaveProgress();
        reason = null;
        return true;
    }

    public bool ToggleEquipment(TownAlly ally, InventoryItem item)
    {
        if (!Player.RecruitedAllies.Contains(ally) || item is not EquipableInventoryItem equipment) return false;
        if (ally.Equipment.IsEquipped(item))
        {
            ally.Equipment.UnEquip(equipment);
            Player.Inventory.Add(item);
        }
        else
        {
            if (!Player.Inventory.Remove(item)) return false;
            var previous = ally.Equipment.GetEquippedItems().ToArray();
            ally.Equipment.Equip(equipment);
            Player.Inventory.AddRange(previous.Where(p => !ally.Equipment.IsEquipped(p)));
        }
        ally.RefreshEquipmentVisuals();
        town.SaveProgress();
        return true;
    }
}
