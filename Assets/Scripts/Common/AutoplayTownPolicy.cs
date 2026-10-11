using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using UnityEngine;

public sealed partial class AutoplayRunner
{
    private readonly HashSet<string> purchased = new(), failedPurchases = new(), failedTraining = new();

    internal bool WantsOffer(Town town, TownBuildingDefinition building, TownShopOffer offer)
    {
        string key = town.Configuration.Id + "/" + building.Id + "/" + offer.Item.ItemName;
        if (failedPurchases.Contains(key)) return false;
        string reason = PurchaseRejection(town,building,offer);
        if (reason != null) { Skip("buy/"+key,reason,item:offer.Item.ItemName); return false; }
        if (CoverageEnabled && !purchased.Contains(offer.Item.ItemName)) return true;
        if (offer.Item is EquipmentItemDefinition equipment)
        {
            if (equipment.IsAmmunition)
                return town.TownPlayer.Inventory.Count(i => i.ItemName == offer.Item.ItemName) < 2;
            return town.TownPlayer.RecruitedAllies.Any(a => IsUpgrade(a,equipment));
        }
        return offer.Item.ItemEffectDefinition is ModifyStatsItemEffectDefinition effect &&
            (effect.VitalModification.Hp > 0 || effect.VitalModification.Sp > 0 || effect.VitalModification.Hunger > 0) &&
            town.TownPlayer.Inventory.Count(i => i.ItemName == offer.Item.ItemName) < 3;
    }

    internal static string PurchaseRejection(Town town, TownBuildingDefinition building, TownShopOffer offer)
    {
        if (!town.Services.Catalog(building).Any(o => o.Item.ItemName == offer.Item.ItemName)) return "Not offered in this town's resolved catalog.";
        if ((town.Services.Shop(building).Stock.FirstOrDefault(s => s.ItemName == offer.Item.ItemName)?.Remaining ?? 0) <= 0) return "Sold out.";
        if (offer.Price < 0 || offer.Price > town.TownPlayer.Gold) return "Not enough gold.";
        if (offer.Item is EquipmentItemDefinition equipment && !town.TownPlayer.RecruitedAllies.Any(a =>
            HeroClass.AllowsItem(a.PrimaryClass,a.SecondaryClass,(EquipableInventoryItem)equipment.AsInventoryItem(null))))
            return "No current party member has the equipment proficiency.";
        return null;
    }

    internal bool BuyOffer(Town town, TownBuildingDefinition building, TownShopOffer offer)
    {
        string key = town.Configuration.Id + "/" + building.Id + "/" + offer.Item.ItemName;
        if (failedPurchases.Contains(key)) return false;
        var stock = town.Services.Shop(building).Stock.FirstOrDefault(s => s.ItemName == offer.Item.ItemName);
        int before = stock?.Remaining ?? 0;
        bool success = town.Services.Buy(building,offer.Item.ItemName,out var reason);
        Report.Purchases.Add(new AutoplayTransaction { Name = offer.Item.ItemName, Building = key,
            ActorId = town.TownPlayer.ControllingTownAlly.Id, Cost = offer.Price, Currency = "Gold", Success = success, Reason = reason });
        WriteEvent(new AutoplayEvent { Kind = "Purchase", ActorId = town.TownPlayer.ControllingTownAlly.Id,
            Item = offer.Item.ItemName, Building = key, Cost = offer.Price, Currency = "Gold", Success = success,
            StockBefore = before, StockAfter = stock?.Remaining ?? 0, Message = reason ?? "Purchased" });
        if (success) { purchased.Add(offer.Item.ItemName); EquipParty(town); Log("Buy " + offer.Item.ItemName); }
        else { failedPurchases.Add(key); Issue("PolicyFailure","Purchase rejected: " + offer.Item.ItemName + ": " + reason); }
        return success;
    }

    internal static int TrainingPriority(TrainerOffer offer) =>
        offer.Kind == SkillKind.Mastery && offer.CurrentRank == 0 ? 0 : offer.CurrentRank == 0 ? 1 : 2;

    internal (TownAlly ally, TrainerOffer offer) NextTraining(Town town)
    {
        var candidates = new List<(TownAlly ally,TrainerOffer offer)>();
        foreach (var ally in town.TownPlayer.RecruitedAllies)
            foreach (var offer in TrainerOffers.Build(ally,town.Configuration))
            {
                string key = ally.Id + "/" + offer.Skill.SkillName;
                if (offer.CurrentRank > 0)
                    Ability(ally.Id,offer.Skill.SkillName,offer.CurrentRank,offer.Skill.ActivationType == ActivationType.Active);
                string reason = offer.IsMaxed ? "Maximum rank reached." : !offer.CanLearn ? offer.LockReason :
                    !offer.UsesPoints && (offer.NextCost < 0 || offer.NextCost > town.TownPlayer.Gold) ? "Not enough gold." : null;
                if (reason != null) { Skip("learn/"+key,reason,ally.Id,offer.Skill.SkillName); continue; }
                if (!failedTraining.Contains(key+"/"+offer.CurrentRank)) candidates.Add((ally,offer));
            }
        return candidates.OrderBy(c => TrainingPriority(c.offer)).ThenBy(c => c.offer.Tier)
            .ThenBy(c => c.offer.NextCost).ThenBy(c => c.ally.Id).ThenBy(c => c.offer.Skill.SkillName).FirstOrDefault();
    }

    internal bool LearnOffer(Town town, TownAlly ally, TrainerOffer offer)
    {
        string key = ally.Id+"/"+offer.Skill.SkillName+"/"+offer.CurrentRank;
        if (failedTraining.Contains(key)) return false;
        bool success = town.Services.Learn(ally,offer.Skill,out var reason);
        int rank = ally.GetRank(offer.Skill.SkillName);
        Report.Training.Add(new AutoplayTransaction { ActorId = ally.Id, Name = offer.Skill.SkillName,
            Currency = offer.UsesPoints ? "SkillPoints" : "Gold", Cost = offer.NextCost,
            RankBefore = offer.CurrentRank, RankAfter = rank, Success = success, Reason = reason });
        WriteEvent(new AutoplayEvent { Kind = "Training", ActorId = ally.Id, Skill = offer.Skill.SkillName,
            Cost = offer.NextCost, Currency = offer.UsesPoints ? "SkillPoints" : "Gold", RankBefore = offer.CurrentRank,
            RankAfter = rank, Success = success, Message = reason ?? "Learned" });
        if (success)
        {
            Ability(ally.Id,offer.Skill.SkillName,rank,offer.Skill.ActivationType == ActivationType.Active);
            Log("Learn " + ally.Name + ": " + offer.Skill.SkillName + " rank " + rank);
        }
        else { failedTraining.Add(key); Issue("PolicyFailure","Training rejected: " + offer.Skill.SkillName + ": " + reason,actorId:ally.Id); }
        // No cached offers: the next tick rebuilds prerequisites, points and ranks for every hero.
        return success;
    }

    private static bool IsTrainer(Town town, TownBuilding building) => building.Definition.DialogPrefab is BallistaDialog ||
        FindFirstObjectByType<TownMenu>().BuildingDialogs.Any(d => d.Id == building.Definition.DialogId && d.Dialog is BallistaDialog);

    private static Vector3Int ServicePosition(Town town, TownBuilding building)
    {
        var vendor = town.GetComponentsInChildren<ShopVendor>().FirstOrDefault(v => v.Building == building.Definition);
        if (!building.HasInterior || vendor == null) return building.TilemapPosition;
        return new[] { Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.up }
            .Select(step => vendor.TilemapPosition + step).First(p => town.CanEnter(p));
    }
}
