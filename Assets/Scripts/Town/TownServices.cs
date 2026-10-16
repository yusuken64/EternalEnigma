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
    public bool OffersOpeningBread(TownBuildingDefinition building) =>
        town.Configuration.Id == "town-0" && building.Id == "bakery" &&
        Common.Instance.CampaignContext?.IsPreparingForOpening == true &&
        !Common.Instance.CampaignContext.State.BoughtOpeningBread;

    public List<TownShopOffer> Catalog(TownBuildingDefinition building) =>
        TownShopCatalog.Resolve(building, TownShopCatalog.Tier(town.Configuration.Id))
            .Select(offer => OffersOpeningBread(building) && offer.Item.ItemName == "Bread"
                ? new TownShopOffer { Item = offer.Item, Price = 0, Quantity = offer.Quantity, StackCount = 1, MinimumTier = offer.MinimumTier }
                : offer).ToList();

    public TownShopSaveData Shop(TownBuildingDefinition building)
    {
        string key = town.Configuration.Id + "/" + building.Id;
        var shop = Save.Shops.FirstOrDefault(s => s.Key == key);
        if (shop == null)
        {
            shop = new TownShopSaveData { Key = key, RestockCycle = -1 };
            Save.Shops.Add(shop);
        }
        if (shop.RestockCycle != Save.RestockCycle)
        {
            shop.Stock = Catalog(building).Select(o => new TownStockSaveData {
                ItemName = o.Item.ItemName, Remaining = o.Quantity }).ToList();
            shop.RestockCycle = Save.RestockCycle;
        }
        return shop;
    }

    public bool Buy(TownBuildingDefinition building, string itemName, out string reason)
    {
        var offer = Catalog(building).FirstOrDefault(o => o.Item.ItemName == itemName);
        var stock = Shop(building).Stock.FirstOrDefault(s => s.ItemName == itemName);
        reason = "This item is sold out.";
        if (offer == null || stock == null || stock.Remaining <= 0) return false;
        reason = $"Costs {offer.Price} gold; you have {Player.Gold}.";
        if (Player.Gold < offer.Price) return false;
        var item = offer.Item.AsInventoryItem(offer.Item.StackMax > 0 ? offer.StackCount : null);
        Player.Gold -= offer.Price;
        stock.Remaining--;
        Player.Inventory.Add(item);
        if (OffersOpeningBread(building) && itemName == "Bread")
        {
            Common.Instance.CampaignContext.State.BoughtOpeningBread = true;
            GameMessages.Post("The baker packs your first Bread for free. " + Common.Instance.CampaignContext.OpeningPreparationHint);
        }
        town.SaveProgress();
        reason = null;
        AudioManager.Instance?.PlayCue(SoundCue.Purchase);
        return true;
    }

    public static int SellPrice(InventoryItem item) => TownShopCatalog.SellPrice(item);

    public bool CanSell(InventoryItem item) => item != null && Player.Inventory.Contains(item) &&
        !Player.RecruitedAllies.Any(a => a.Equipment.IsEquipped(item)) && SellPrice(item) > 0;

    public string SellRestriction(InventoryItem item)
    {
        if (item == null || !Player.Inventory.Contains(item)) return "This item is no longer in your bag.";
        if (Player.RecruitedAllies.Any(a => a.Equipment.IsEquipped(item))) return "Unequip this item before selling it.";
        if (SellPrice(item) <= 0) return "This item has no sale value.";
        return Player.Gold > int.MaxValue - SellPrice(item) ? "Your gold is full." : null;
    }

    public bool Sell(InventoryItem item, out string reason)
    {
        reason = SellRestriction(item);
        int price = SellPrice(item);
        if (reason != null) return false;
        Player.Inventory.Remove(item);
        Player.Gold += price;
        town.SaveProgress();
        reason = null;
        AudioManager.Instance?.PlayCue(SoundCue.Sell);
        return true;
    }

    public bool CanLearn(TownAlly ally, Skill skill, out TrainerOffer offer, out string reason)
    {
        offer = null;
        reason = "This skill cannot be learned.";
        if (ally == null || !Player.RecruitedAllies.Contains(ally)) { reason = "This hero is not in the party."; return false; }
        if (skill == null) return false;
        offer = TrainerOffers.Find(ally, town.Configuration, skill);
        if (offer == null) { reason = "This hero's class cannot learn this skill."; return false; }
        if (offer.IsMaxed) { reason = offer.MaxRank <= 1 ? "Already learned." : "Already at maximum rank."; return false; }
        if (!offer.CanLearn) { reason = string.IsNullOrEmpty(offer.LockReason) ? "This skill cannot be learned." : offer.LockReason; return false; }
        if (!offer.UsesPoints && (offer.NextCost < 0 || Player.Gold < offer.NextCost))
        { reason = $"Costs {offer.NextCost} gold; you have {Player.Gold}."; return false; }
        reason = null;
        return true;
    }

    public bool Learn(TownAlly ally, Skill skill, out string reason)
    {
        if (!CanLearn(ally, skill, out var offer, out reason)) return false;
        // Class heroes pay in skill points, which are derived from the learned ranks (nothing to deduct).
        if (!offer.UsesPoints)
        {
            Player.Gold -= offer.NextCost;
        }
        ally.SetRank(offer.Skill.SkillName, offer.CurrentRank + 1);
        var context = Common.Instance.CampaignContext;
        if (context?.IsPreparingForOpening == true && town.Configuration.Id == "town-0" &&
            ally.Id == Common.Instance.GameSaveData.ProtagonistId && offer.Skill.ActivationType == ActivationType.Active)
        {
            context.State.LearnedOpeningSkill = true;
            GameMessages.Post("You learned your first active skill, " + offer.Skill.DisplayName + ". " + context.OpeningPreparationHint);
        }
        town.SaveProgress();
        reason = null;
        AudioManager.Instance?.PlayCue(SoundCue.Purchase);
        return true;
    }

    /// <summary>True when any party member has carried vitals to reset.</summary>
    public bool NeedsRest => Player.RecruitedAllies.Any(a => a.Hp >= 0 || a.Mana >= 0 || a.HasHunger);

    /// <summary>Free rest restores HP, Mana and hunger and resets hunger accumulation.</summary>
    public bool Rest(out string reason)
    {
        reason = "Everyone is already fully rested.";
        if (!NeedsRest) return false;
        foreach (var ally in Player.RecruitedAllies) { ally.Hp = -1; ally.Mana = -1; ally.HasHunger = false; ally.Hunger = 0; ally.HungerAccumulate = 0; }
        town.SaveProgress();
        reason = null;
        AudioManager.Instance?.PlayCue(SoundCue.Rest);
        return true;
    }

    /// <summary>Explicit save at the inn: also records the checkpoint a defeat restores.</summary>
    public bool SaveGame(out string reason)
    {
        town.WriteSaveData();
        var hero = Player.ControllingTownAlly;
        return CampaignSaving.Commit(Common.Instance, town.Configuration.Id + "/inn", hero.TilemapPosition, hero.CurrentFacing, out reason);
    }

    public int Donate(int requested) => Donate(requested, out _);

    public int Donate(int requested, out string reason)
    {
        reason = "Enter a donation greater than zero.";
        if (requested <= 0) return 0;
        reason = $"Requires {requested} gold; you have {Player.Gold}.";
        if (requested > Player.Gold) return 0;
        reason = "The donation total is full.";
        if (requested > int.MaxValue - Save.DonationTotal) return 0;
        Player.Gold -= requested;
        Save.DonationTotal += requested;
        town.SaveProgress();
        AudioManager.Instance?.PlayCue(SoundCue.Purchase);
        reason = null;
        return requested;
    }

    public bool CanEnter(DungeonTierData tier) => tier != null &&
        town.Configuration.DungeonTiers.Contains(tier);

    public bool Recruit(TownAlly ally, out string reason)
    {
        reason = $"Party limit is {town.Configuration.MaxPartySize}.";
        if (Player.RecruitedAllies.Count >= town.Configuration.MaxPartySize) return false;
        reason = "This ally is no longer available.";
        if (!town.TownAllies.Contains(ally)) return false;
        reason = $"Costs {ally.RecruitCost} gold; you have {Player.Gold}.";
        if (Player.Gold < ally.RecruitCost) return false;
        var context = Common.Instance.CampaignContext;
        if (context != null) { context.Roster.Add(ally.Id); context.Active.Add(ally.Id); }
        Player.Gold -= ally.RecruitCost;
        ally.EnsureStartingSkills();
        var loadout = HeroClassBinding.FromPrefab(ally, new TownAllyData());
        ally.Equipment.RestoreSaved(loadout.Equipment, Common.Instance.ItemManager, Player.Inventory.Add);
        AllyRecruitDialog.Recruit(town, ally);
        GameMessages.Post($"{ally.Name} joined the party.");
        town.SaveProgress();
        reason = null;
        AudioManager.Instance?.PlayCue(ally.RecruitCost > 0 ? SoundCue.Purchase : SoundCue.Pickup);
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
            GameMessages.Post($"{ally.Name} left the party.");
            town.RefreshCampaignParty(); reason = null; return true;
        }
        foreach (var item in ally.Equipment.GetEquippedItems().ToArray())
        {
            ally.Equipment.UnEquip(item);
            Player.Inventory.Add(item);
        }
        AllyRecruitDialog.RemoveAlly(town, ally);
        GameMessages.Post($"{ally.Name} left the party.");
        Player.EnsureControlledAlly();
        town.SaveProgress();
        reason = null;
        return true;
    }

    public bool ToggleEquipment(TownAlly ally, InventoryItem item) => ToggleEquipment(ally, item, out _);

    public bool ToggleEquipment(TownAlly ally, InventoryItem item, out string reason)
    {
        reason = "This hero is not in the party.";
        if (!Player.RecruitedAllies.Contains(ally)) return false;
        bool wasEquipped = ally.Equipment.IsEquipped(item);
        if (!EquipmentTransferService.Toggle(ally.Equipment, Player.Inventory, item,
            ally.PrimaryClass, ally.SecondaryClass, out reason)) return false;
        ally.RefreshEquipmentVisuals();
        town.SaveProgress();
        AudioManager.Instance?.PlayCue(wasEquipped ? SoundCue.Unequip : SoundCue.Equip);
        return true;
    }

}
